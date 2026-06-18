// Vastior - camera zoom manager for Grim Dawn (standalone .exe, dark brass theme).
// Native WinForms, owner-drawn. Detects install status (read-only) and performs
// install / repair / uninstall in-process via VastiorInstaller - no scripts and
// no PowerShell. It only ever touches the camera files it installs; characters,
// shared stash, saves, and Steam Cloud data are never modified.
//
// Build (see CONTRIBUTING.md): a C# compiler such as the .NET SDK, Visual Studio
// Build Tools, or the in-box framework compiler, compiling all src\*.cs together:
//   csc /target:winexe /out:Vastior.exe /r:System.dll /r:System.Drawing.dll
//       /r:System.Windows.Forms.dll src\*.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;
using L = Vastior.VastiorLayout;

namespace Vastior
{
	static class Program
	{
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		static extern bool SetProcessDPIAware();

		[STAThread]
		static void Main()
		{
			try { SetProcessDPIAware(); }
			catch { }
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new VastiorForm());
		}
	}

	class VastiorForm : Form, IBackdropProvider
	{
		const string AppName = "Vastior";
		const string AppVersion = "1.0";
		const string FolderDialogTitle = "Select your Grim Dawn install folder";
		const string PreferredPath = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Grim Dawn";
		const string FooterText = "Vastior installs only camera files and backs up your saves first. Not affiliated with Crate Entertainment.";

		string _appDir;
		bool _busy;
		int _statusKind; // 0 = not installed, 1 = installed, 2 = unknown
		bool _dragging;
		Point _dragOffset;
		Bitmap _backdrop;

		Rectangle _inner, _dragArea, _field, _statusRow, _badge, _console;
		int _actionsY, _utilY, _footerY;

		TextBox _pathBox;
		VastiorLogConsole _log;
		VastiorButton _btnBrowse, _btnInstall, _btnRepair, _btnUninstall, _btnRefresh, _btnOpen, _btnClose;

		static int S(int value)
		{
			return VastiorTheme.S(value);
		}

		public VastiorForm()
		{
			using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
			{
				VastiorTheme.Scale = g.DpiX / 96f;
			}
			_appDir = AppDomain.CurrentDomain.BaseDirectory;
			FormBorderStyle = FormBorderStyle.None;
			StartPosition = FormStartPosition.CenterScreen;
			BackColor = VastiorTheme.PanelBot;
			Text = AppName + " - Manager";
			try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
			catch { }
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

			BuildLayout();
			BuildBackdrop();
			BuildControls();
			Shown += delegate
			{
				AutoDetect();
				UpdateStatus();
				ShowIntro();
			};
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && _backdrop != null)
			{
				_backdrop.Dispose();
				_backdrop = null;
			}
			base.Dispose(disposing);
		}

		// ---------------------------------------------------------- layout -----
		void BuildLayout()
		{
			int frame = S(L.Frame);
			int width = S(L.WindowWidth);
			int innerW = width - 2 * frame;
			int bx = frame + S(L.PadX);
			int bw = innerW - 2 * S(L.PadX);

			_dragArea = new Rectangle(bx, frame, bw, S(L.HeadingBlock));

			int fieldY = frame + S(L.HeadingBlock);
			int labelW = S(L.FieldLabelWidth);
			int browseSpace = S(L.BrowseWidth) + S(L.BrowseGap);
			_field = new Rectangle(bx + labelW, fieldY, bw - labelW - browseSpace, S(L.FieldHeight));

			int statusY = _field.Bottom + S(L.FieldToStatus);
			_statusRow = new Rectangle(bx, statusY, bw, S(L.StatusRowHeight));
			_badge = new Rectangle(bx + S(L.StatusLabelWidth) + S(L.StatusLabelGap), statusY, S(L.BadgeWidth), S(L.StatusRowHeight));

			int consoleY = _statusRow.Bottom + S(L.StatusToConsole);
			_console = new Rectangle(bx, consoleY, bw, S(L.ConsoleHeight));

			_actionsY = _console.Bottom + S(L.ConsoleFrame) + S(L.ConsoleToActions);
			_utilY = _actionsY + S(L.ActionRowHeight) + S(L.ActionsToUtil);
			_footerY = _utilY + S(L.UtilRowHeight) + S(L.UtilToFooter);

			int height = _footerY + S(L.FooterHeight) + frame;
			ClientSize = new Size(width, height);
			_inner = new Rectangle(frame, frame, innerW, height - 2 * frame);
		}

		void BuildBackdrop()
		{
			if (_backdrop != null)
			{
				_backdrop.Dispose();
				_backdrop = null;
			}
			if (_inner.Width <= 0 || _inner.Height <= 0)
			{
				return;
			}
			_backdrop = new Bitmap(_inner.Width, _inner.Height, PixelFormat.Format32bppPArgb);
			using (Graphics g = Graphics.FromImage(_backdrop))
			{
				g.SmoothingMode = SmoothingMode.AntiAlias;
				Rectangle local = new Rectangle(0, 0, _inner.Width, _inner.Height);
				VastiorTheme.FillRectV(g, local, VastiorTheme.PanelTop, VastiorTheme.PanelBot);
				g.FillRectangle(VastiorTheme.Noise(), local);
				DrawVignette(g, local);
			}
		}

		// The corner blend for every custom button: blit the exact panel region
		// behind the child so chamfer corners match the gradient/grain/vignette.
		public void PaintChildBackdrop(Graphics g, Control child)
		{
			Rectangle dst = new Rectangle(0, 0, child.Width, child.Height);
			if (_backdrop == null)
			{
				using (SolidBrush b = new SolidBrush(VastiorTheme.PanelBot))
				{
					g.FillRectangle(b, dst);
				}
				return;
			}
			Rectangle src = new Rectangle(child.Left - _inner.X, child.Top - _inner.Y, child.Width, child.Height);
			if (src.X < 0 || src.Y < 0 || src.Right > _backdrop.Width || src.Bottom > _backdrop.Height)
			{
				using (SolidBrush b = new SolidBrush(VastiorTheme.PanelBot))
				{
					g.FillRectangle(b, dst);
				}
				return;
			}
			g.DrawImage(_backdrop, dst, src, GraphicsUnit.Pixel);
		}

		void BuildControls()
		{
			int bx = _inner.X + S(L.PadX);
			int bw = _inner.Width - 2 * S(L.PadX);

			// game folder path (read-only, copyable), vertically centred in the field
			_pathBox = new TextBox();
			_pathBox.ReadOnly = true;
			_pathBox.BorderStyle = BorderStyle.None;
			_pathBox.BackColor = VastiorTheme.FieldInk;
			_pathBox.ForeColor = VastiorTheme.TextBright;
			_pathBox.Font = VastiorTheme.MonoFont(11, FontStyle.Regular);
			int ph = _pathBox.PreferredHeight;
			_pathBox.SetBounds(_field.X + S(L.FieldTextInsetX), _field.Y + (_field.Height - ph) / 2, _field.Width - S(L.FieldTextInsetX) - S(4), ph);
			_pathBox.TextChanged += delegate { UpdateStatus(); };
			Controls.Add(_pathBox);

			_btnBrowse = MakeButton("Browse", VastiorTheme.ButtonStone, 11, false);
			_btnBrowse.SetBounds(_field.Right + S(L.BrowseGap), _field.Y, S(L.BrowseWidth), _field.Height);
			_btnBrowse.Click += delegate { OnBrowse(); };

			// console
			_log = new VastiorLogConsole();
			_log.SetBounds(_console.X, _console.Y, _console.Width, _console.Height);
			Controls.Add(_log);

			// action + utility button rows (equal heights per row)
			int gap = S(L.ButtonGap);
			int third = (bw - 2 * gap) / 3;
			int x0 = bx, x1 = bx + third + gap, x2 = bx + 2 * (third + gap);
			int lastW = bw - 2 * (third + gap);

			_btnInstall = MakeButton("Install", VastiorTheme.ButtonGreen, 12, true);
			_btnInstall.SetBounds(x0, _actionsY, third, S(L.ActionRowHeight));
			_btnRepair = MakeButton("Repair", VastiorTheme.ButtonBlue, 12, true);
			_btnRepair.SetBounds(x1, _actionsY, third, S(L.ActionRowHeight));
			_btnUninstall = MakeButton("Uninstall", VastiorTheme.ButtonRed, 12, true);
			_btnUninstall.SetBounds(x2, _actionsY, lastW, S(L.ActionRowHeight));
			_btnInstall.Click += delegate { Run(true, "INSTALL"); };
			_btnRepair.Click += delegate { Run(true, "REPAIR"); };
			_btnUninstall.Click += delegate { Run(false, "UNINSTALL"); };

			_btnRefresh = MakeButton("Refresh", VastiorTheme.ButtonStone, 11, true);
			_btnRefresh.SetBounds(x0, _utilY, third, S(L.UtilRowHeight));
			_btnOpen = MakeButton("Open Folder", VastiorTheme.ButtonStone, 11, true);
			_btnOpen.SetBounds(x1, _utilY, third, S(L.UtilRowHeight));
			_btnClose = MakeButton("Close", VastiorTheme.ButtonBronzeRed, 11, true);
			_btnClose.SetBounds(x2, _utilY, lastW, S(L.UtilRowHeight));
			_btnRefresh.Click += delegate { if (!_busy) { UpdateStatus(); AppendLog("[INFO] Status refreshed."); } };
			_btnOpen.Click += delegate { OnOpenFolder(); };
			_btnClose.Click += delegate { if (!_busy) { Close(); } };
		}

		VastiorButton MakeButton(string text, ButtonScheme scheme, float fontPx, bool bold)
		{
			VastiorButton b = new VastiorButton();
			b.Text = text;
			b.SetScheme(scheme);
			b.Font = VastiorTheme.HeadFont(fontPx, bold ? FontStyle.Bold : FontStyle.Regular);
			Controls.Add(b);
			return b;
		}

		// --------------------------------------------------------- painting ----
		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle full = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);

			VastiorTheme.FillRectV(g, full, VastiorTheme.FrameOuterTop, VastiorTheme.FrameOuterBottom);
			using (SolidBrush b = new SolidBrush(VastiorTheme.FrameBlack))
			{
				g.FillRectangle(b, Rectangle.Inflate(full, -S(L.FrameBlackInset), -S(L.FrameBlackInset)));
			}
			VastiorTheme.FillRectV(g, Rectangle.Inflate(full, -S(L.FrameInnerInset), -S(L.FrameInnerInset)), VastiorTheme.FrameInnerTop, VastiorTheme.FrameInnerBottom);

			if (_backdrop != null)
			{
				g.DrawImage(_backdrop, _inner.Location);
			}
			else
			{
				VastiorTheme.FillRectV(g, _inner, VastiorTheme.PanelTop, VastiorTheme.PanelBot);
			}

			DrawHeading(g);
			DrawFieldRow(g);
			DrawStatusRow(g);
			DrawConsoleFrame(g);
			DrawFooter(g);
		}

		void DrawHeading(Graphics g)
		{
			int bx = _inner.X + S(L.PadX);
			int y = _inner.Y + S(L.HeadingTop);
			float w;
			using (Font f = VastiorTheme.TitleFont(17, FontStyle.Bold))
			{
				DrawText(g, "Vastior", f, VastiorTheme.Brass, bx, y, true);
				w = g.MeasureString("Vastior", f).Width;
			}
			using (Font f = VastiorTheme.SerifFont(11, FontStyle.Italic))
			{
				DrawText(g, "unofficial fan mod for Grim Dawn", f, VastiorTheme.TextMuted, bx + (int)w + S(8), y + S(5), false);
			}
		}

		void DrawFieldRow(Graphics g)
		{
			int bx = _inner.X + S(L.PadX);
			using (Font f = VastiorTheme.HeadFont(12, FontStyle.Regular))
			{
				DrawLabel(g, "Game folder", f, VastiorTheme.TextBright, new Rectangle(bx, _field.Y, S(L.FieldLabelWidth), _field.Height));
			}
			VastiorTheme.FillRectV(g, _field, VastiorTheme.FieldTop, VastiorTheme.FieldBot);
			using (Pen p = new Pen(VastiorTheme.BrassDim))
			{
				g.DrawRectangle(p, _field.X, _field.Y, _field.Width - 1, _field.Height - 1);
			}
			VastiorTheme.FillRectV(g, new Rectangle(_field.X, _field.Y, S(L.FieldEdgeWidth), _field.Height), VastiorTheme.Brass, VastiorTheme.BrassDim);
		}

		void DrawStatusRow(Graphics g)
		{
			using (Font f = VastiorTheme.HeadFont(12, FontStyle.Regular))
			{
				DrawLabel(g, "Status", f, VastiorTheme.TextMuted, new Rectangle(_statusRow.X, _statusRow.Y, S(L.StatusLabelWidth), _statusRow.Height));
			}
			DrawBadge(g);
		}

		void DrawBadge(Graphics g)
		{
			Color dot = _statusKind == 1 ? VastiorTheme.StatusGood : (_statusKind == 2 ? VastiorTheme.StatusUnknown : VastiorTheme.StatusBad);
			string label = _statusKind == 1 ? "INSTALLED" : (_statusKind == 2 ? "UNKNOWN" : "NOT installed");

			VastiorTheme.FillRectV(g, _badge, VastiorTheme.C("#14100a"), VastiorTheme.C("#080604"));
			using (Pen p = new Pen(Color.FromArgb(130, dot)))
			{
				g.DrawRectangle(p, _badge.X, _badge.Y, _badge.Width - 1, _badge.Height - 1);
			}

			int dotSize = S(L.BadgeDot);
			int dotX = _badge.X + S(L.BadgePadX);
			int dotY = _badge.Y + (_badge.Height - dotSize) / 2; // vertically centred
			using (SolidBrush glow = new SolidBrush(Color.FromArgb(70, dot)))
			{
				g.FillEllipse(glow, dotX - 2, dotY - 2, dotSize + 4, dotSize + 4);
			}
			using (SolidBrush b = new SolidBrush(dot))
			{
				g.FillEllipse(b, dotX, dotY, dotSize, dotSize);
			}

			int textX = dotX + dotSize + S(L.BadgeDotTextGap);
			Rectangle textRect = new Rectangle(textX, _badge.Y, _badge.Right - textX - S(4), _badge.Height);
			using (Font f = VastiorTheme.MonoFont(11, FontStyle.Bold))
			{
				DrawLabel(g, label, f, dot, textRect); // text vertically centred
			}
		}

		void DrawConsoleFrame(Graphics g)
		{
			VastiorTheme.FillRectV(g, Rectangle.Inflate(_console, S(L.ConsoleFrame), S(L.ConsoleFrame)), VastiorTheme.ConsoleFrameTop, VastiorTheme.ConsoleFrameBottom);
			using (SolidBrush b = new SolidBrush(VastiorTheme.ConsoleBg))
			{
				g.FillRectangle(b, _console);
			}
		}

		void DrawFooter(Graphics g)
		{
			int bx = _inner.X + S(L.PadX);
			using (Font f = VastiorTheme.SerifFont(10.5f, FontStyle.Italic))
			using (StringFormat sf = new StringFormat())
			using (SolidBrush b = new SolidBrush(VastiorTheme.TextMuted))
			{
				g.TextRenderingHint = TextRenderingHint.AntiAlias;
				g.DrawString(FooterText, f, b, new RectangleF(bx, _footerY, _inner.Width - 2 * S(L.PadX), S(L.FooterHeight)), sf);
			}
		}

		// Draws text vertically centred (and left-aligned) inside a rectangle.
		void DrawLabel(Graphics g, string text, Font font, Color color, Rectangle rect)
		{
			using (StringFormat sf = new StringFormat())
			using (SolidBrush b = new SolidBrush(color))
			{
				sf.Alignment = StringAlignment.Near;
				sf.LineAlignment = StringAlignment.Center;
				sf.FormatFlags = StringFormatFlags.NoWrap;
				sf.Trimming = StringTrimming.EllipsisCharacter;
				g.TextRenderingHint = TextRenderingHint.AntiAlias;
				g.DrawString(text, font, b, rect, sf);
			}
		}

		void DrawText(Graphics g, string text, Font font, Color color, int x, int y, bool shadow)
		{
			g.TextRenderingHint = TextRenderingHint.AntiAlias;
			if (shadow)
			{
				using (SolidBrush sh = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
				{
					g.DrawString(text, font, sh, x, y + 1);
				}
			}
			using (SolidBrush b = new SolidBrush(color))
			{
				g.DrawString(text, font, b, x, y);
			}
		}

		void DrawVignette(Graphics g, Rectangle r)
		{
			using (GraphicsPath p = new GraphicsPath())
			{
				p.AddRectangle(r);
				using (PathGradientBrush pg = new PathGradientBrush(p))
				{
					pg.CenterColor = Color.Transparent;
					pg.SurroundColors = new[] { Color.FromArgb(150, 0, 0, 0) };
					pg.FocusScales = new PointF(0.78f, 0.7f);
					g.FillRectangle(pg, r);
				}
			}
		}

		// ---- drag-to-move on the header area --------------------------------
		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left && _dragArea.Contains(e.Location))
			{
				_dragging = true;
				_dragOffset = e.Location;
			}
			base.OnMouseDown(e);
		}
		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (_dragging)
			{
				Location = new Point(Location.X + e.X - _dragOffset.X, Location.Y + e.Y - _dragOffset.Y);
			}
			base.OnMouseMove(e);
		}
		protected override void OnMouseUp(MouseEventArgs e)
		{
			_dragging = false;
			base.OnMouseUp(e);
		}

		// ---------------------------------------------- read-only detection ----
		bool IsGrimDawn(string gd)
		{
			if (string.IsNullOrEmpty(gd) || !Directory.Exists(gd))
			{
				return false;
			}
			try
			{
				return File.Exists(Path.Combine(gd, "Grim Dawn.exe")) || File.Exists(Path.Combine(gd, "x64\\Grim Dawn.exe"));
			}
			catch
			{
				return false;
			}
		}
		bool IsInstalled(string gd)
		{
			try
			{
				if (File.Exists(Path.Combine(gd, "Vastior_install.txt")))
				{
					return true;
				}
				return File.Exists(Path.Combine(gd, "winmm_orig.dll")) && File.Exists(Path.Combine(gd, "Vastior.dll"));
			}
			catch
			{
				return false;
			}
		}
		void AutoDetect()
		{
			List<string> roots = new List<string>();
			roots.Add("C:\\Program Files (x86)\\Steam");
			roots.Add("C:\\Program Files\\Steam");
			try
			{
				RegistryKey k = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam");
				if (k != null)
				{
					string sp = k.GetValue("SteamPath") as string;
					if (!string.IsNullOrEmpty(sp))
					{
						sp = sp.Replace('/', '\\');
						roots.Add(sp);
						string vdf = Path.Combine(sp, "steamapps\\libraryfolders.vdf");
						if (File.Exists(vdf))
						{
							foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
							{
								roots.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
							}
						}
					}
				}
			}
			catch { }
			foreach (string r in roots)
			{
				string gd = Path.Combine(r, "steamapps\\common\\Grim Dawn");
				if (IsGrimDawn(gd))
				{
					_pathBox.Text = gd;
					return;
				}
			}
			if (string.IsNullOrEmpty(_pathBox.Text))
			{
				_pathBox.Text = PreferredPath;
			}
		}
		void UpdateStatus()
		{
			string gd = _pathBox.Text;
			if (!IsGrimDawn(gd))
			{
				_statusKind = 2;
				_btnInstall.Enabled = _btnRepair.Enabled = _btnUninstall.Enabled = false;
				Invalidate(_badge);
				return;
			}
			bool installed = IsInstalled(gd);
			_statusKind = installed ? 1 : 0;
			_btnInstall.Enabled = !_busy && !installed;
			_btnRepair.Enabled = !_busy && installed;
			_btnUninstall.Enabled = !_busy && installed;
			Invalidate(_badge);
		}

		// ---------------------------------------------------------- actions ----
		void OnBrowse()
		{
			if (_busy)
			{
				return;
			}
			string current = _pathBox.Text;
			string start = Directory.Exists(current) ? current : null;
			string picked = VastiorFolderPicker.PickFolder(Handle, FolderDialogTitle, start);
			if (picked == null)
			{
				return; // cancelled -> keep previous path
			}
			if (!IsGrimDawn(picked))
			{
				MessageBox.Show(this,
					"That folder does not look like a Grim Dawn install.\n\nExpected \"Grim Dawn.exe\" (or \"x64\\Grim Dawn.exe\") inside:\n" + picked,
					"Folder not recognised", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return; // keep previous path
			}
			_pathBox.Text = picked;
			UpdateStatus();
		}

		void OnOpenFolder()
		{
			string gd = _pathBox.Text;
			if (!Directory.Exists(gd))
			{
				return;
			}
			try { Process.Start("explorer.exe", gd); }
			catch { }
		}

		// ------------------------------------------------------------- log -----
		void ShowIntro()
		{
			_log.Clear();
			AppendLog("[INFO] " + AppName + " v" + AppVersion + " - a camera zoom mod for Grim Dawn.");
			AppendLog("[INFO] It only changes the in-game camera. It NEVER reads, writes, or edits");
			AppendLog("[INFO] your saves, characters, or shared stash.");
			AppendLog("[INFO]");
			AppendLog("[INFO] Install backs up your saves to the Desktop first, then copies the mod");
			AppendLog("[INFO] into the game folder. Uninstall removes only what it installed and");
			AppendLog("[INFO] restores anything it replaced - the game is left stock.");
			AppendLog("[INFO]");
			AppendLog("[INFO] Install and uninstall run inside this app - no scripts and no");
			AppendLog("[INFO] PowerShell, with nothing left running afterwards.");
			AppendLog("[INFO]");
		}
		void AppendLog(string line)
		{
			Color c = VastiorTheme.TextMuted;
			if (line.Contains("[OK]") || line.Contains("[ OK ]"))
			{
				c = VastiorTheme.StatusGood;
			}
			else if (line.Contains("[WARN]"))
			{
				c = VastiorTheme.StatusUnknown;
			}
			else if (line.Contains("[ERROR]") || line.Contains("[ERR"))
			{
				c = VastiorTheme.StatusBad;
			}
			else if (line.Contains("[STEP]"))
			{
				c = VastiorTheme.Accent;
			}
			_log.AppendLine(line, c);
		}
		void SetBusy(bool busy)
		{
			_busy = busy;
			_btnBrowse.Enabled = !busy;
			_btnRefresh.Enabled = !busy;
			_btnClose.Enabled = !busy;
			UpdateStatus();
		}

		// ---- run install / uninstall in-process (no scripts, no PowerShell) ----
		void Run(bool install, string opLabel)
		{
			if (_busy)
			{
				return;
			}
			string gd = _pathBox.Text;
			if (!IsGrimDawn(gd))
			{
				return;
			}
			string verb = !install ? "Remove Vastior from" : (opLabel == "REPAIR" ? "Repair Vastior in" : "Install Vastior into");
			MessageBoxIcon icon = !install ? MessageBoxIcon.Warning : MessageBoxIcon.Question;
			if (MessageBox.Show(this, verb + ":\n" + gd + "\n\nYour saves are never modified. Continue?", "Confirm " + opLabel.ToLowerInvariant(), MessageBoxButtons.YesNo, icon) != DialogResult.Yes)
			{
				return;
			}
			if (install && !Directory.Exists(Path.Combine(_appDir, "files")))
			{
				_log.Clear();
				AppendLog("[ERROR] The 'files' folder is missing next to Vastior.exe - cannot install.");
				return;
			}

			_log.Clear();
			AppendLog("[STEP] === " + opLabel + " ===");
			SetBusy(true);

			string appDir = _appDir;
			System.Threading.ThreadPool.QueueUserWorkItem(delegate
			{
				Action<string> log = delegate(string line)
				{
					try { BeginInvoke((MethodInvoker)delegate { AppendLog(line); }); }
					catch { }
				};
				bool ok = false;
				try
				{
					ok = install ? VastiorInstaller.Install(gd, appDir, log) : VastiorInstaller.Uninstall(gd, log);
				}
				catch (Exception ex)
				{
					try { BeginInvoke((MethodInvoker)delegate { AppendLog("[ERROR] " + ex.Message); }); }
					catch { }
				}
				bool done = ok;
				try
				{
					BeginInvoke((MethodInvoker)delegate
					{
						AppendLog(done ? "[OK] " + opLabel + " finished." : "[ERROR] " + opLabel + " did not complete.");
						SetBusy(false);
					});
				}
				catch { }
			});
		}
	}
}

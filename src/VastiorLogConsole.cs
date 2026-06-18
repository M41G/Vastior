// Vastior manager - console/log panel: a borderless RichTextBox paired with a
// themed VastiorScrollBar (no bright native scrollbar). Colours each line by its
// tag, auto-scrolls to the bottom, and streams stdout/stderr live without
// freezing the UI (appends arrive marshalled from the worker process).
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Vastior
{
	class VastiorLogConsole : Control
	{
		const int EM_GETLINECOUNT = 0x00BA;
		const int EM_GETFIRSTVISIBLELINE = 0x00CE;
		const int EM_LINESCROLL = 0x00B6;

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		readonly RichTextBox _text;
		readonly VastiorScrollBar _bar;

		public VastiorLogConsole()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
			BackColor = VastiorTheme.ConsoleBg;

			_text = new RichTextBox();
			_text.ReadOnly = true;
			_text.BorderStyle = BorderStyle.None;
			_text.BackColor = VastiorTheme.ConsoleBg;
			_text.ForeColor = VastiorTheme.TextMuted;
			_text.ScrollBars = RichTextBoxScrollBars.None;
			_text.DetectUrls = false;
			_text.WordWrap = true;
			_text.TabStop = false;
			_text.Font = VastiorTheme.MonoFont(10.5f, FontStyle.Regular);
			_text.MouseWheel += OnWheel;
			Controls.Add(_text);

			_bar = new VastiorScrollBar();
			_bar.UserScrolled += OnBarScrolled;
			_bar.MouseWheel += OnWheel;
			Controls.Add(_bar);

			MouseWheel += OnWheel;
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			base.OnSizeChanged(e);
			LayoutChildren();
		}
		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			LayoutChildren();
		}

		void LayoutChildren()
		{
			int sb = VastiorTheme.S(VastiorLayout.ScrollBarWidth);
			int pad = VastiorTheme.S(2);
			_bar.SetBounds(Width - sb, 0, sb, Height);
			_text.SetBounds(pad, pad, Math.Max(0, Width - sb - pad * 2), Math.Max(0, Height - pad * 2));
			SyncBar();
		}

		// --- line metrics ------------------------------------------------------
		int LineHeight()
		{
			return Math.Max(1, _text.Font.Height);
		}
		int VisibleLines()
		{
			return Math.Max(1, _text.ClientSize.Height / LineHeight());
		}
		int TotalLines()
		{
			return SendMessage(_text.Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero);
		}
		int FirstVisibleLine()
		{
			return SendMessage(_text.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
		}
		void LineScrollBy(int delta)
		{
			if (delta != 0)
			{
				SendMessage(_text.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)delta);
			}
		}
		void SyncBar()
		{
			if (!_text.IsHandleCreated)
			{
				return;
			}
			_bar.SetMetrics(TotalLines(), VisibleLines(), FirstVisibleLine());
		}

		// --- input -------------------------------------------------------------
		void OnBarScrolled(object sender, EventArgs e)
		{
			if (!_text.IsHandleCreated)
			{
				return;
			}
			LineScrollBy(_bar.Value - FirstVisibleLine());
			_bar.SetMetrics(TotalLines(), VisibleLines(), FirstVisibleLine());
		}
		void OnWheel(object sender, MouseEventArgs e)
		{
			LineScrollBy(VastiorLayout.ScrollWheelLines * (e.Delta > 0 ? -1 : 1));
			SyncBar();
			HandledMouseEventArgs handled = e as HandledMouseEventArgs;
			if (handled != null)
			{
				handled.Handled = true;
			}
		}

		// --- public API --------------------------------------------------------
		public void Clear()
		{
			_text.Clear();
			SyncBar();
		}
		public void AppendLine(string line, Color color)
		{
			_text.SelectionStart = _text.TextLength;
			_text.SelectionLength = 0;
			_text.SelectionColor = color;
			_text.AppendText(line + Environment.NewLine);
			_text.SelectionStart = _text.TextLength;
			_text.ScrollToCaret();
			SyncBar();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			using (SolidBrush b = new SolidBrush(VastiorTheme.ConsoleBg))
			{
				e.Graphics.FillRectangle(b, ClientRectangle);
			}
		}
	}
}

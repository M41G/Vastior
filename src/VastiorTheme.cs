// Vastior manager - centralised theme: palette, fonts, DPI scaling, and the
// shared drawing helpers (chamfer paths, gradient fills, grain texture).
// All colours live here so the look can be retuned in one place.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Vastior
{
	// Colour set for one chamfered button state group.
	struct ButtonScheme
	{
		public Color FrameTop, FrameBottom, FaceTop, FaceMid, FaceBottom, Text, Glow;
	}

	static class VastiorTheme
	{
		// --- DPI scaling -------------------------------------------------------
		public static float Scale = 1f;
		public static int S(int value)
		{
			return (int)Math.Round(value * Scale);
		}

		// --- frame / panel -----------------------------------------------------
		public static readonly Color FrameOuterTop = C("#b89a55"), FrameOuterBottom = C("#332714");
		public static readonly Color FrameBlack = C("#050403");
		public static readonly Color FrameInnerTop = C("#d8b96a"), FrameInnerBottom = C("#3a2c16");
		public static readonly Color PanelTop = Color.FromArgb(20, 15, 9), PanelBot = Color.FromArgb(7, 5, 3);

		// --- text / accents ----------------------------------------------------
		public static readonly Color Brass = C("#c2a15a"), BrassDim = C("#70562c");
		public static readonly Color TextBright = C("#f1dfb0"), TextMuted = C("#bfa875");

		// --- game folder field -------------------------------------------------
		public static readonly Color FieldTop = C("#100c07"), FieldBot = C("#050403");
		public static readonly Color FieldInk = C("#0b0805");

		// --- console -----------------------------------------------------------
		public static readonly Color ConsoleBg = C("#080604");
		public static readonly Color ConsoleFrameTop = C("#7a6233"), ConsoleFrameBottom = C("#3a2c16");

		// --- status / log tag colours -----------------------------------------
		public static readonly Color StatusGood = C("#20d34f");
		public static readonly Color StatusBad = C("#d8633a");
		public static readonly Color StatusUnknown = C("#d8a23e");
		public static readonly Color Accent = C("#8fb0d8");

		// --- themed scrollbar --------------------------------------------------
		public static readonly Color ScrollTrack = C("#0c0a06");
		public static readonly Color ScrollThumbTop = C("#7a6233"), ScrollThumbBottom = C("#3f3016");
		public static readonly Color ScrollThumbHot = C("#a98a45");
		public static readonly Color ScrollBorder = C("#4a3a1d");

		// --- button schemes (all button colours centralised here) -------------
		public static readonly ButtonScheme ButtonGreen = Scheme("#5fa05a", "#16310f", "#2f5f2c", "#143d12", "#08220a", "#d0f5cc", Color.FromArgb(140, 32, 211, 79));
		public static readonly ButtonScheme ButtonBlue = Scheme("#4a82b8", "#102440", "#1e4872", "#0e2840", "#071628", "#c8e4f8", Color.FromArgb(140, 74, 130, 184));
		public static readonly ButtonScheme ButtonRed = Scheme("#a8543c", "#2a120b", "#5a2918", "#2e120a", "#160803", "#f6d8cd", Color.FromArgb(120, 216, 99, 58));
		public static readonly ButtonScheme ButtonBronzeRed = Scheme("#c18445", "#3d1b10", "#4a2416", "#25110b", "#0b0503", "#f1dfb0", Color.FromArgb(110, 176, 106, 50));
		public static readonly ButtonScheme ButtonStone = Scheme("#d8b96a", "#3a2c16", "#2c2619", "#100c07", "#070503", "#f1dfb0", Color.Transparent);
		public static readonly ButtonScheme ButtonDead = Scheme("#3a3228", "#141009", "#181410", "#0a0806", "#060403", "#6a5d49", Color.Transparent);

		static ButtonScheme Scheme(string ft, string fb, string a, string b, string c, string t, Color glow)
		{
			ButtonScheme s;
			s.FrameTop = C(ft); s.FrameBottom = C(fb);
			s.FaceTop = C(a); s.FaceMid = C(b); s.FaceBottom = C(c);
			s.Text = C(t); s.Glow = glow;
			return s;
		}

		// --- fonts (graceful fallback; no online or bundled fonts) ------------
		static List<string> _installed;
		static bool Has(string name)
		{
			if (_installed == null)
			{
				_installed = new List<string>();
				foreach (FontFamily f in new InstalledFontCollection().Families)
				{
					_installed.Add(f.Name.ToLowerInvariant());
				}
			}
			return _installed.Contains(name.ToLowerInvariant());
		}
		static FontFamily Family(string[] prefs)
		{
			foreach (string p in prefs)
			{
				if (Has(p))
				{
					try { return new FontFamily(p); }
					catch { }
				}
			}
			return FontFamily.GenericSerif;
		}
		static Font Build(FontFamily fam, float px, FontStyle style)
		{
			try { return new Font(fam, px, style, GraphicsUnit.Pixel); }
			catch
			{
				try { return new Font(fam, px, FontStyle.Regular, GraphicsUnit.Pixel); }
				catch { return new Font(FontFamily.GenericSerif, px, FontStyle.Regular, GraphicsUnit.Pixel); }
			}
		}
		public static Font TitleFont(float px, FontStyle st) { return Build(Family(new[] { "Cinzel Decorative", "Cinzel", "Georgia", "Times New Roman" }), px * Scale, st); }
		public static Font HeadFont(float px, FontStyle st) { return Build(Family(new[] { "Cinzel", "Georgia", "Times New Roman" }), px * Scale, st); }
		public static Font SerifFont(float px, FontStyle st) { return Build(Family(new[] { "Georgia", "Times New Roman" }), px * Scale, st); }
		public static Font MonoFont(float px, FontStyle st) { return Build(Family(new[] { "JetBrains Mono", "Consolas", "Courier New" }), px * Scale, st); }

		// --- colour helpers ----------------------------------------------------
		public static Color C(string hex)
		{
			hex = hex.TrimStart('#');
			return Color.FromArgb(
				Convert.ToInt32(hex.Substring(0, 2), 16),
				Convert.ToInt32(hex.Substring(2, 2), 16),
				Convert.ToInt32(hex.Substring(4, 2), 16));
		}
		public static Color Lighten(Color c, float amount)
		{
			return Color.FromArgb(c.A,
				(int)(c.R + (255 - c.R) * amount),
				(int)(c.G + (255 - c.G) * amount),
				(int)(c.B + (255 - c.B) * amount));
		}

		// --- shape / fill helpers ---------------------------------------------
		public static GraphicsPath Chamfer(Rectangle r, int cut)
		{
			GraphicsPath p = new GraphicsPath();
			int x = r.X, y = r.Y, w = r.Width, h = r.Height;
			p.AddLine(x + cut, y, x + w - cut, y);
			p.AddLine(x + w - cut, y, x + w, y + cut);
			p.AddLine(x + w, y + cut, x + w, y + h - cut);
			p.AddLine(x + w, y + h - cut, x + w - cut, y + h);
			p.AddLine(x + w - cut, y + h, x + cut, y + h);
			p.AddLine(x + cut, y + h, x, y + h - cut);
			p.AddLine(x, y + h - cut, x, y + cut);
			p.CloseFigure();
			return p;
		}
		public static void FillPathV(Graphics g, GraphicsPath path, Rectangle r, Color top, Color bottom)
		{
			using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(r.X, r.Y - 1, Math.Max(1, r.Width), r.Height + 2), top, bottom, 90f))
			{
				g.FillPath(b, path);
			}
		}
		public static void FillRectV(Graphics g, Rectangle r, Color top, Color bottom)
		{
			using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(r.X, r.Y - 1, Math.Max(1, r.Width), r.Height + 2), top, bottom, 90f))
			{
				g.FillRectangle(b, r);
			}
		}

		// Subtle grain so flat fills do not look plasticky. Generated once, tiled.
		static TextureBrush _noise;
		public static TextureBrush Noise()
		{
			if (_noise != null)
			{
				return _noise;
			}
			Bitmap b = new Bitmap(96, 96, PixelFormat.Format32bppArgb);
			Random rnd = new Random(7);
			for (int y = 0; y < 96; y++)
			{
				for (int x = 0; x < 96; x++)
				{
					int a = rnd.Next(0, 14);
					int v = rnd.Next(40, 230);
					b.SetPixel(x, y, Color.FromArgb(a, v, v, v));
				}
			}
			_noise = new TextureBrush(b);
			_noise.WrapMode = WrapMode.Tile;
			return _noise;
		}
	}
}

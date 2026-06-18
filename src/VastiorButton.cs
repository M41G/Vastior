// Vastior manager - chamfered (bevel-cut) action button, owner-drawn.
//
// Corner-artifact fix: the cut corners are NOT part of the button face, so they
// must show whatever is painted behind the button. The control therefore asks
// its parent (an IBackdropProvider) to paint the real panel background into the
// full bounds first; the chamfer is then drawn anti-aliased on top. No flat
// fill, no fake transparency - the corners blend perfectly into the gradient.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Vastior
{
	// Implemented by the form so children can sample the exact panel background.
	interface IBackdropProvider
	{
		void PaintChildBackdrop(Graphics g, Control child);
	}

	class VastiorButton : Control
	{
		ButtonScheme _scheme;
		bool _hover, _pressed;

		public VastiorButton()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
			BackColor = VastiorTheme.PanelBot;
			Cursor = Cursors.Hand;
			_scheme = VastiorTheme.ButtonStone;
		}

		public void SetScheme(ButtonScheme scheme)
		{
			_scheme = scheme;
			Invalidate();
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			_hover = true;
			Invalidate();
			base.OnMouseEnter(e);
		}
		protected override void OnMouseLeave(EventArgs e)
		{
			_hover = false;
			_pressed = false;
			Invalidate();
			base.OnMouseLeave(e);
		}
		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				_pressed = true;
				Invalidate();
			}
			base.OnMouseDown(e);
		}
		protected override void OnMouseUp(MouseEventArgs e)
		{
			_pressed = false;
			Invalidate();
			base.OnMouseUp(e);
		}
		protected override void OnEnabledChanged(EventArgs e)
		{
			Invalidate();
			base.OnEnabledChanged(e);
		}

		void PaintBackdrop(Graphics g)
		{
			IBackdropProvider provider = FindForm() as IBackdropProvider;
			if (provider != null)
			{
				provider.PaintChildBackdrop(g, this);
				return;
			}
			using (SolidBrush b = new SolidBrush(BackColor))
			{
				g.FillRectangle(b, ClientRectangle);
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;

			// 1. real parent background -> chamfer corners blend cleanly
			PaintBackdrop(g);

			bool enabled = Enabled;
			ButtonScheme s = enabled ? _scheme : VastiorTheme.ButtonDead;
			Color frameTop = s.FrameTop, frameBottom = s.FrameBottom;
			Color faceTop = s.FaceTop, faceMid = s.FaceMid, faceBottom = s.FaceBottom;
			if (enabled && _hover)
			{
				faceTop = VastiorTheme.Lighten(faceTop, 0.12f);
				faceMid = VastiorTheme.Lighten(faceMid, 0.12f);
				faceBottom = VastiorTheme.Lighten(faceBottom, 0.12f);
				frameTop = VastiorTheme.Lighten(frameTop, 0.1f);
			}

			int cut = VastiorTheme.S(VastiorLayout.ChamferCut);
			Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

			// 2. brass frame
			using (GraphicsPath outer = VastiorTheme.Chamfer(r, cut + VastiorTheme.S(1)))
			{
				VastiorTheme.FillPathV(g, outer, r, frameTop, frameBottom);
			}

			// 3. recessed face (3-stop gradient + grain + top highlight)
			Rectangle ir = Rectangle.Inflate(r, -VastiorTheme.S(2), -VastiorTheme.S(2));
			using (GraphicsPath inner = VastiorTheme.Chamfer(ir, cut))
			{
				Rectangle gr = new Rectangle(ir.X, ir.Y - 1, Math.Max(1, ir.Width), ir.Height + 2);
				using (LinearGradientBrush br = new LinearGradientBrush(gr, faceTop, faceBottom, 90f))
				{
					ColorBlend blend = new ColorBlend(3);
					blend.Colors = new[] { faceTop, faceMid, faceBottom };
					blend.Positions = new[] { 0f, 0.55f, 1f };
					br.InterpolationColors = blend;
					g.FillPath(br, inner);
				}
				g.SetClip(inner);
				g.FillRectangle(VastiorTheme.Noise(), ir);
				using (Pen hi = new Pen(Color.FromArgb(enabled ? 46 : 14, 255, 235, 180)))
				{
					g.DrawLine(hi, ir.X + cut, ir.Y + 1, ir.Right - cut, ir.Y + 1);
				}
				if (_pressed)
				{
					using (SolidBrush sb = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
					{
						g.FillRectangle(sb, ir);
					}
				}
				g.ResetClip();
			}

			// 4. label (shadow + optional glow + face colour)
			DrawLabel(g, s.Text, s.Glow, enabled);
		}

		void DrawLabel(Graphics g, Color textColor, Color glow, bool enabled)
		{
			using (StringFormat sf = new StringFormat())
			{
				sf.Alignment = StringAlignment.Center;
				sf.LineAlignment = StringAlignment.Center;
				g.TextRenderingHint = TextRenderingHint.AntiAlias;
				RectangleF tr = new RectangleF(0, _pressed ? 1 : 0, Width, Height);
				using (SolidBrush shadow = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
				{
					g.DrawString(Text, Font, shadow, new RectangleF(tr.X, tr.Y + 1, tr.Width, tr.Height), sf);
				}
				if (enabled && glow.A > 0)
				{
					using (SolidBrush gb = new SolidBrush(Color.FromArgb(glow.A / 2, glow)))
					{
						g.DrawString(Text, Font, gb, tr, sf);
					}
				}
				using (SolidBrush fb = new SolidBrush(textColor))
				{
					g.DrawString(Text, Font, fb, tr, sf);
				}
			}
		}
	}
}

// Vastior manager - themed vertical scrollbar (dark track, brass thumb).
// Replaces the bright native scrollbar inside the console. The model is line
// based: content = total lines, viewport = visible lines, value = first visible
// line. SetMetrics() is called by the console when text/size changes (silent);
// user drags/paging raise UserScrolled so the console can scroll its text.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Vastior
{
	class VastiorScrollBar : Control
	{
		int _content, _viewport, _value;
		bool _dragging, _hoverThumb;
		int _dragGrab;

		public event EventHandler UserScrolled;

		public VastiorScrollBar()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
			BackColor = VastiorTheme.ScrollTrack;
			_viewport = 1;
		}

		public int Value
		{
			get { return _value; }
		}

		// Update range/position without raising UserScrolled (avoids feedback loops).
		public void SetMetrics(int content, int viewport, int value)
		{
			_content = Math.Max(0, content);
			_viewport = Math.Max(1, viewport);
			_value = Clamp(value, 0, MaxValue());
			Invalidate();
		}

		bool Scrollable()
		{
			return _content > _viewport && _viewport > 0;
		}
		int MaxValue()
		{
			return Math.Max(0, _content - _viewport);
		}
		static int Clamp(int v, int lo, int hi)
		{
			if (v < lo)
			{
				return lo;
			}
			if (v > hi)
			{
				return hi;
			}
			return v;
		}

		int TrackMargin()
		{
			return VastiorTheme.S(2);
		}
		int TrackLength()
		{
			return Math.Max(1, Height - 2 * TrackMargin());
		}
		int ThumbLength()
		{
			if (!Scrollable())
			{
				return TrackLength();
			}
			int len = (int)((float)TrackLength() * _viewport / _content);
			return Clamp(len, VastiorTheme.S(VastiorLayout.ScrollThumbMin), TrackLength());
		}
		int ThumbTop()
		{
			if (!Scrollable())
			{
				return TrackMargin();
			}
			int travel = TrackLength() - ThumbLength();
			int max = MaxValue();
			if (max <= 0)
			{
				return TrackMargin();
			}
			return TrackMargin() + (int)((float)travel * _value / max);
		}
		Rectangle ThumbRect()
		{
			return new Rectangle(VastiorTheme.S(2), ThumbTop(), Math.Max(1, Width - VastiorTheme.S(4)), ThumbLength());
		}

		void SetValueFromUser(int value)
		{
			int clamped = Clamp(value, 0, MaxValue());
			bool changed = clamped != _value;
			_value = clamped;
			Invalidate();
			if (changed && UserScrolled != null)
			{
				UserScrolled(this, EventArgs.Empty);
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left && Scrollable())
			{
				Rectangle thumb = ThumbRect();
				if (thumb.Contains(e.Location))
				{
					_dragging = true;
					_dragGrab = e.Y - thumb.Y;
				}
				else
				{
					int page = _viewport > 1 ? _viewport - 1 : 1;
					SetValueFromUser(e.Y < thumb.Y ? _value - page : _value + page);
				}
			}
			base.OnMouseDown(e);
		}
		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (_dragging)
			{
				int travel = TrackLength() - ThumbLength();
				if (travel > 0)
				{
					int top = e.Y - _dragGrab - TrackMargin();
					SetValueFromUser((int)Math.Round((double)top * MaxValue() / travel));
				}
			}
			else
			{
				bool hot = Scrollable() && ThumbRect().Contains(e.Location);
				if (hot != _hoverThumb)
				{
					_hoverThumb = hot;
					Invalidate();
				}
			}
			base.OnMouseMove(e);
		}
		protected override void OnMouseUp(MouseEventArgs e)
		{
			_dragging = false;
			base.OnMouseUp(e);
		}
		protected override void OnMouseLeave(EventArgs e)
		{
			_hoverThumb = false;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;

			using (SolidBrush track = new SolidBrush(VastiorTheme.ScrollTrack))
			{
				g.FillRectangle(track, ClientRectangle);
			}
			using (Pen seam = new Pen(VastiorTheme.ScrollBorder))
			{
				g.DrawLine(seam, 0, 0, 0, Height - 1);
			}

			Rectangle thumb = ThumbRect();
			Color top = (_hoverThumb || _dragging) ? VastiorTheme.ScrollThumbHot : VastiorTheme.ScrollThumbTop;
			int alpha = Scrollable() ? 255 : 90; // inert (dimmed) when nothing to scroll
			using (GraphicsPath path = Rounded(thumb, VastiorTheme.S(3)))
			{
				Rectangle gr = new Rectangle(thumb.X, thumb.Y - 1, Math.Max(1, thumb.Width), thumb.Height + 2);
				using (LinearGradientBrush b = new LinearGradientBrush(gr, Color.FromArgb(alpha, top), Color.FromArgb(alpha, VastiorTheme.ScrollThumbBottom), 90f))
				{
					g.FillPath(b, path);
				}
				using (Pen border = new Pen(Color.FromArgb(alpha, VastiorTheme.ScrollBorder)))
				{
					g.DrawPath(border, path);
				}
			}
		}

		static GraphicsPath Rounded(Rectangle r, int radius)
		{
			int d = Math.Max(1, radius * 2);
			GraphicsPath p = new GraphicsPath();
			if (r.Width <= d || r.Height <= d)
			{
				p.AddRectangle(r);
				return p;
			}
			p.AddArc(r.X, r.Y, d, d, 180, 90);
			p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
			p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
			p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
			p.CloseFigure();
			return p;
		}
	}
}

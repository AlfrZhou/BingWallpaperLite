using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    internal static class Style
    {
        internal static readonly Color Ink = Color.FromArgb(27, 47, 43);
        internal static readonly Color Muted = Color.FromArgb(108, 125, 119);
        internal static readonly Color Green = Color.FromArgb(15, 119, 96);
        internal static readonly Color Background = Color.FromArgb(244, 247, 246);
        internal static readonly Color Line = Color.FromArgb(225, 233, 229);
        internal static string Family = "Microsoft YaHei UI";
        internal static Font Font(float size, bool bold) { return new Font(Family, size, bold ? FontStyle.Bold : FontStyle.Regular); }
        internal static GraphicsPath Round(RectangleF rect, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }
        internal static Label Label(string text, float size, Color color, bool bold)
        { return new Label { Text = text, Font = Font(size, bold), ForeColor = color, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft }; }
        internal static void PaintBackground(Control control, Graphics graphics)
        {
            Color color = control.Parent == null ? control.BackColor : control.Parent.BackColor;
            if (color.A != 255) color = Background;
            // Paint even the corners and empty text area: a double buffer can contain another control's old pixels.
            using (var fill = new SolidBrush(color)) graphics.FillRectangle(fill, control.ClientRectangle);
        }
        internal static void PaintText(Graphics graphics, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        {
            // Keep text in the same GDI+ pipeline as shapes: GDI HDC text can lose the paint clip/origin.
            var state = graphics.Save();
            try
            {
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (var format = new StringFormat(StringFormat.GenericTypographic))
                using (var fill = new SolidBrush(color))
                {
                    format.FormatFlags = StringFormatFlags.NoWrap;
                    format.HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None;
                    format.Trimming = (flags & TextFormatFlags.EndEllipsis) != 0 ? StringTrimming.EllipsisCharacter : StringTrimming.None;
                    format.Alignment = (flags & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center : (flags & TextFormatFlags.Right) != 0 ? StringAlignment.Far : StringAlignment.Near;
                    format.LineAlignment = (flags & TextFormatFlags.VerticalCenter) != 0 ? StringAlignment.Center : (flags & TextFormatFlags.Bottom) != 0 ? StringAlignment.Far : StringAlignment.Near;
                    graphics.DrawString(text ?? "", font, fill, bounds, format);
                }
            }
            finally { graphics.Restore(state); }
        }
        internal static void PaintFocus(Graphics graphics, Rectangle bounds)
        {
            if (bounds.Width < 2 || bounds.Height < 2) return;
            using (var pen = new Pen(Muted) { DashStyle = DashStyle.Dot }) graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }
    }
    internal sealed class RoundedCard : Panel
    {
        public RoundedCard() { DoubleBuffered = true; BackColor = Style.Background; Padding = new Padding(20); SetStyle(ControlStyles.Opaque | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Style.PaintBackground(this, e.Graphics);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Style.Round(new RectangleF(.5F, .5F, Width - 1, Height - 1), 12))
            using (var fill = new SolidBrush(Color.White))
            using (var border = new Pen(Style.Line)) { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path); }
            base.OnPaint(e);
        }
    }
    internal sealed class ActionButton : Button
    {
        private bool hover;
        internal bool Primary;
        public ActionButton(string text, bool primary)
        {
            Text = text; Primary = primary; Font = Style.Font(9.5F, true); Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            Size = new Size(150, 40); UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Style.PaintBackground(this, e.Graphics);
            var color = !Enabled ? Color.FromArgb(221, 229, 225) : Primary ? (hover ? Color.FromArgb(9, 98, 79) : Style.Green) : (hover ? Color.FromArgb(237, 247, 242) : Color.White);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Style.Round(new RectangleF(.5F, .5F, Width - 1, Height - 1), 7))
            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(Primary ? color : Style.Line)) { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
            Style.PaintText(e.Graphics, Text, Font, ClientRectangle, Enabled ? (Primary ? Color.White : Style.Green) : Style.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) Style.PaintFocus(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
        }
    }
    internal sealed class Toggle : CheckBox
    {
        public Toggle() { AutoSize = false; Size = new Size(44, 25); Cursor = Cursors.Hand; AccessibleName = "每天自动更新"; UseVisualStyleBackColor = false; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Style.PaintBackground(this, e.Graphics);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Style.Round(new RectangleF(1, 2, Width - 2, Height - 4), (Height - 4) / 2F))
            using (var fill = new SolidBrush(Checked ? Style.Green : Color.FromArgb(201, 212, 206))) e.Graphics.FillPath(fill, path);
            float d = Height - 10;
            using (var fill = new SolidBrush(Color.White)) e.Graphics.FillEllipse(fill, Checked ? Width - d - 5 : 5, 5, d, d);
            if (Focused && ShowFocusCues) Style.PaintFocus(e.Graphics, Rectangle.Inflate(ClientRectangle, -2, -2));
        }
    }
    internal sealed class StyledCheck : CheckBox
    {
        public StyledCheck() { Cursor = Cursors.Hand; UseVisualStyleBackColor = false; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Style.PaintBackground(this, e.Graphics);
            float scale = e.Graphics.DpiX / 96F, side = 13 * scale, top = (Height - side) / 2;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Style.Round(new RectangleF(.5F, top, side, side), 3 * scale))
            using (var fill = new SolidBrush(Checked ? Style.Green : Color.White))
            using (var pen = new Pen(Checked ? Style.Green : Color.FromArgb(179, 195, 186)))
            { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path); }
            if (Checked) using (var pen = new Pen(Color.White, 1.5F * scale))
                e.Graphics.DrawLines(pen, new[] { new PointF(3 * scale, top + 6.5F * scale), new PointF(5.5F * scale, top + 9 * scale), new PointF(10.5F * scale, top + 4 * scale) });
            Style.PaintText(e.Graphics, Text, Font, new Rectangle((int)(21 * scale), 0, Width - (int)(21 * scale), Height), Enabled ? Style.Ink : Style.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }
    internal sealed class Badge : Control
    {
        internal bool Busy, Paused;
        internal void SetState(string text, bool busy, bool paused)
        {
            if (Text == text && Busy == busy && Paused == paused) return;
            Busy = busy; Paused = paused; Text = text; Invalidate();
        }
        public Badge() { DoubleBuffered = true; TabStop = false; Font = Style.Font(8.5F, true); Size = new Size(142, 29); SetStyle(ControlStyles.Opaque | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Style.PaintBackground(this, e.Graphics);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color text = Busy ? Color.FromArgb(155, 99, 18) : Paused ? Style.Muted : Style.Green;
            Color bg = Busy ? Color.FromArgb(253, 243, 220) : Paused ? Color.FromArgb(231, 237, 234) : Color.FromArgb(226, 244, 234);
            using (var path = Style.Round(new RectangleF(0, 0, Width - 1, Height - 1), 14))
            using (var brush = new SolidBrush(bg)) e.Graphics.FillPath(brush, path);
            using (var brush = new SolidBrush(text)) e.Graphics.FillEllipse(brush, 12, Height / 2 - 3, 6, 6);
            Style.PaintText(e.Graphics, Text, Font, new Rectangle(25, 0, Width - 29, Height), text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
    internal sealed class CompactSelect : Control
    {
        internal readonly List<string> Items = new List<string>();
        private int selected = -1;
        private bool hovered;
        private string search = "";
        private DateTime searchAt;
        private ToolStripDropDown popup;
        internal event EventHandler SelectedIndexChanged;
        internal object SelectedItem
        {
            get { return selected >= 0 && selected < Items.Count ? Items[selected] : null; }
            set { int index = value == null ? -1 : Items.IndexOf(value.ToString()); if (index >= 0) SelectedIndex = index; }
        }
        internal int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value < -1 || value >= Items.Count) throw new ArgumentOutOfRangeException("value");
                if (selected == value) return; selected = value; Invalidate();
                var changed = SelectedIndexChanged; if (changed != null) changed(this, EventArgs.Empty);
            }
        }
        public CompactSelect()
        {
            DoubleBuffered = true; TabStop = true; Cursor = Cursors.Hand; Font = Style.Font(9.5F, false); Size = new Size(140, 32);
            AccessibleRole = AccessibleRole.ComboBox; SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Style.PaintBackground(this, e.Graphics);
            float scale = e.Graphics.DpiX / 96F; e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color border = Focused || popup != null && popup.Visible ? Style.Green : hovered ? Color.FromArgb(164, 188, 176) : Style.Line;
            using (var path = Style.Round(new RectangleF(.5F, .5F, Width - 1, Height - 1), 5 * scale))
            using (var fill = new SolidBrush(Enabled ? Color.White : Color.FromArgb(244, 247, 245)))
            using (var pen = new Pen(border)) { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path); }
            int left = (int)(9 * scale), arrow = (int)(26 * scale);
            Style.PaintText(e.Graphics, SelectedItem == null ? "" : SelectedItem.ToString(), Font,
                new Rectangle(left, 0, Math.Max(1, Width - left - arrow), Height), Enabled ? Style.Ink : Style.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            float x = Width - 15 * scale, y = Height / 2F;
            using (var pen = new Pen(Style.Muted, 1.4F * scale))
                e.Graphics.DrawLines(pen, new[] { new PointF(x - 3 * scale, y - 1.5F * scale), new PointF(x, y + 1.5F * scale), new PointF(x + 3 * scale, y - 1.5F * scale) });
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { if (!Enabled && popup != null) popup.Close(); Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnVisibleChanged(EventArgs e) { if (!Visible && popup != null) popup.Close(); base.OnVisibleChanged(e); }
        protected override void OnClick(EventArgs e) { Focus(); Open(); base.OnClick(e); }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F4 || e.KeyCode == Keys.Space || e.Alt && e.KeyCode == Keys.Down) { Open(); e.Handled = true; }
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                if (Items.Count != 0) SelectedIndex = Math.Max(0, Math.Min(Items.Count - 1, selected + (e.KeyCode == Keys.Down ? 1 : -1))); e.Handled = true;
            }
            base.OnKeyDown(e);
        }
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar))
            {
                if (DateTime.Now - searchAt > TimeSpan.FromSeconds(1.5)) search = "";
                search += e.KeyChar; searchAt = DateTime.Now;
                int match = Items.FindIndex(item => item.StartsWith(search, StringComparison.CurrentCultureIgnoreCase));
                if (match >= 0) SelectedIndex = match; e.Handled = true;
            }
            base.OnKeyPress(e);
        }
        private void Open()
        {
            if (!Enabled || IsDisposed || Disposing || Items.Count == 0) return;
            var dropdown = GetPopup();
            if (dropdown.Visible) return;
            var list = (ListBox)((ToolStripControlHost)dropdown.Items[0]).Control;
            list.SelectedIndex = selected;
            if (selected >= 0) list.TopIndex = Math.Max(0, selected - 3);
            dropdown.Show(this, new Point(0, Height + 3));
            if (!dropdown.IsDisposed && dropdown.Visible && !list.IsDisposed) list.Focus();
            if (!IsDisposed) Invalidate();
        }
        private ToolStripDropDown GetPopup()
        {
            if (popup != null) return popup;
            int row = Math.Max(24, Font.Height + 9);
            var list = new ListBox { BorderStyle = BorderStyle.None, Font = Font, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = row, Size = new Size(Width - 2, Math.Min(9, Items.Count) * row), BackColor = Color.White, AccessibleName = AccessibleName + "选项" };
            list.Items.AddRange(Items.ToArray()); list.SelectedIndex = selected;
            if (selected >= 0) list.TopIndex = Math.Max(0, selected - 3);
            list.DrawItem += delegate(object sender, DrawItemEventArgs args)
            {
                if (args.Index < 0) return; bool active = (args.State & DrawItemState.Selected) != 0;
                using (var fill = new SolidBrush(active ? Color.FromArgb(228, 244, 235) : Color.White)) args.Graphics.FillRectangle(fill, args.Bounds);
                Style.PaintText(args.Graphics, Items[args.Index], Font, new Rectangle(args.Bounds.X + 9, args.Bounds.Y, args.Bounds.Width - 15, args.Bounds.Height),
                    active ? Style.Green : Style.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            var host = new ToolStripControlHost(list) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = list.Size };
            var dropdown = new ToolStripDropDown { Padding = new Padding(1), BackColor = Style.Line, AutoClose = true };
            dropdown.Items.Add(host); popup = dropdown;
            Action commit = delegate
            {
                // Finish WinForms' close sequence before notifying code that may close the settings window.
                int index = list.SelectedIndex; dropdown.Close(ToolStripDropDownCloseReason.ItemClicked);
                if (!IsDisposed && !Disposing && index >= 0 && index < Items.Count) SelectedIndex = index;
            };
            list.MouseMove += delegate(object sender, MouseEventArgs args) { int i = list.IndexFromPoint(args.Location); if (i >= 0) list.SelectedIndex = i; };
            list.Click += delegate { commit(); };
            list.KeyDown += delegate(object sender, KeyEventArgs args) { if (args.KeyCode == Keys.Enter) { commit(); args.Handled = true; } else if (args.KeyCode == Keys.Escape) { dropdown.Close(); args.Handled = true; } };
            dropdown.Closed += delegate
            {
                // SetVisibleCore still uses the menu handle after raising Closed. Keep the owned popup
                // alive for reuse; disposing here throws ObjectDisposedException inside WinForms.
                if (!IsDisposed && !Disposing) Invalidate();
            };
            return dropdown;
        }
        protected override void Dispose(bool disposing) { if (disposing && popup != null) { var old = popup; popup = null; old.Dispose(); } base.Dispose(disposing); }
        protected override AccessibleObject CreateAccessibilityInstance() { return new SelectAccessibility(this); }
        private sealed class SelectAccessibility : ControlAccessibleObject
        {
            private readonly CompactSelect owner;
            internal SelectAccessibility(CompactSelect select) : base(select) { owner = select; }
            public override string Value { get { return owner.SelectedItem == null ? "" : owner.SelectedItem.ToString(); } set { owner.SelectedItem = value; } }
            public override string DefaultAction { get { return "展开"; } }
            public override void DoDefaultAction() { owner.Open(); }
        }
    }
    internal sealed class PhotoCard : Control
    {
        private Image photo;
        internal string Caption = "每天，遇见一处风景";
        internal string Credit = "等待获取今天的必应图片";
        public PhotoCard() { DoubleBuffered = true; TabStop = false; AccessibleName = "当前必应壁纸预览"; AccessibleRole = AccessibleRole.Graphic; SetStyle(ControlStyles.Opaque | ControlStyles.ResizeRedraw, true); }
        internal void SetPhoto(Image value) { var old = photo; photo = value; if (old != null) old.Dispose(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Style.PaintBackground(this, e.Graphics);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float dpi = e.Graphics.DpiX / 96F;
            Func<int, int> s = value => (int)Math.Round(value * dpi);
            using (var path = Style.Round(new RectangleF(0, 0, Width, Height), 12 * dpi))
            {
                var state = e.Graphics.Save(); e.Graphics.SetClip(path, CombineMode.Intersect);
                if (photo != null)
                {
                    float factor = Math.Max((float)Width / photo.Width, (float)Height / photo.Height);
                    float w = photo.Width * factor, h = photo.Height * factor;
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    e.Graphics.DrawImage(photo, (Width - w) / 2, (Height - h) / 2, w, h);
                }
                else using (var bg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(55, 99, 86), Color.FromArgb(108, 147, 116), 35)) e.Graphics.FillRectangle(bg, ClientRectangle);
                using (var gradient = new LinearGradientBrush(ClientRectangle, Color.FromArgb(0, 8, 23, 18), Color.FromArgb(225, 8, 23, 18), 90F))
                {
                    gradient.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, 8, 23, 18), Color.FromArgb(0, 8, 23, 18), Color.FromArgb(225, 8, 23, 18) }, Positions = new[] { 0F, .20F, 1F } };
                    gradient.WrapMode = WrapMode.TileFlipY; e.Graphics.FillRectangle(gradient, ClientRectangle);
                }
                using (var badge = Style.Round(new RectangleF(s(12), s(12), s(124), s(24)), s(12)))
                using (var fill = new SolidBrush(Color.FromArgb(110, 10, 27, 21))) e.Graphics.FillPath(fill, badge);
                using (var small = Style.Font(8.5F, true))
                    Style.PaintText(e.Graphics, "BING  ·  每日一图", small, new Rectangle(s(21), s(12), s(108), s(24)), Color.White, TextFormatFlags.VerticalCenter);
                using (var title = Style.Font(16, true))
                    Style.PaintText(e.Graphics, Caption, title, new Rectangle(s(15), Height - s(76), Width - s(30), s(36)), Color.White,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                using (var small = Style.Font(8.5F, false))
                    Style.PaintText(e.Graphics, Credit, small, new Rectangle(s(16), Height - s(38), Width - s(32), s(23)), Color.FromArgb(225, 236, 230),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                e.Graphics.Restore(state);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing && photo != null) { photo.Dispose(); photo = null; } base.Dispose(disposing); }
    }
}

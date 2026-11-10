using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace SettingNiranV2 {
    // ===== Shared theme =====
    public static class Theme {
        public static readonly Color Bg = Color.FromArgb(14, 14, 16);
        public static readonly Color Card = Color.FromArgb(26, 26, 30);
        public static readonly Color Border = Color.FromArgb(52, 52, 58);
        public static readonly Color Text = Color.FromArgb(240, 240, 240);
        public static readonly Color Muted = Color.FromArgb(160, 160, 172);
        public static GraphicsPath Round(Rectangle r, int rad) {
            var p = new GraphicsPath(); int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
        public static Stream Res(string name) { return Assembly.GetExecutingAssembly().GetManifestResourceStream(name); }
        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int a, int b, int c, int d, int e, int f);
        public static void RoundForm(Form f, int r) { f.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, f.Width + 1, f.Height + 1, r * 2, r * 2)); }
    }

    // ===== Frosted glass engine: sharp background + pre-blurred copy =====
    public static class Glass {
        public static Bitmap Sharp, Blur;
        public static void Build(Image src, Size sz) {
            Sharp = new Bitmap(sz.Width, sz.Height);
            using (var g = Graphics.FromImage(Sharp)) {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float s = Math.Max((float)sz.Width / src.Width, (float)sz.Height / src.Height);
                float w = src.Width * s, h = src.Height * s;
                g.DrawImage(src, (sz.Width - w) / 2f, (sz.Height - h) / 2f, w, h);
                using (var b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillRectangle(b, 0, 0, sz.Width, sz.Height);
            }
            Blur = MakeBlur(Sharp);
        }
        static Bitmap MakeBlur(Bitmap src) {
            Bitmap cur = src;
            for (int pass = 0; pass < 2; pass++) {
                int w = Math.Max(8, cur.Width / 6), h = Math.Max(8, cur.Height / 6);
                var small = new Bitmap(w, h);
                using (var g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.DrawImage(cur, 0, 0, w, h); }
                var big = new Bitmap(src.Width, src.Height);
                using (var g = Graphics.FromImage(big)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.DrawImage(small, 0, 0, src.Width, src.Height); }
                small.Dispose(); cur = big;
            }
            return cur;
        }
        // Draw the blurred background that sits behind control c, clipped to path, then a dark tint.
        public static void Frost(Graphics g, Control c, GraphicsPath path, int tintAlpha) {
            var f = c.FindForm();
            if (f != null && Blur != null) {
                var p = f.PointToClient(c.PointToScreen(Point.Empty));
                var st = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                g.DrawImageUnscaled(Blur, -p.X, -p.Y);
                g.Restore(st);
            }
            using (var br = new SolidBrush(Color.FromArgb(tintAlpha, 20, 20, 20))) g.FillPath(br, path);
        }
    }

    // ===== Glass panel =====
    public class GlassPanel : Panel {
        public int Radius = 14, Tint = 105;
        public GlassPanel() {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), Radius)) {
                Glass.Frost(g, this, path, Tint);
                using (var pen = new Pen(Color.FromArgb(55, 255, 255, 255))) g.DrawPath(pen, path);
            }
        }
    }

    // ===== Glass top bar (draggable from MainForm) =====
    public class GlassBar : Control {
        public string Title = "", Subtitle = "";
        public GlassBar() {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using (var path = new GraphicsPath()) {
                path.AddRectangle(new Rectangle(0, 0, Width, Height));
                Glass.Frost(g, this, path, 140);
            }
            using (var pen = new Pen(Color.FromArgb(50, 255, 255, 255))) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            using (var f = new Font("Segoe UI Semibold", 10f)) using (var b = new SolidBrush(Theme.Text)) g.DrawString(Title, f, b, 16, 10);
            using (var f = new Font("Segoe UI", 8.5f)) using (var b = new SolidBrush(Theme.Muted)) g.DrawString(Subtitle, f, b, 172, 12);
        }
    }

    // ===== Custom toggle (fixed: toggles on mouse up and redraws immediately) =====
    public class GlassToggle : Control {
        public string Desc = ""; bool chk, hover;
        public event EventHandler CheckedChanged;
        public bool Checked {
            get { return chk; }
            set { if (chk != value) { chk = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); } }
        }
        public GlassToggle() {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.StandardClick, true);
            BackColor = Color.Transparent; Cursor = Cursors.Hand; Size = new Size(242, 42);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseUp(MouseEventArgs e) {
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location)) { Checked = !Checked; Invalidate(); Update(); }
            base.OnMouseUp(e);
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            if (hover) using (var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), 8)) using (var b = new SolidBrush(Color.FromArgb(18, 255, 255, 255))) g.FillPath(b, path);
            var track = new Rectangle(6, 11, 34, 18);
            using (var path = Theme.Round(track, 9)) {
                using (var b = new SolidBrush(chk ? Color.FromArgb(235, 240, 240, 245) : Color.FromArgb(55, 255, 255, 255))) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(chk ? 0 : 90, 255, 255, 255))) g.DrawPath(p, path);
            }
            int tx = chk ? track.Right - 16 : track.X + 2;
            using (var b = new SolidBrush(chk ? Color.FromArgb(20, 20, 22) : Color.FromArgb(200, 200, 208))) g.FillEllipse(b, tx, track.Y + 2, 14, 14);
            using (var f = new Font("Segoe UI", 9.5f)) using (var b = new SolidBrush(Theme.Text)) g.DrawString(Text, f, b, 50, 4);
            using (var f = new Font("Segoe UI", 8f)) using (var b = new SolidBrush(Theme.Muted)) g.DrawString(Desc, f, b, 50, 22);
        }
    }

    // ===== Glass button =====
    public class RoundedButton : Control {
        public bool Primary; bool hover, down;
        public RoundedButton() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent; Cursor = Cursors.Hand; Font = new Font("Segoe UI Semibold", 9.5f);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            Color fill, fore = Primary ? Color.Black : Theme.Text; int borderA = 60;
            if (Primary) fill = Color.FromArgb(down ? 190 : hover ? 235 : 215, 255, 255, 255);
            else fill = Color.FromArgb(down ? 70 : hover ? 52 : 32, 255, 255, 255);
            if (!Enabled) { fill = Color.FromArgb(22, 255, 255, 255); fore = Color.FromArgb(110, 110, 118); borderA = 25; }
            using (var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), 9)) {
                Glass.Frost(g, this, path, 90);
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(borderA, 255, 255, 255))) g.DrawPath(p, path);
            }
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var tb = new SolidBrush(fore)) g.DrawString(Text, Font, tb, new RectangleF(0, 0, Width, Height), sf);
        }
    }

    // ===== Minimize / Close =====
    public enum BoxKind { Minimize, Close }
    public class ControlBoxButton : Control {
        public BoxKind Kind; bool hover, down;
        public ControlBoxButton(BoxKind k) {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Kind = k; Size = new Size(46, 40); Cursor = Cursors.Hand; BackColor = Color.Transparent;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (hover) {
                Color c = Kind == BoxKind.Close ? Color.FromArgb(down ? 230 : 190, 225, 45, 55) : Color.FromArgb(down ? 70 : 45, 255, 255, 255);
                using (var path = Theme.Round(new Rectangle(5, 6, Width - 10, Height - 12), 8)) using (var b = new SolidBrush(c)) g.FillPath(b, path);
            }
            using (var pen = new Pen(Color.FromArgb(230, 255, 255, 255), 1.4f)) {
                int cx = Width / 2, cy = Height / 2;
                if (Kind == BoxKind.Minimize) g.DrawLine(pen, cx - 6, cy, cx + 6, cy);
                else { g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5); g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5); }
            }
        }
    }

    // ===== Transparent log view =====
    public class LogView : Control {
        class Line { public Color C; public string T; }
        List<Line> lines = new List<Line>(); int offset;
        public LogView() {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }
        public void AddLine(Color c, string t) {
            lines.Add(new Line { C = c, T = t }); if (lines.Count > 300) lines.RemoveAt(0);
            offset = 0; Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e) {
            offset += e.Delta > 0 ? 2 : -2; if (offset < 0) offset = 0; if (offset > Math.Max(0, lines.Count - 1)) offset = Math.Max(0, lines.Count - 1);
            Invalidate(); base.OnMouseWheel(e);
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            const int lh = 16; int vis = Math.Max(1, Height / lh);
            int end = lines.Count - offset; int start = Math.Max(0, end - vis);
            using (var f = new Font("Consolas", 9f))
                for (int i = start; i < end; i++) using (var b = new SolidBrush(lines[i].C)) g.DrawString(lines[i].T, f, b, 0, (i - start) * lh);
        }
    }
}

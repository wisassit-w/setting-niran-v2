using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace SettingNiranV2 {
    public class SplashForm : Form {
        Timer timer = new Timer(); int progress; int hold; Image logo;
        public SplashForm() {
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(520, 300); BackColor = Theme.Bg; DoubleBuffered = true; TopMost = true; ShowInTaskbar = false;
            try { logo = Image.FromStream(Theme.Res("logo.png")); } catch { }
            Theme.RoundForm(this, 16);
            timer.Interval = 25;
            timer.Tick += delegate {
                if (progress < 100) { progress++; Invalidate(); }
                else if (++hold > 10) { timer.Stop(); Close(); }
            };
            timer.Start();
        }
        string Status() {
            if (progress < 25) return "Loading core modules...";
            if (progress < 50) return "Reading system configuration...";
            if (progress < 75) return "Preparing optimization engine...";
            if (progress < 100) return "Checking permissions...";
            return "Ready";
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var p = new Pen(Theme.Border)) using (var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), 16)) g.DrawPath(p, path);
            if (logo != null) {
                var r = new Rectangle((Width - 84) / 2, 30, 84, 84);
                using (var gp = new GraphicsPath()) { gp.AddEllipse(r); g.SetClip(gp); g.DrawImage(logo, r); g.ResetClip(); }
                using (var p = new Pen(Theme.Border, 2)) g.DrawEllipse(p, r);
            }
            var sf = new StringFormat { Alignment = StringAlignment.Center };
            using (var f = new Font("Segoe UI Semibold", 20f)) g.DrawString("SETTING NIRAN V2", f, Brushes.White, new RectangleF(0, 128, Width, 40), sf);
            using (var f = new Font("Segoe UI", 9f)) using (var b = new SolidBrush(Theme.Muted)) {
                g.DrawString("FiveM PvP Optimization Suite", f, b, new RectangleF(0, 168, Width, 20), sf);
                g.DrawString(Status(), f, b, new RectangleF(0, 214, Width, 20), sf);
            }
            var track = new Rectangle(80, 244, 360, 4);
            using (var b = new SolidBrush(Theme.Card)) g.FillRectangle(b, track);
            g.FillRectangle(Brushes.White, 80, 244, (int)(3.6 * progress), 4);
        }
    }
}

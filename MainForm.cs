using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Security.Principal;
using System.Windows.Forms;
namespace SettingNiranV2 {
    public class MainForm : Form {
        GlassBar bar; LogView logView;
        Dictionary<string, GlassToggle> boxes = new Dictionary<string, GlassToggle>();
        RoundedButton btnApply, btnAll, btnRevert, btnCache, btnNet;

        protected override CreateParams CreateParams {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x02000000; cp.Style |= 0x00020000; return cp; } // WS_EX_COMPOSITED, WS_MINIMIZEBOX
        }

        public MainForm() {
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 610); BackColor = Theme.Bg; DoubleBuffered = true; Text = "Setting Niran V2";
            try { Icon = new Icon(Theme.Res("app.ico")); } catch { }
            using (var img = Image.FromStream(Theme.Res("bg.jpg"))) Glass.Build(img, ClientSize);
            Theme.RoundForm(this, 14);
            Engine.Log = Write;

            // glass top bar + control box
            bar = new GlassBar { Title = "SETTING NIRAN V2", Subtitle = "PvP Optimization Suite", Size = new Size(900, 40), Dock = DockStyle.Top };
            var bClose = new ControlBoxButton(BoxKind.Close) { Location = new Point(900 - 46, 0), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            var bMin = new ControlBoxButton(BoxKind.Minimize) { Location = new Point(900 - 92, 0), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            bClose.Click += delegate { Close(); };
            bMin.Click += delegate { WindowState = FormWindowState.Minimized; };
            bar.Controls.Add(bClose); bar.Controls.Add(bMin);
            bar.MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(Handle, 0xA1, 2, 0); } };
            Controls.Add(bar);

            Card("NETWORK", 20); Card("INPUT & TIMERS", 315); var f = Card("FIVEM & SYSTEM", 610);
            btnCache = new RoundedButton { Text = "Clear FiveM Cache", Location = new Point(16, 204), Size = new Size(238, 36) };
            btnNet = new RoundedButton { Text = "Network Reset", Location = new Point(16, 248), Size = new Size(238, 36) };
            btnCache.Click += delegate { RunBusy(Engine.ClearCache); };
            btnNet.Click += delegate { if (MessageBox.Show("This resets Winsock / IP / TCP and may briefly drop your connection. Continue?", "Network Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) RunBusy(Engine.NetworkReset); };
            f.Controls.Add(btnCache); f.Controls.Add(btnNet);

            btnApply = new RoundedButton { Text = "APPLY SELECTED", Primary = true, Location = new Point(20, 376), Size = new Size(300, 44) };
            btnAll = new RoundedButton { Text = "Select all", Location = new Point(332, 376), Size = new Size(120, 44) };
            btnRevert = new RoundedButton { Text = "Revert changes", Location = new Point(760, 376), Size = new Size(120, 44) };
            btnApply.Click += delegate { Apply(); };
            btnAll.Click += delegate { foreach (var c in boxes.Values) c.Checked = true; };
            btnRevert.Click += delegate { if (MessageBox.Show("Revert Setting Niran V2 changes back to Windows defaults?", "Revert", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) RunBusy(Engine.Revert); };
            Controls.Add(btnApply); Controls.Add(btnAll); Controls.Add(btnRevert);

            var logPanel = new GlassPanel { Location = new Point(20, 436), Size = new Size(860, 154), Padding = new Padding(14, 10, 14, 10) };
            logView = new LogView { Dock = DockStyle.Fill };
            logPanel.Controls.Add(logView); Controls.Add(logPanel);

            bool admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            Write(admin ? "Ready. Running as administrator." : "[!] Not running as administrator. Tweaks will fail. Re-open as admin.");
        }

        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.DrawImageUnscaled(Glass.Sharp, 0, 0); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(Color.FromArgb(70, 255, 255, 255))) using (var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), 14)) e.Graphics.DrawPath(p, path);
        }

        GlassPanel Card(string header, int x) {
            var p = new GlassPanel { Location = new Point(x, 56), Size = new Size(270, 304) };
            p.Controls.Add(new Label { Text = header, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8.5f), Location = new Point(18, 14), AutoSize = true, UseMnemonic = false, BackColor = Color.Transparent });
            int y = 40;
            foreach (var t in Engine.All) {
                if (t.Group != header) continue;
                var tg = new GlassToggle { Text = t.Name, Desc = t.Desc, Checked = t.Def, Location = new Point(14, y) };
                p.Controls.Add(tg); boxes[t.Id] = tg; y += 48;
            }
            Controls.Add(p); return p;
        }

        void Apply() {
            var sel = new List<Tweak>();
            foreach (var t in Engine.All) if (boxes[t.Id].Checked) sel.Add(t);
            if (sel.Count == 0) { Write("[!] Nothing selected."); return; }
            RunBusy(delegate {
                Write("Applying " + sel.Count + " tweak(s)...");
                Write("Creating restore point (can take ~15 s)...");
                Engine.RestorePoint();
                foreach (var t in sel) { try { Write("Working: " + t.Name + "..."); t.Apply(); Write("[OK] " + t.Name); } catch (Exception ex) { Write("[FAIL] " + t.Name + ": " + ex.Message); } }
                Write("Done. A restart is recommended.");
            });
        }

        void SetBusy(bool b) { foreach (var c in new Control[] { btnApply, btnAll, btnRevert, btnCache, btnNet }) c.Enabled = !b; }
        void RunBusy(Action work) {
            SetBusy(true);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate {
                try { work(); } catch (Exception ex) { Write("[FAIL] " + ex.Message); }
                try { BeginInvoke(new Action(delegate { SetBusy(false); })); } catch { }
            });
        }
        void Write(string s) {
            if (logView == null) return;
            if (InvokeRequired) { try { BeginInvoke(new Action<string>(Write), s); } catch { } return; }
            Color c = s.StartsWith("[OK]") ? Color.FromArgb(130, 230, 150) : (s.StartsWith("[FAIL]") || s.StartsWith("[!]")) ? Color.FromArgb(245, 125, 125) : Theme.Muted;
            logView.AddLine(c, DateTime.Now.ToString("HH:mm:ss") + "  " + s);
        }
    }
}

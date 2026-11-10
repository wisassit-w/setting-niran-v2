using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
[assembly: AssemblyTitle("Setting Niran V2")]
[assembly: AssemblyProduct("Setting Niran V2")]
[assembly: AssemblyVersion("2.0.0.0")]
namespace SettingNiranV2 {
    static class Program {
        static void Fail(Exception ex) { MessageBox.Show(ex.ToString(), "Setting Niran V2 - error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Fail(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Fail(e.ExceptionObject as Exception); };
            try {
                using (var splash = new SplashForm()) Application.Run(splash);
                Application.Run(new MainForm());
            } catch (Exception ex) { Fail(ex); }
        }
    }
}

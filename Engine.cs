using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace SettingNiranV2 {
    static class Native {
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, IntPtr pv, uint flags);
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, int[] pv, uint flags);
        [DllImport("ntdll.dll")] public static extern int NtSetTimerResolution(uint desired, bool set, out uint current);
        [DllImport("ntdll.dll")] public static extern int NtQueryTimerResolution(out uint min, out uint max, out uint cur);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
    }

    public class Tweak { public string Id, Name, Desc, Group; public bool Def; public Action Apply; }

    public static class Engine {
        public static Action<string> Log = delegate { };
        const RegistryValueKind DW = RegistryValueKind.DWord, STR = RegistryValueKind.String;
        const string IFEO = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\";
        const string LAYERS = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        const string FSO = "DISABLEDXMAXIMIZEDWINDOWEDMODE";
        static readonly string[] Builds = { "2060", "2189", "2372", "2545", "2612", "2699", "2802", "2944", "3095", "3258", "3323", "3407", "3570" };

        public static List<string> GameExes() {
            var l = new List<string> { "FiveM.exe", "GTA5.exe", "FiveM_GTAProcess.exe" };
            foreach (var b in Builds) l.Add("FiveM_b" + b + "_GTAProcess.exe");
            return l;
        }
        public static string FiveMDir() { return Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "", "FiveM"); }

        // ---------- helpers ----------
        public static string Exec(string file, string args) {
            try {
                var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd(); string e = p.StandardError.ReadToEnd(); p.WaitForExit(90000); return (o + e).Trim(); }
            } catch (Exception ex) { return "ERR: " + ex.Message; }
        }
        public static string PS(string script) {
            return Exec("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        }
        static void LM(string path, string name, object v, RegistryValueKind k) { using (var key = Registry.LocalMachine.CreateSubKey(path)) key.SetValue(name, v, k); }
        static void CU(string path, string name, object v, RegistryValueKind k) { using (var key = Registry.CurrentUser.CreateSubKey(path)) key.SetValue(name, v, k); }
        static void DelLM(string path, string name) { using (var k = Registry.LocalMachine.OpenSubKey(path, true)) { if (k != null) k.DeleteValue(name, false); } }

        public static List<string> FsoPaths() {
            var l = new List<string>(); string d = FiveMDir();
            string[] c = { Path.Combine(d, "FiveM.exe"), Path.Combine(d, @"FiveM.app\FiveM.exe"),
                @"C:\Program Files\Rockstar Games\Grand Theft Auto V\GTA5.exe", @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V\GTA5.exe",
                @"C:\Program Files\Epic Games\GTAV\GTA5.exe" };
            foreach (var p in c) if (File.Exists(p)) l.Add(p);
            try { foreach (var p in Directory.GetFiles(Path.Combine(d, "FiveM.app"), "FiveM*.exe")) if (!l.Contains(p)) l.Add(p); } catch { }
            try { using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V")) { var f = k == null ? null : k.GetValue("InstallFolder") as string; if (f != null) { var p = Path.Combine(f, "GTA5.exe"); if (File.Exists(p) && !l.Contains(p)) l.Add(p); } } } catch { }
            return l;
        }

        // ---------- tweaks ----------
        public static List<Tweak> All = new List<Tweak> {
            new Tweak { Id="qos", Group="NETWORK", Name="QoS packet priority", Desc="DSCP 46 policy for FiveM / GTA", Def=true, Apply=Qos },
            new Tweak { Id="tcp", Group="NETWORK", Name="TCP low latency", Desc="AckFreq 1, NoDelay 1, Retrans 3", Def=true, Apply=Tcp },
            new Tweak { Id="thr", Group="NETWORK", Name="Disable network throttling", Desc="Throttling off, max responsiveness", Def=true, Apply=Throttle },
            new Tweak { Id="nic", Group="NETWORK", Name="NIC tuning", Desc="LSO off, RSS on, ECN/timestamps off", Def=true, Apply=Nic },
            new Tweak { Id="dns", Group="NETWORK", Name="Cloudflare DNS", Desc="Set 1.1.1.1 / 1.0.0.1 (optional)", Def=false, Apply=Dns },
            new Tweak { Id="kbd", Group="INPUT & TIMERS", Name="Fastest keyboard input", Desc="Max repeat rate, zero delay", Def=true, Apply=Keyboard },
            new Tweak { Id="mou", Group="INPUT & TIMERS", Name="Mouse acceleration off", Desc="Raw 1:1 pointer movement", Def=true, Apply=Mouse },
            new Tweak { Id="hpet", Group="INPUT & TIMERS", Name="Disable HPET / dynamic tick", Desc="bcdedit tweaks (restart needed)", Def=true, Apply=Hpet },
            new Tweak { Id="tmr", Group="INPUT & TIMERS", Name="Timer resolution 0.5 ms", Desc="Held while this app stays open", Def=true, Apply=Timer05 },
            new Tweak { Id="pri", Group="FIVEM & SYSTEM", Name="High CPU priority", Desc="Persistent for FiveM / GTA processes", Def=true, Apply=Priority },
            new Tweak { Id="fso", Group="FIVEM & SYSTEM", Name="Disable fullscreen optimizations", Desc="Applied to game executables", Def=true, Apply=Fso },
            new Tweak { Id="pwr", Group="FIVEM & SYSTEM", Name="Ultimate Performance power plan", Desc="Activates the max performance plan", Def=true, Apply=Power },
        };

        const string QOSROOT = @"SOFTWARE\Policies\Microsoft\Windows\QoS\";
        static void Qos() {
            LM(@"SYSTEM\CurrentControlSet\Services\Tcpip\QoS", "Do not use NLA", "1", STR);
            int n = 0;
            foreach (var exe in GameExes()) {
                using (var k = Registry.LocalMachine.CreateSubKey(QOSROOT + "SNV2_" + exe)) {
                    k.SetValue("Version", "1.0", STR); k.SetValue("Application Name", exe, STR); k.SetValue("Protocol", "*", STR);
                    k.SetValue("Local Port", "*", STR); k.SetValue("Local IP", "*", STR); k.SetValue("Local IP Prefix Length", "*", STR);
                    k.SetValue("Remote Port", "*", STR); k.SetValue("Remote IP", "*", STR); k.SetValue("Remote IP Prefix Length", "*", STR);
                    k.SetValue("DSCP Value", "46", STR); k.SetValue("Throttle Rate", "-1", STR);
                }
                n++;
            }
            Log("QoS policies written for " + n + " executables (fully active after restart).");
        }
        static void Tcp() {
            using (var ifs = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true))
                foreach (var n in ifs.GetSubKeyNames()) using (var k = ifs.OpenSubKey(n, true)) { k.SetValue("TcpAckFrequency", 1, DW); k.SetValue("TCPNoDelay", 1, DW); k.SetValue("TcpDelAckTicks", 0, DW); }
            LM(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "TcpMaxDataRetransmissions", 3, DW);
        }
        static void Throttle() {
            LM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), DW);
            LM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0, DW);
        }
        static void Nic() {
            Exec("netsh", "int tcp set global autotuninglevel=normal");
            Exec("netsh", "int tcp set global rss=enabled");
            Exec("netsh", "int tcp set global ecncapability=disabled");
            Exec("netsh", "int tcp set global timestamps=disabled");
            PS("Disable-NetAdapterLso -Name '*' -ErrorAction SilentlyContinue");
        }
        static void Dns() { PS("Get-NetAdapter | Where-Object Status -eq 'Up' | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ServerAddresses ('1.1.1.1','1.0.0.1') }; Clear-DnsClientCache"); }
        static void Keyboard() {
            CU(@"Control Panel\Keyboard", "KeyboardSpeed", "31", STR); CU(@"Control Panel\Keyboard", "KeyboardDelay", "0", STR);
            Native.SystemParametersInfo(0x000B, 31, IntPtr.Zero, 3); Native.SystemParametersInfo(0x0017, 0, IntPtr.Zero, 3);
        }
        static void Mouse() {
            CU(@"Control Panel\Mouse", "MouseSpeed", "0", STR); CU(@"Control Panel\Mouse", "MouseThreshold1", "0", STR); CU(@"Control Panel\Mouse", "MouseThreshold2", "0", STR);
            Native.SystemParametersInfo(0x0004, 0, new int[] { 0, 0, 0 }, 3);
        }
        static void Hpet() {
            Exec("bcdedit", "/set useplatformclock false");
            Exec("bcdedit", "/set disabledynamictick yes");
            PS("Get-PnpDevice -FriendlyName 'High precision event timer' -ErrorAction SilentlyContinue | Disable-PnpDevice -Confirm:$false -ErrorAction SilentlyContinue");
            Log("[!] Restart Windows for HPET / tick changes to take effect.");
        }
        static void Timer05() {
            LM(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "GlobalTimerResolutionRequests", 1, DW);
            uint cur; Native.NtSetTimerResolution(5000, true, out cur);
            uint mn, mx, c; Native.NtQueryTimerResolution(out mn, out mx, out c);
            Log("Timer resolution now " + (c / 10000.0).ToString("0.000") + " ms (hardware min " + (mx / 10000.0).ToString("0.000") + " ms)");
        }
        static void Priority() {
            foreach (var exe in GameExes()) { using (var k = Registry.LocalMachine.CreateSubKey(IFEO + exe + @"\PerfOptions")) k.SetValue("CpuPriorityClass", 3, DW); }
            int n = 0;
            foreach (var p in Process.GetProcesses()) {
                try { string nm = p.ProcessName; if (nm.StartsWith("FiveM", StringComparison.OrdinalIgnoreCase) || nm.Equals("GTA5", StringComparison.OrdinalIgnoreCase)) { p.PriorityClass = ProcessPriorityClass.High; n++; } } catch { }
            }
            Log("High priority set (persistent) + applied to " + n + " running process(es).");
        }
        static void Fso() {
            var paths = FsoPaths();
            using (var k = Registry.CurrentUser.CreateSubKey(LAYERS))
                foreach (var p in paths) { string cur = (k.GetValue(p) as string) ?? ""; if (!cur.Contains(FSO)) { if (cur.Length == 0) cur = "~"; k.SetValue(p, cur + " " + FSO, STR); } }
            Log("Fullscreen optimizations disabled for " + paths.Count + " executable(s).");
        }
        static void Power() {
            string o = Exec("powercfg", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");
            var m = Regex.Match(o, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            if (m.Success) { Exec("powercfg", "-setactive " + m.Value); Log("Ultimate Performance plan active."); }
            else { Exec("powercfg", "-setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"); Log("Ultimate plan unavailable, High Performance set instead."); }
        }

        // ---------- actions ----------
        public static void RestorePoint() {
            string r = PS("try { Checkpoint-Computer -Description 'Setting Niran V2' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; 'ok' } catch { 'skip' }");
            Log(r.Contains("ok") ? "Restore point created." : "Restore point skipped (disabled or created within the last 24h).");
        }
        public static void ClearCache() {
            if (Process.GetProcessesByName("FiveM").Length > 0) { Log("[!] FiveM is running. Close it first, then clear the cache."); return; }
            long freed = 0; string data = Path.Combine(FiveMDir(), @"FiveM.app\data");
            foreach (var sub in new[] { "cache", "server-cache", "server-cache-priv" }) {
                string d = Path.Combine(data, sub); if (!Directory.Exists(d)) continue;
                foreach (var f in Directory.GetFiles(d, "*", SearchOption.AllDirectories)) { try { var fi = new FileInfo(f); long s = fi.Length; fi.Delete(); freed += s; } catch { } }
                foreach (var sd in Directory.GetDirectories(d)) { try { Directory.Delete(sd, true); } catch { } }
            }
            Log("[OK] Cache cleared, " + (freed / 1048576.0).ToString("0.0") + " MB freed.");
        }
        public static void NetworkReset() {
            Exec("ipconfig", "/flushdns"); Exec("netsh", "winsock reset"); Exec("netsh", "int ip reset"); Exec("netsh", "int tcp reset");
            Log("[OK] DNS flushed, Winsock / IP / TCP stack reset. Restart Windows to finish.");
        }
        public static void Revert() {
            using (var q = Registry.LocalMachine.OpenSubKey(QOSROOT, true)) { if (q != null) foreach (var nm in q.GetSubKeyNames()) if (nm.StartsWith("SNV2_")) q.DeleteSubKeyTree(nm, false); }
            PS("Get-NetAdapter | Where-Object Status -eq 'Up' | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ResetServerAddresses }; Enable-NetAdapterLso -Name '*' -ErrorAction SilentlyContinue; Get-PnpDevice -FriendlyName 'High precision event timer' -ErrorAction SilentlyContinue | Enable-PnpDevice -Confirm:$false -ErrorAction SilentlyContinue");
            Exec("bcdedit", "/deletevalue useplatformclock"); Exec("bcdedit", "/deletevalue disabledynamictick");
            using (var ifs = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true))
                foreach (var n in ifs.GetSubKeyNames()) using (var k = ifs.OpenSubKey(n, true)) { k.DeleteValue("TcpAckFrequency", false); k.DeleteValue("TCPNoDelay", false); k.DeleteValue("TcpDelAckTicks", false); }
            DelLM(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "TcpMaxDataRetransmissions");
            DelLM(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "GlobalTimerResolutionRequests");
            LM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", 10, DW);
            LM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 20, DW);
            CU(@"Control Panel\Keyboard", "KeyboardDelay", "1", STR);
            Native.SystemParametersInfo(0x0017, 1, IntPtr.Zero, 3);
            foreach (var exe in GameExes()) using (var k = Registry.LocalMachine.OpenSubKey(IFEO + exe, true)) { if (k != null) k.DeleteSubKeyTree("PerfOptions", false); }
            using (var k = Registry.CurrentUser.CreateSubKey(LAYERS))
                foreach (var p in FsoPaths()) { string cur = k.GetValue(p) as string; if (cur == null) continue; cur = cur.Replace(" " + FSO, "").Trim(); if (cur == "~" || cur.Length == 0) k.DeleteValue(p, false); else k.SetValue(p, cur, STR); }
            Exec("powercfg", "-setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
            Log("[OK] Changes reverted (mouse setting left as is). Restart Windows to finish.");
        }
    }
}

// Shoal.exe: a double-click launcher that opens Shoal in its own window and streams VR to a
// Meta Quest over Meta Quest Link. Written for the C# 5 compiler that ships with Windows.
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Shoal")]
[assembly: System.Reflection.AssemblyProduct("Shoal")]
[assembly: System.Reflection.AssemblyDescription("Swim through your photos in VR")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

static class ShoalLauncher
{
    const string AppUrl = "https://mkaram99.github.io/shoal/";
    const string LinkClient = @"C:\Program Files\Oculus\Support\oculus-client\Client.exe";
    const string MetaRuntime = @"C:\Program Files\Oculus\Support\oculus-runtime\oculus_openxr_64.json";
    const string Title = "Shoal";

    [STAThread]
    static int Main()
    {
        Application.EnableVisualStyles();
        try
        {
            // Shoal always uses Meta Quest Link. If another runtime (e.g. Virtual Desktop) is the PC's default,
            // point only Shoal's own browser window at Link through XR_RUNTIME_JSON; the PC-wide setting is untouched.
            string runtime = ActiveOpenXrRuntime();
            bool linkInstalled = File.Exists(MetaRuntime);
            if (!IsMetaRuntime(runtime) && !linkInstalled)
            {
                DialogResult answer = MessageBox.Show(
                    "Shoal sends VR to your headset through Meta Quest Link, but Meta Quest Link isn't installed on this PC.\n\n" +
                    "Open Shoal anyway? (It works on the desktop, but Enter VR won't reach the headset.)",
                    Title, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer != DialogResult.Yes) return 0;
            }
            bool pointAtLink = !IsMetaRuntime(runtime) && linkInstalled;

            StartLinkIfNeeded();

            string browser = FindBrowser();
            if (browser == null)
            {
                MessageBox.Show("Shoal needs Microsoft Edge or Google Chrome, and neither was found on this PC.",
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }

            // A profile of its own keeps Shoal's saved photos and settings apart from normal browsing.
            string profile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shoal", "Browser");
            Directory.CreateDirectory(profile);

            // A Shoal window that is already open would ignore the runtime choice below, so restart it.
            CloseRunningShoal(profile);

            ProcessStartInfo start = new ProcessStartInfo(browser,
                "--app=\"" + AppUrl + "\" " +
                "--user-data-dir=\"" + profile + "\" " +
                "--no-first-run --no-default-browser-check --window-size=1600,900");
            start.UseShellExecute = false;
            if (pointAtLink) start.EnvironmentVariables["XR_RUNTIME_JSON"] = MetaRuntime;
            Process.Start(start);
            return 0;
        }
        catch (Exception error)
        {
            MessageBox.Show("Shoal couldn't start: " + error.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    // The per-user setting wins over the machine-wide one, matching how the OpenXR loader picks a runtime.
    static string ActiveOpenXrRuntime()
    {
        string value = ReadRuntime(Registry.CurrentUser);
        return value ?? ReadRuntime(Registry.LocalMachine);
    }

    static string ReadRuntime(RegistryKey hive)
    {
        using (RegistryKey key = hive.OpenSubKey(@"SOFTWARE\Khronos\OpenXR\1"))
        {
            if (key == null) return null;
            string value = key.GetValue("ActiveRuntime") as string;
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }

    static bool IsMetaRuntime(string runtime)
    {
        return runtime != null && runtime.IndexOf("oculus", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string FriendlyRuntimeName(string runtime)
    {
        string lower = runtime.ToLowerInvariant();
        if (lower.Contains("virtual desktop") || lower.Contains("virtualdesktop")) return "Virtual Desktop";
        if (lower.Contains("steamvr") || lower.Contains("steam")) return "SteamVR";
        if (lower.Contains("mixedreality")) return "Windows Mixed Reality";
        return Path.GetFileNameWithoutExtension(runtime);
    }

    // Only closes browser processes that use Shoal's own profile folder, never other browsing windows.
    static void CloseRunningShoal(string profile)
    {
        string query = "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='msedge.exe' OR Name='chrome.exe'";
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
        {
            foreach (ManagementObject proc in searcher.Get())
            {
                string cmd = proc["CommandLine"] as string;
                if (cmd == null || cmd.IndexOf(profile, StringComparison.OrdinalIgnoreCase) < 0) continue;
                try { Process.GetProcessById(Convert.ToInt32(proc["ProcessId"])).Kill(); } catch { }
            }
        }
        System.Threading.Thread.Sleep(800);   // let the old window release its profile
    }

    // The Meta Quest Link app runs as oculus-client\Client.exe (OculusClient.exe beside it is an empty stub).
    // Any problem here is ignored: Shoal still opens, and Link can be started by hand.
    static void StartLinkIfNeeded()
    {
        try
        {
            if (!File.Exists(LinkClient)) return;
            string query = "SELECT ExecutablePath FROM Win32_Process WHERE Name='Client.exe'";
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject proc in searcher.Get())
                {
                    string path = proc["ExecutablePath"] as string;
                    if (path != null && path.IndexOf(@"\oculus-client\", StringComparison.OrdinalIgnoreCase) >= 0) return;
                }
            }
            Process.Start(LinkClient);
        }
        catch { }
    }

    static string FindBrowser()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates =
        {
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            Path.Combine(local, @"Google\Chrome\Application\chrome.exe"),
        };
        foreach (string path in candidates)
        {
            if (File.Exists(path)) return path;
        }
        return null;
    }
}

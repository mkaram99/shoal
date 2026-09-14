// Shoal.exe: a double-click launcher that opens Shoal in its own window and streams VR to a
// Meta Quest over Meta Quest Link. Written for the C# 5 compiler that ships with Windows.
using System;
using System.Diagnostics;
using System.IO;
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
    const string LinkClient = @"C:\Program Files\Oculus\Support\oculus-client\OculusClient.exe";
    const string Title = "Shoal";

    [STAThread]
    static int Main()
    {
        Application.EnableVisualStyles();
        try
        {
            string runtime = ActiveOpenXrRuntime();
            if (!IsMetaRuntime(runtime))
            {
                string current = runtime == null ? "none" : FriendlyRuntimeName(runtime);
                DialogResult answer = MessageBox.Show(
                    "Shoal sends VR to your headset through Meta Quest Link, but this PC's VR runtime is set to " + current + ".\n\n" +
                    "To switch it (one time): open the Meta Quest Link app, go to Settings → General, and next to " +
                    "\"OpenXR Runtime\" click \"Set Meta Quest Link as active\".\n\n" +
                    "Open Shoal anyway?",
                    Title, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer != DialogResult.Yes) return 0;
            }

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

            ProcessStartInfo start = new ProcessStartInfo(browser,
                "--app=\"" + AppUrl + "\" " +
                "--user-data-dir=\"" + profile + "\" " +
                "--no-first-run --no-default-browser-check --window-size=1600,900");
            start.UseShellExecute = false;
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

    static void StartLinkIfNeeded()
    {
        if (Process.GetProcessesByName("OculusClient").Length > 0) return;
        if (!File.Exists(LinkClient)) return;   // Link not installed here: Shoal still opens on the desktop
        Process.Start(LinkClient);
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

// Shoal.exe: a double-click launcher that opens Shoal in its own window and streams VR to a
// Meta Quest over Meta Quest Link. Written for the C# 5 compiler that ships with Windows.
//
// Launched from the PC desktop: opens the Shoal window and exits.
// Launched from the headset (Meta Quest Link library): opens the Shoal window, presses V (Shoal's
// Enter VR shortcut) once the page is ready, and stays running until the Shoal window closes, so
// Link treats Shoal as the running app.
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Shoal")]
[assembly: System.Reflection.AssemblyProduct("Shoal")]
[assembly: System.Reflection.AssemblyDescription("Swim through your photos in VR")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.1.0.0")]

static class ShoalLauncher
{
    const string AppUrl = "https://mkaram99.github.io/shoal/";
    const string LinkClient = @"C:\Program Files\Oculus\Support\oculus-client\Client.exe";
    const string MetaRuntime = @"C:\Program Files\Oculus\Support\oculus-runtime\oculus_openxr_64.json";
    const string Title = "Shoal";
    const string VrTitleMarker = "in VR";        // Shoal adds this to its window title while VR is running
    const byte VK_V = 0x56;                       // Shoal's Enter VR keyboard shortcut

    [STAThread]
    static int Main(string[] args)
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
            bool fromHeadset = HasArg(args, "--vr") || LaunchedByLink();

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
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shoal",
                Path.GetFileNameWithoutExtension(browser).ToLowerInvariant() == "chrome" ? "Chrome" : "Browser");   // Chrome and Edge profiles are not interchangeable
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

            if (fromHeadset)
            {
                EnterVrWhenReady();
                WaitForShoalToClose(profile);
            }
            return 0;
        }
        catch (Exception error)
        {
            MessageBox.Show("Shoal couldn't start: " + error.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    static bool HasArg(string[] args, string name)
    {
        foreach (string a in args) if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ---------------- launched from the headset? ----------------

    // Meta Quest Link starts library apps from its own service processes; a desktop double-click comes from Explorer.
    static bool LaunchedByLink()
    {
        try
        {
            int pid = Process.GetCurrentProcess().Id;
            for (int depth = 0; depth < 6; depth++)
            {
                int parent = -1;
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT ParentProcessId FROM Win32_Process WHERE ProcessId=" + pid))
                {
                    foreach (ManagementObject o in s.Get()) parent = Convert.ToInt32(o["ParentProcessId"]);
                }
                if (parent <= 0) return false;
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT Name, ExecutablePath FROM Win32_Process WHERE ProcessId=" + parent))
                {
                    foreach (ManagementObject o in s.Get())
                    {
                        string name = ((o["Name"] as string) ?? "").ToLowerInvariant();
                        string path = ((o["ExecutablePath"] as string) ?? "").ToLowerInvariant();
                        if (name.StartsWith("ovr") || name == "oculusdash.exe" || path.Contains(@"\oculus\")) return true;
                        if (name == "explorer.exe") return false;
                    }
                }
                pid = parent;
            }
        }
        catch { }
        return false;
    }

    // ---------------- entering VR for someone wearing the headset ----------------

    // Presses V in the Shoal window once the page is ready, then waits long enough for the headset to accept the
    // session (the browser allows about 10 seconds) before trying again. Never presses V while VR is running,
    // because V also exits VR.
    static void EnterVrWhenReady()
    {
        DateTime deadline = DateTime.Now.AddSeconds(120);
        Thread.Sleep(6000);   // page load and headset check
        while (DateTime.Now < deadline)
        {
            string title;
            IntPtr window = FindShoalWindow(out title);
            if (window != IntPtr.Zero)
            {
                if (IsInVr(title)) return;
                if (FocusWindow(window))
                {
                    PressKey(VK_V);
                    DateTime settle = DateTime.Now.AddSeconds(12);
                    while (DateTime.Now < settle)
                    {
                        Thread.Sleep(500);
                        FindShoalWindow(out title);
                        if (IsInVr(title)) return;
                    }
                    continue;
                }
            }
            Thread.Sleep(1500);
        }
    }

    static bool IsInVr(string title)
    {
        return title != null && title.IndexOf(VrTitleMarker, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Waits on the Shoal *window*, not the browser processes: Edge can keep running in the background after its
    // window closes, which would leave this launcher (and Link's idea of the running app) alive forever.
    static void WaitForShoalToClose(string profile)
    {
        string title;
        DateTime appearBy = DateTime.Now.AddSeconds(60);
        while (FindShoalWindow(out title) == IntPtr.Zero)
        {
            if (DateTime.Now > appearBy) return;
            Thread.Sleep(1000);
        }
        int missing = 0;
        while (missing < 3)   // a few consecutive misses, so a brief redraw doesn't count as closing
        {
            Thread.Sleep(2000);
            missing = FindShoalWindow(out title) == IntPtr.Zero ? missing + 1 : 0;
        }
    }

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint attach, uint attachTo, bool doAttach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extraInfo);

    // Shoal's window is a browser app window (class Chrome_WidgetWin_1) whose title starts with "Shoal".
    static IntPtr FindShoalWindow(out string title)
    {
        IntPtr found = IntPtr.Zero;
        string foundTitle = null;
        EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
        {
            if (!IsWindowVisible(hWnd)) return true;
            StringBuilder cls = new StringBuilder(64);
            GetClassName(hWnd, cls, cls.Capacity);
            if (cls.ToString() != "Chrome_WidgetWin_1") return true;
            StringBuilder text = new StringBuilder(256);
            GetWindowText(hWnd, text, text.Capacity);
            string t = text.ToString();
            if (!t.StartsWith(Title, StringComparison.Ordinal)) return true;
            found = hWnd; foundTitle = t;
            return false;
        }, IntPtr.Zero);
        title = foundTitle;
        return found;
    }

    static bool FocusWindow(IntPtr window)
    {
        if (GetForegroundWindow() == window) return true;
        if (IsIconic(window)) ShowWindow(window, 9);   // SW_RESTORE
        uint unused;
        uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out unused);
        uint thisThread = GetCurrentThreadId();
        bool attached = foregroundThread != 0 && foregroundThread != thisThread && AttachThreadInput(thisThread, foregroundThread, true);
        BringWindowToTop(window);
        SetForegroundWindow(window);
        if (attached) AttachThreadInput(thisThread, foregroundThread, false);
        Thread.Sleep(300);
        return GetForegroundWindow() == window;
    }

    static void PressKey(byte vk)
    {
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        Thread.Sleep(40);
        keybd_event(vk, 0, 2, UIntPtr.Zero);   // KEYEVENTF_KEYUP
    }

    // ---------------- runtime, Link and browser ----------------

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

    // Only closes browser processes that use Shoal's own profile folder, never other browsing windows.
    static void CloseRunningShoal(string profile)
    {
        string query = "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='msedge.exe' OR Name='chrome.exe'";
        bool closedAny = false;
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
        {
            foreach (ManagementObject proc in searcher.Get())
            {
                string cmd = proc["CommandLine"] as string;
                if (cmd == null || cmd.IndexOf(profile, StringComparison.OrdinalIgnoreCase) < 0) continue;
                try { Process.GetProcessById(Convert.ToInt32(proc["ProcessId"])).Kill(); closedAny = true; } catch { }
            }
        }
        if (closedAny) Thread.Sleep(800);   // let the old window release its profile
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
            // Chrome first: on this laptop it is set to the NVIDIA GPU that drives the headset
            // (Windows Settings > Display > Graphics). A browser on the other GPU can't start VR.
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            Path.Combine(local, @"Google\Chrome\Application\chrome.exe"),
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        };
        foreach (string path in candidates)
        {
            if (File.Exists(path)) return path;
        }
        return null;
    }
}

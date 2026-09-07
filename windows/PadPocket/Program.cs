using Microsoft.Win32;

namespace PadPocket;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var instance = new Mutex(true, @"Local\PadPocket.MonitorSwitch", out bool firstInstance);
        if (!firstInstance) return;

        try
        {
            string monitor = "Primary";
            int delay = 500;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--monitor", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    monitor = args[++i];
                else if (args[i].Equals("--wake-delay", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length
                         && int.TryParse(args[++i], out int parsed) && parsed >= 0 && parsed <= 10000)
                    delay = parsed;
                else
                    throw new ArgumentException("Usage: PadPocket.exe [--monitor Primary|All|index|name] [--wake-delay 0..10000]");
            }
            if (string.IsNullOrWhiteSpace(monitor) || monitor.Contains('"'))
                throw new ArgumentException("The monitor selector must be nonempty and cannot contain quotes.");

            using var context = new TrayContext(monitor, delay);
            Application.Run(context);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            MessageBox.Show(error.Message, "Pad Pocket", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupName = "PadPocketMonitorSwitch";
    private readonly MonitorHotkeyWindow hotkeys;
    private readonly NotifyIcon tray;
    private readonly Icon appIcon;
    private readonly ContextMenuStrip menu;
    private readonly string monitor;
    private readonly int delay;
    private bool switching;
    private bool disposed;

    public TrayContext(string monitor, int delay)
    {
        this.monitor = monitor;
        this.delay = delay;
        hotkeys = new MonitorHotkeyWindow();
        menu = new ContextMenuStrip();
        menu.Items.Add("Switch to USB-C", null, async (_, _) => await SwitchInput(2, "USB-C"));
        menu.Items.Add("Switch to DisplayPort", null, async (_, _) => await SwitchInput(3, "DisplayPort"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Show detected monitors", null, (_, _) => ShowMonitors());
        var startup = new ToolStripMenuItem("Start with Windows");
        startup.Click += (_, _) => ToggleStartup(startup);
        menu.Items.Add(startup);
        menu.Opening += (_, _) =>
        {
            try { startup.Checked = IsStartupEnabled(); }
            catch (Exception error) { Status(error.Message, ToolTipIcon.Error); }
        };
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        using (var iconStream = typeof(TrayContext).Assembly.GetManifestResourceStream("PadPocket.AppIcon"))
        using (var bundledIcon = new Icon(iconStream))
            appIcon = (Icon)bundledIcon.Clone();
        tray = new NotifyIcon
        {
            Icon = appIcon,
            Text = "Pad Pocket monitor switch",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += (_, _) => Status($"Listening for Ctrl+Alt+F23/F24. Target: {monitor}");
        hotkeys.HotkeyPressed += async (_, e) => await SwitchInput(e.Id == 1 ? 2u : 3u, e.Id == 1 ? "USB-C" : "DisplayPort");
        // Enumerate on demand: displays may not yet be available at sign-in.
        Status($"Ready. Target: {monitor}");
    }

    private async Task SwitchInput(uint value, string name)
    {
        if (switching || disposed) return;
        switching = true;
        try
        {
            NativeMonitorControl.WakeDisplay();
            await Task.Delay(delay);
            if (disposed) return;
            string changed = NativeMonitorControl.SetInput(monitor, value);
            Status($"Switched {changed} to {name}.");
        }
        catch (Exception error) { Status(error.Message, ToolTipIcon.Error); }
        finally { switching = false; }
    }

    private void ShowMonitors()
    {
        try { MessageBox.Show(NativeMonitorControl.DescribeMonitors(), "Pad Pocket monitors"); }
        catch (Exception error) { Status(error.Message, ToolTipIcon.Error); }
    }

    private string StartupCommand => $"\"{Environment.ProcessPath}\" --monitor \"{monitor}\" --wake-delay {delay}";

    private bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return string.Equals(key?.GetValue(StartupName) as string, StartupCommand, StringComparison.OrdinalIgnoreCase);
    }

    private void ToggleStartup(ToolStripMenuItem item)
    {
        try
        {
            bool enabled = IsStartupEnabled();
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.DeleteValue(StartupName, false);
            else key.SetValue(StartupName, StartupCommand);
            item.Checked = !enabled;
            Status(enabled ? "Start with Windows disabled." : "Start with Windows enabled.");
        }
        catch (Exception error) { Status(error.Message, ToolTipIcon.Error); }
    }

    private void Status(string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (!disposed) tray.ShowBalloonTip(3000, "Pad Pocket", message, icon);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            tray.Visible = false;
            tray.Dispose();
            appIcon.Dispose();
            menu.Dispose();
            hotkeys.Dispose();
        }
        base.Dispose(disposing);
    }
}

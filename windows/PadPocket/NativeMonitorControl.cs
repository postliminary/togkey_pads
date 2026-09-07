using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

public sealed class MonitorHotkeyEventArgs : EventArgs
{
    public int Id { get; private set; }
    public MonitorHotkeyEventArgs(int id) { Id = id; }
}

public sealed class MonitorHotkeyWindow : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_F23 = 0x86;
    private const uint VK_F24 = 0x87;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    public event EventHandler<MonitorHotkeyEventArgs> HotkeyPressed;

    public MonitorHotkeyWindow()
    {
        CreateHandle(new CreateParams());
        uint modifiers = MOD_CONTROL | MOD_ALT | MOD_NOREPEAT;
        if (!RegisterHotKey(Handle, 1, modifiers, VK_F23))
        {
            int error = Marshal.GetLastWin32Error();
            DestroyHandle();
            throw new Win32Exception(error, "Could not register Ctrl+Alt+F23. Exit the old Pad Pocket PowerShell receiver or any other app using this hotkey, then launch Pad Pocket again.");
        }

        if (!RegisterHotKey(Handle, 2, modifiers, VK_F24))
        {
            int error = Marshal.GetLastWin32Error();
            UnregisterHotKey(Handle, 1);
            DestroyHandle();
            throw new Win32Exception(error, "Could not register Ctrl+Alt+F24. Exit the old Pad Pocket PowerShell receiver or any other app using this hotkey, then launch Pad Pocket again.");
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WM_HOTKEY && HotkeyPressed != null)
        {
            HotkeyPressed(this, new MonitorHotkeyEventArgs(message.WParam.ToInt32()));
        }
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            UnregisterHotKey(Handle, 1);
            UnregisterHotKey(Handle, 2);
            DestroyHandle();
        }
    }
}

public static class NativeMonitorControl
{
    private const uint MONITORINFOF_PRIMARY = 0x00000001;
    private const uint WM_SYSCOMMAND = 0x0112;
    private const uint SC_MONITORPOWER = 0xF170;
    private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public int left;
        public int top;
        public int right;
        public int bottom;
        public int workLeft;
        public int workTop;
        public int workRight;
        public int workBottom;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr handle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string description;
    }

    private sealed class MonitorTarget
    {
        public int Index;
        public bool IsPrimary;
        public IntPtr Handle;
        public string Description;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PHYSICAL_MONITOR[] physicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitor(IntPtr monitor);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetVCPFeature(IntPtr monitor, byte code, uint value);

    public static void WakeDisplay()
    {
        SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, new IntPtr(SC_MONITORPOWER), new IntPtr(-1));
    }

    public static string DescribeMonitors()
    {
        List<MonitorTarget> targets = GetTargets();
        try
        {
            return DescribeTargets(targets);
        }
        finally
        {
            DestroyTargets(targets);
        }
    }

    public static string SetInput(string selector, uint input)
    {
        List<MonitorTarget> targets = GetTargets();
        List<string> changed = new List<string>();
        List<string> failed = new List<string>();
        int requestedIndex;
        bool selectByIndex = Int32.TryParse(selector, out requestedIndex);

        try
        {
            foreach (MonitorTarget target in targets)
            {
                bool selected = selector.Equals("All", StringComparison.OrdinalIgnoreCase)
                    || (selector.Equals("Primary", StringComparison.OrdinalIgnoreCase) && target.IsPrimary)
                    || (selectByIndex && target.Index == requestedIndex)
                    || (!selectByIndex
                        && !selector.Equals("Primary", StringComparison.OrdinalIgnoreCase)
                        && !selector.Equals("All", StringComparison.OrdinalIgnoreCase)
                        && target.Description.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0);

                if (!selected) continue;

                if (SetVCPFeature(target.Handle, 0x60, input))
                {
                    changed.Add(target.Description);
                }
                else
                {
                    failed.Add(target.Description + " (Win32 " + Marshal.GetLastWin32Error() + ")");
                }
            }

            if (changed.Count == 0)
            {
                string available = DescribeTargets(targets);
                if (failed.Count > 0)
                {
                    throw new InvalidOperationException("DDC/CI failed for " + String.Join(", ", failed.ToArray()) + ". Available: " + available);
                }
                throw new InvalidOperationException("No monitor matched '" + selector + "'. Available: " + available);
            }

            return String.Join(", ", changed.ToArray());
        }
        finally
        {
            DestroyTargets(targets);
        }
    }

    private static List<MonitorTarget> GetTargets()
    {
        List<MonitorTarget> targets = new List<MonitorTarget>();
        MonitorEnumProc callback = delegate(IntPtr logicalMonitor, IntPtr hdc, IntPtr rect, IntPtr data)
        {
            MONITORINFO info = new MONITORINFO();
            info.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(logicalMonitor, ref info)) return true;

            uint count;
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(logicalMonitor, out count) || count == 0) return true;

            PHYSICAL_MONITOR[] physical = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(logicalMonitor, count, physical)) return true;

            foreach (PHYSICAL_MONITOR monitor in physical)
            {
                MonitorTarget target = new MonitorTarget();
                target.Index = targets.Count;
                target.IsPrimary = (info.flags & MONITORINFOF_PRIMARY) != 0;
                target.Handle = monitor.handle;
                target.Description = String.IsNullOrWhiteSpace(monitor.description) ? "Unnamed monitor" : monitor.description;
                targets.Add(target);
            }
            return true;
        };

        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate displays");
        }
        if (targets.Count == 0)
        {
            throw new InvalidOperationException("Windows did not expose any physical monitors through the DDC/CI API.");
        }
        return targets;
    }

    private static string DescribeTargets(List<MonitorTarget> targets)
    {
        List<string> descriptions = new List<string>();
        foreach (MonitorTarget target in targets)
        {
            descriptions.Add(target.Index + ": " + target.Description + (target.IsPrimary ? " (primary)" : ""));
        }
        return String.Join("; ", descriptions.ToArray());
    }

    private static void DestroyTargets(List<MonitorTarget> targets)
    {
        foreach (MonitorTarget target in targets)
        {
            if (target.Handle != IntPtr.Zero) DestroyPhysicalMonitor(target.Handle);
        }
    }
}

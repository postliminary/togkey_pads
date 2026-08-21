param(
    [string]$ControlMyMonitorPath = "C:\Tools\ControlMyMonitor.exe",
    [string]$MonitorId = "Primary"
)

if (-not (Test-Path -LiteralPath $ControlMyMonitorPath)) {
    throw "ControlMyMonitor was not found at $ControlMyMonitorPath"
}

Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class MonitorHotkeys {
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] public static extern IntPtr GetMessage(out MSG msg, IntPtr h, uint min, uint max);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public IntPtr hwnd; public uint message; public UIntPtr wParam; public IntPtr lParam;
        public uint time; public int x; public int y; }
}
"@

$modControlAlt = 0x0002 -bor 0x0001
$vkF23 = 0x86
$vkF24 = 0x87
$wmHotkey = 0x0312
$hwndBroadcast = [IntPtr]0xffff
$wmSysCommand = 0x0112
$scMonitorPower = [IntPtr]0xF170
$monitorOn = [IntPtr](-1)

if (-not [MonitorHotkeys]::RegisterHotKey([IntPtr]::Zero, 1, $modControlAlt, $vkF23)) {
    throw "Could not register Ctrl+Alt+F23"
}
if (-not [MonitorHotkeys]::RegisterHotKey([IntPtr]::Zero, 2, $modControlAlt, $vkF24)) {
    [void][MonitorHotkeys]::UnregisterHotKey([IntPtr]::Zero, 1)
    throw "Could not register Ctrl+Alt+F24"
}

Write-Host "Listening: Ctrl+Alt+F23 => USB-C (2), Ctrl+Alt+F24 => DisplayPort (3)"
try {
    $message = New-Object MonitorHotkeys+MSG
    while ([MonitorHotkeys]::GetMessage([ref]$message, [IntPtr]::Zero, 0, 0) -ne [IntPtr]::Zero) {
        if ($message.message -eq $wmHotkey) {
            $value = if ($message.wParam.ToUInt32() -eq 1) { 2 } else { 3 }
            [void][MonitorHotkeys]::SendMessage($hwndBroadcast, $wmSysCommand, $scMonitorPower, $monitorOn)
            Start-Sleep -Milliseconds 250
            & $ControlMyMonitorPath /SetValue $MonitorId 60 $value
            Write-Host "$(Get-Date -Format s) set VCP 0x60 to $value"
        }
        [void][MonitorHotkeys]::TranslateMessage([ref]$message)
        [void][MonitorHotkeys]::DispatchMessage([ref]$message)
    }
}
finally {
    [void][MonitorHotkeys]::UnregisterHotKey([IntPtr]::Zero, 1)
    [void][MonitorHotkeys]::UnregisterHotKey([IntPtr]::Zero, 2)
}

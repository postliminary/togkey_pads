# Pad Pocket two-PC BLE broadcast proof of concept

This configuration turns the two Pad Pocket buttons into PC selectors. Each button selects its
Bluetooth profile, changes the status color, and sends the matching monitor-input command to both
connected PCs. The Windows receiver on whichever PC owns that input performs the DDC/CI switch.

## Controls

| Gesture | Result |
| --- | --- |
| Left key, tap | Select PC 1 / BLE profile 0, show solid blue, and select USB-C (`0x60` = `2`) |
| Right key, tap | Select PC 2 / BLE profile 1, show solid green, and select DisplayPort (`0x60` = `3`) |
| Both keys, hold for 3 seconds | Enter the Bluetooth layer |
| Bluetooth layer entered | Solid purple indicates setup mode |
| Bluetooth layer: left key, tap | Select profile 0; solid blue confirms the selection |
| Bluetooth layer: right key, tap | Select profile 1; solid green confirms the selection |
| Bluetooth layer: either key, hold for 500 ms | Clear that profile; its blue/green light breathes while ready to pair |
| Bluetooth layer: both keys, hold for 3 seconds | Return to the default layer |

The RGB indicators turn off after five minutes without a key press and wake on the next press. The
broadcast portion of a PC-selection action does not change the active profile. A missing or
disconnected host is skipped; the connected host still receives the monitor command.

## Build and flash

The manifest pins ZMK v0.3. Build from the ZMK development container with this repository mounted
at `/workspaces/zmk-config`. The repository root is an external ZMK module as well as the location
of the `config` directory, so both CMake arguments below are required.

```sh
cd /workspaces/zmk/app
west build -p always -d build/pad_pocket -b seeeduino_xiao_ble -- \
  -DSHIELD=pad_pocket \
  -DSNIPPET=studio-rpc-usb-uart \
  -DZMK_CONFIG=/workspaces/zmk-config/config \
  -DZMK_EXTRA_MODULES=/workspaces/zmk-config
```

The output is `/workspaces/zmk/app/build/pad_pocket/zephyr/zmk.uf2`. Double-press the XIAO BLE reset button, wait for the
`XIAO-SENSE` USB drive, and copy `zmk.uf2` to it. The drive disconnects when flashing completes.

If bonds from older firmware cause trouble, first flash the `settings_reset` artifact described by
`build.yaml`, then flash `zmk.uf2` again.

## Pair both PCs

1. Enter the Bluetooth layer by holding both keys for three seconds.
2. Hold the left key for at least 500 ms to clear profile 0. Tap it once to select profile 0, then
   pair **Pad Pocket BT** in Windows Bluetooth settings on PC 1.
3. Hold the right key for at least 500 ms to clear profile 1. Tap it once to select profile 1, then
   pair **Pad Pocket BT** on PC 2.
4. Ensure both Windows machines show the pad connected. ZMK can retain both BLE connections even
   though one profile remains the normal active profile.
5. Hold both keys for three seconds to return to the default layer.

After setup, tap the left or right key directly to select that PC and monitor input.

## Windows receiver

The receiver is a .NET 10 Windows Forms tray app in [`windows/PadPocket`](../../windows/PadPocket) at the repository root. It uses the
Windows monitor-control API directly; no PowerShell, VBS, or external monitor utility is needed.

### Build

Download a Windows helper ZIP from the GitHub release assets and extract it, or install the
.NET 10 SDK and run from the repository root:

```powershell
dotnet publish .\windows\PadPocket\PadPocket.csproj -c Release -r win-x64 -o .\windows\PadPocket\bin\publish\win-x64
```

Copy `windows/PadPocket/bin/publish/win-x64/PadPocket.exe` to a permanent folder on each PC.
The single executable includes its .NET runtime. Use `win-arm64` instead of `win-x64` when
publishing for an ARM64 PC. Building requires the SDK; running the published app does not.

### Run and start with Windows

1. Exit the old PowerShell receiver through its tray menu, and disable its scheduled task or
   remove its Startup shortcut to avoid hotkey conflicts.
2. Double-click `PadPocket.exe`. It starts in the notification area with no console window.
3. Right-click its tray icon and enable **Start with Windows**. This registers the current
   executable path and arguments for your account at sign-in, without requiring administrator
   access or Task Scheduler. Uncheck the same option to disable it. If you move the executable,
   launch it from its new location and enable the option again.

Only one app instance runs per Windows session. The tray menu provides **Switch to USB-C**,
**Switch to DisplayPort**, **Show detected monitors**, and **Exit**.

The receiver listens for `Ctrl+Alt+F23` (USB-C, VCP `0x60` value `2`) and `Ctrl+Alt+F24`
(DisplayPort, value `3`). It wakes the local display signal before switching. Enable DDC/CI in
the Planar monitor menu.

The primary monitor is selected by default. To choose a monitor or adjust the wake delay:

```powershell
.\PadPocket.exe --monitor PZN3815Q --wake-delay 1000
```

`--monitor` accepts `Primary`, `All`, a zero-based index from **Show detected monitors**, or a
unique part of a monitor description. `--wake-delay` accepts 0–10000 milliseconds (default 500).
Enable **Start with Windows** after launching with your chosen arguments to save them.

Monitors are enumerated when switching, so unavailable displays at sign-in do not terminate the
receiver. If switching fails, check DDC/CI, the cable, dock, and display driver. The original
[`windows/monitor-switch.ps1`](../../windows/monitor-switch.ps1) remains available as a legacy receiver.

## Simultaneous-delivery test

1. On each PC, run the receiver and confirm the tray icon appears.
2. Use the tray menu on each PC to verify USB-C and DisplayPort switching before testing Bluetooth.
3. Keep both PCs connected over BLE, then tap the left key. The LEDs should turn blue, profile 0
   should become active, and the Planar should switch to USB-C.
4. Tap the right key. The LEDs should turn green, profile 1 should become active, and the Planar
   should switch to DisplayPort.
5. Disconnect either PC and repeat. The connected PC should still receive the broadcast action,
   while firmware logs (when enabled) report that the other profile is not connected.

Only one monitor input is visible at a time, so “simultaneous delivery” means both Windows receivers
observe the same broadcast report; whichever receiver's requested input wins last determines the
visible input.

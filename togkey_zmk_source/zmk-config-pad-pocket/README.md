# Pad Pocket two-PC BLE broadcast proof of concept

This configuration keeps normal ZMK endpoint behavior for ordinary keys and adds two explicit
monitor actions which send the same keyboard report to connected BLE profiles 0 and 1.

## Controls

| Gesture | Result |
| --- | --- |
| Left key, single tap | Volume down on the active profile only |
| Right key, single tap | Volume up on the active profile only |
| Left key, double tap | Broadcast `Ctrl+Alt+F23` (select USB-C, VCP `0x60` = `2`) |
| Right key, double tap | Broadcast `Ctrl+Alt+F24` (select DisplayPort, VCP `0x60` = `3`) |
| Both keys, hold for 3 seconds | Enter the Bluetooth layer |
| Bluetooth layer: left key, tap | Select PC 1 / BLE profile 0 |
| Bluetooth layer: right key, tap | Select PC 2 / BLE profile 1 |
| Bluetooth layer: either key, hold for 500 ms | Clear that key's profile for pairing |
| Bluetooth layer: both keys, hold for 3 seconds | Return to the default layer |

The broadcast behavior does not call `BT_SEL` and does not change the active profile. A missing or
disconnected host is skipped; the connected host still receives the action.

## Build and flash

The manifest follows ZMK `main`, so update and rebuild together when upstream APIs change.

```sh
west init -l config
west update
west build -s zmk/app -d build/pad_pocket -b xiao_ble//zmk -- \
  -DSHIELD=pad_pocket -DSNIPPET=studio-rpc-usb-uart
```

The output is `build/pad_pocket/zephyr/zmk.uf2`. Double-press the XIAO BLE reset button, wait for the
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

For ordinary keys, enter the Bluetooth layer and tap left or right to select the destination PC.

## Windows receiver

Install [ControlMyMonitor](https://www.nirsoft.net/utils/control_my_monitor.html) on both PCs and run
`windows/monitor-switch.ps1` on sign-in. By default it targets the primary monitor; pass the Planar
PZN3815Q monitor ID reported by ControlMyMonitor if it is not primary:

```powershell
powershell -ExecutionPolicy Bypass -File .\windows\monitor-switch.ps1 \
  -ControlMyMonitorPath C:\Tools\ControlMyMonitor.exe -MonitorId Primary
```

The receiver registers the uncommon hotkeys `Ctrl+Alt+F23` and `Ctrl+Alt+F24`, wakes the local
display signal, then sets DDC/CI VCP `0x60` to `2` (USB-C) or `3` (DisplayPort). Enable DDC/CI in the
Planar monitor menu.

## Simultaneous-delivery test

1. On each PC, run the receiver and confirm its console says both hotkeys are registered.
2. Keep both PCs connected over BLE. Select either profile and verify single-tap volume changes only
   that PC; switch profiles and repeat. This confirms ordinary active-profile routing is preserved.
3. Double-tap the left key. Both receiver consoles should log `Ctrl+Alt+F23`; the destination signal
   wakes and the Planar switches to USB-C (`0x60` = `2`).
4. Double-tap the right key. Both consoles should log `Ctrl+Alt+F24`; the destination signal wakes
   and the Planar switches to DisplayPort (`0x60` = `3`).
5. Disconnect either PC and repeat. The connected PC should still act, while firmware logs (when
   enabled) report that the other profile is not connected.

Only one monitor input is visible at a time, so “simultaneous delivery” means both Windows receivers
observe the same broadcast report; whichever receiver's requested input wins last determines the
visible input.

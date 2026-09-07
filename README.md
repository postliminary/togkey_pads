# togkey_pads
Source Code for our TogKey Macropads

## Windows helper

The Pad Pocket monitor-switching tray app lives in [windows/PadPocket](windows/PadPocket).
See the [receiver instructions](togkey_zmk_source/zmk-config-pad-pocket/README.md#windows-receiver)
for building, setup, and usage.

Publishing a GitHub release builds self-contained .NET 10 executables for Windows x64 and
ARM64 and attaches `PadPocket-win-x64.zip` and `PadPocket-win-arm64.zip` to that release.
Extract the ZIP for your PC and run `PadPocket.exe`; no separate .NET runtime is required.
The release tag must include `.github/workflows/release-windows.yml`.

## ZMK firmware

ZMK supports [GitHub Actions firmware builds](https://zmk.dev/docs/user-setup) using its
reusable `zmkfirmware/zmk/.github/workflows/build-user-config.yml` workflow. This repository's
configurations and build matrices live under `togkey_zmk_source`; a firmware workflow would
need to select those nested paths and include the Pad Pocket custom module. Firmware release
automation is not configured here yet. Generated `.uf2` files are ignored; see the
[Pad Pocket build instructions](togkey_zmk_source/zmk-config-pad-pocket/README.md#build-and-flash).

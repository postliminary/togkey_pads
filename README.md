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

The [Pad Pocket firmware workflow](.github/workflows/build-pad-pocket.yml) builds only the
custom Pad Pocket configuration and its settings-reset firmware using ZMK v0.3. It runs on
pushes and pull requests affecting that configuration or workflow, manually from Actions,
and when a GitHub release is published. Download the `pad-pocket-firmware` Actions artifact,
or the `pad-pocket.uf2` and `pad-pocket-settings-reset.uf2` assets attached to a release.
The release tag must include the firmware workflow. Generated `.uf2` files are ignored; see the
[Pad Pocket build instructions](togkey_zmk_source/zmk-config-pad-pocket/README.md#build-and-flash).

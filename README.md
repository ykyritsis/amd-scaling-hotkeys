# AMD Scaling Hotkeys

A small Windows tray utility for switching AMD Radeon GPU scaling modes with global hotkeys. It uses AMD's ADLX display API and does not modify application files.

## Features

- Center, Stretch (Full panel), Preserve aspect ratio, and cycle modes
- Global hotkeys: `Ctrl+Alt+1`, `Ctrl+Alt+2`, `Ctrl+Alt+3`, `Ctrl+Alt+S`
- Notification-area menu for monitor selection and manual switching
- Embedded tray icon; no console window in the tray build
- Automatically enables GPU Scaling before changing the mode

## Requirements

- Windows 10/11 64-bit
- Any AMD Radeon driver that installs the 64-bit ADLX interface (`amdadlx64.dll`)
- A display that exposes scaling controls through the driver

The utility is not tied to a particular Radeon model. Very old legacy drivers
that do not provide ADLX are unsupported; update to the current Radeon driver
for the target PC. A copied `monitor.txt` is automatically ignored when its
display ID is not present on the new PC.

## Run

Double-click `Start AMD Scaling Hotkeys.cmd`. The program lives in the notification area. Right-click its orange **A** icon to select a monitor or exit.

For an application to use the selected GPU scaling mode, it normally needs to
run in **exclusive fullscreen** at a resolution different from the monitor's
native resolution. Borderless fullscreen and windowed applications are scaled
by the Windows desktop compositor, so the driver mode may have no visible
effect. Switching modes can briefly flicker the display.

If more than one monitor is attached, use the tray menu or run `AmdScaling.exe list` to identify a display ID.

## Build

Run `build.cmd` with the .NET Framework C# compiler. It produces a diagnostic console build (`AmdScaling.exe`) and a windowless tray build (`AmdScalingTray.exe`).

## License

MIT. AMD ADLX is an AMD API; this utility is an independent client and is not affiliated with AMD.

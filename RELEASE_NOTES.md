# AMD Scaling Hotkeys v1.0.0

Initial global release for Windows x64 applications.

## Highlights

- Global hotkeys for Center, Stretch (Full panel), and Preserve aspect ratio
- Notification-area control with an embedded orange tray icon
- AMD ADLX integration with monitor selection
- Console and windowless tray builds

## Requirements

Windows 10/11 x64 and an AMD Radeon driver exposing `amdadlx64.dll`. For the
selected mode to affect an application, use exclusive fullscreen at a
non-native resolution; borderless/windowed apps are handled by the desktop
compositor.

The utility is model-independent. It uses the installed AMD ADLX interface
and automatically reselects a single monitor when moved to another PC.

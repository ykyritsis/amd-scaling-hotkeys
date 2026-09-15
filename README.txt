AMD Scaling Hotkeys
===================

This utility changes the AMD GPU scaling mode for the selected display.
It uses AMD's installed ADLX driver interface; it does not alter application files.

Run Start AMD Scaling Hotkeys.cmd (or double-click AmdScalingTray.exe).
The program stays in the notification area.

AmdScaling.exe is the console/diagnostic build. AmdScalingTray.exe is the
windowless tray build.

Hotkeys
-------
Ctrl+Alt+1  Center
Ctrl+Alt+2  Stretch (Full panel)
Ctrl+Alt+3  Preserve aspect ratio
Ctrl+Alt+S  Cycle Center -> Stretch -> Preserve aspect ratio

Right-click the notification-area icon to choose a monitor or exit.

Command line
------------
AmdScaling.exe list
AmdScaling.exe center
AmdScaling.exe stretch
AmdScaling.exe aspect
AmdScaling.exe cycle
AmdScaling.exe tray

If more than one monitor is attached, use the ID shown by list:
AmdScaling.exe aspect --display DISPLAY_ID_FROM_LIST

Notes
-----
The program enables GPU Scaling automatically before setting a mode.
If a hotkey is already used by another program, use the tray menu instead.
Keep AmdScaling.exe and Start AMD Scaling Hotkeys.cmd together.

For a mode to affect an application, it normally must use exclusive fullscreen
at a non-native resolution. Borderless and windowed applications are scaled by
the Windows desktop compositor. If list reports Center after a mode command,
the display or driver did not accept that mode.

MSI AFTERBURNER OVERCLOCK SYNC (MsiAfterburnerSync)
===================================================

PURPOSE:
- Fixes MSI Center resetting your overclock when switching user scenarios / power modes (e.g. Extreme Performance <-> Balanced).
- Standalone, lightweight, and 100% anti-cheat safe.
- DOES NOT touch NVAPI, GPU VBIOS, or game memory.
- Consumes 0% CPU (uses Windows kernel registry change notification).
- Directly triggers MSI Afterburner's official CLI (-Profile1..5) to reapply your overclock profile.

HOW TO USE:
1. Double-click `run.cmd` or `dist\MsiAfterburnerSync.exe` to launch.
2. Select your MSI Afterburner profile slot (e.g., Profile 1).
3. Set your preferred re-apply delay (1 to 10 seconds, default: 1s).
4. Click "▶ Apply Profile Now (Test)" to verify Afterburner applies the profile.
5. Click "Save Settings".
6. Check "Start with Windows at logon" or run `install-autostart.cmd` to run silently in the system tray at boot.

HOW IT WORKS:
When MSI Center switches power modes or scenarios, it updates its registry state.
MsiAfterburnerSync detects this event with 0% CPU, waits your configured delay (e.g. 1 second), and automatically tells MSI Afterburner to apply your saved overclock profile slot.
Since NvpwrControl is NOT kept running in the background, your games will not crash or conflict with anti-cheat.

@echo off 
CiTool.exe --update-policy "C:\NvpwrCerts\Policy\{959A0F15-8985-4551-A208-5FFE9EDB3A70}.cip"
CiTool.exe --refresh 
echo.
echo ========================================================================
echo [OK] Signed kernel-only policy deployed successfully!
echo.
echo CRITICAL STEP FOR SECURE BOOT:
echo Because this is a SIGNED policy tied to your UEFI Secure Boot keys,
echo Windows bootloader (winload.efi) must verify it at startup.
echo.
echo Please RESTART YOUR PC now, then run NvpwrControl.exe as administrator.
echo ========================================================================
pause

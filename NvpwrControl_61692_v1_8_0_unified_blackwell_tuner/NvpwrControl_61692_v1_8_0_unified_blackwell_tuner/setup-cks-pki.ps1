#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Step 1 of 3: Generate Custom Kernel Signers PKI and install driver cert into Windows.

.DESCRIPTION
    Creates:
      PK.cer / PK.pfx    -- UEFI Platform Key (root of Secure Boot trust)
      KEK.cer / KEK.pfx  -- UEFI Key Exchange Key
      db.cer / db.pfx    -- Driver signing cert (signs Nvpwr.sys AND SiPolicy.p7b)

    Installs db.cer into:
      Cert:\LocalMachine\Root              (Trusted Root CA)
      Cert:\LocalMachine\TrustedPublisher  (required for kernel driver loading)

    All .cer files are saved to C:\NvpwrCerts\ -- copy to USB for BIOS Key Management.
#>

$ErrorActionPreference = 'Stop'
$outDir = 'C:\NvpwrCerts'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host ''
Write-Host '======================================================' -ForegroundColor Cyan
Write-Host '  NVPWR Custom Kernel Signers -- PKI Generation       ' -ForegroundColor Cyan
Write-Host '======================================================' -ForegroundColor Cyan
Write-Host ''

Write-Host 'Enter a password to protect your .pfx key backups (remember this!)' -ForegroundColor Yellow
$pfxPwd = Read-Host -AsSecureString 'PFX password'

# 1. Platform Key (PK)
Write-Host '[1/3] Generating Platform Key (PK)...' -ForegroundColor Green
$pkCert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -match 'NvpwrPlatformKey' } | Select-Object -First 1
if (-not $pkCert) {
    $pkCert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject 'CN=NvpwrPlatformKey, O=NvpwrLocal' `
        -KeyUsage DigitalSignature `
        -FriendlyName 'Nvpwr Platform Key' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears(10)
    Write-Host "  Created: $($pkCert.Subject)" -ForegroundColor Gray
} else {
    Write-Host "  Reusing existing: $($pkCert.Subject)" -ForegroundColor Gray
}
Export-Certificate -Cert $pkCert -FilePath "$outDir\PK.cer"  -Force | Out-Null
Export-PfxCertificate -Cert $pkCert -FilePath "$outDir\PK.pfx" -Password $pfxPwd -Force | Out-Null

# 2. Key Exchange Key (KEK)
Write-Host '[2/3] Generating Key Exchange Key (KEK)...' -ForegroundColor Green
$kekCert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -match 'NvpwrKEK' } | Select-Object -First 1
if (-not $kekCert) {
    $kekCert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject 'CN=NvpwrKEK, O=NvpwrLocal' `
        -KeyUsage DigitalSignature `
        -FriendlyName 'Nvpwr Key Exchange Key' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears(10)
    Write-Host "  Created: $($kekCert.Subject)" -ForegroundColor Gray
} else {
    Write-Host "  Reusing existing: $($kekCert.Subject)" -ForegroundColor Gray
}
Export-Certificate -Cert $kekCert -FilePath "$outDir\KEK.cer" -Force | Out-Null
Export-PfxCertificate -Cert $kekCert -FilePath "$outDir\KEK.pfx" -Password $pfxPwd -Force | Out-Null

# 3. Driver Signing Cert (db) -- this is the one that signs Nvpwr.sys
Write-Host '[3/3] Generating Driver Signing Certificate (db)...' -ForegroundColor Green
$dbCert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -match 'NvpwrDriverSigning' } | Select-Object -First 1
if (-not $dbCert) {
    $dbCert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject 'CN=NvpwrDriverSigning, O=NvpwrLocal' `
        -KeyUsage DigitalSignature `
        -FriendlyName 'Nvpwr Driver Signing' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears(10)
    Write-Host "  Created: $($dbCert.Subject)" -ForegroundColor Gray
} else {
    Write-Host "  Reusing existing: $($dbCert.Subject)" -ForegroundColor Gray
}
Export-Certificate -Cert $dbCert -FilePath "$outDir\db.cer"  -Force | Out-Null
Export-PfxCertificate -Cert $dbCert -FilePath "$outDir\db.pfx" -Password $pfxPwd -Force | Out-Null

# Install db cert into Windows stores (needed for driver loading)
Write-Host ''
Write-Host 'Installing db.cer into Windows certificate stores...' -ForegroundColor Green
Import-Certificate -FilePath "$outDir\db.cer" -CertStoreLocation 'Cert:\LocalMachine\Root'            | Out-Null
Import-Certificate -FilePath "$outDir\db.cer" -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null
Write-Host '  Installed into Trusted Root CA and Trusted Publishers.' -ForegroundColor Gray

Write-Host ''
Write-Host '======================================================' -ForegroundColor Cyan
Write-Host '  Done!                                               ' -ForegroundColor Cyan
Write-Host '======================================================' -ForegroundColor Cyan
Write-Host ''
Write-Host 'Files written to C:\NvpwrCerts\:' -ForegroundColor Yellow
Get-ChildItem $outDir | Select-Object Name, @{N='Size';E={$_.Length}} | Format-Table -AutoSize
Write-Host ''
Write-Host 'NEXT STEPS:' -ForegroundColor Yellow
Write-Host '  1. Copy PK.cer, KEK.cer, db.cer to the ROOT of a FAT32 USB drive' -ForegroundColor White
Write-Host '  2. Run build.ps1 to rebuild Nvpwr.sys signed with NvpwrDriverSigning cert' -ForegroundColor White
Write-Host '  3. Run setup-cks-deploy.ps1 (creates + deploys SiPolicy.p7b to EFI)' -ForegroundColor White
Write-Host '  4. In BIOS Key Management: Append db.cer, Append KEK.cer, Replace PK.cer' -ForegroundColor White
Write-Host '  5. bcdedit /set testsigning off  &&  reboot' -ForegroundColor White
Write-Host ''

#Requires -RunAsAdministrator
# NVPWR Custom Kernel Signers -- SiPolicy Deploy Script
# Run as Administrator after setup-cks-pki.ps1 has been run.
# Compatible with Windows PowerShell 5.1+

$ErrorActionPreference = "Stop"

$certDir   = "C:\NvpwrCerts"
$policyDir = "C:\NvpwrCerts\Policy"
$dbCer     = Join-Path $certDir "db.cer"

New-Item -ItemType Directory -Force -Path $policyDir | Out-Null

if (-not (Test-Path $dbCer)) {
    throw "db.cer not found at $dbCer. Run setup-cks-pki.ps1 first."
}

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  NVPWR CKS -- SiPolicy Creation and EFI Deployment  " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

# [1/4] Install db.cer into Windows certificate stores
Write-Host "[1/4] Installing db.cer into Windows certificate stores..." -ForegroundColor Cyan
certutil -addstore Root             $dbCer
certutil -addstore TrustedPublisher $dbCer
Write-Host "  Done." -ForegroundColor Green

# [2/4] Build Code Integrity policy XML
Write-Host ""
Write-Host "[2/4] Building Code Integrity policy XML..." -ForegroundColor Cyan

$policyXml = Join-Path $policyDir "NvpwrCIPolicy.xml"
$template  = "C:\Windows\schemas\CodeIntegrity\ExamplePolicies\AllowMicrosoft.xml"

if (Test-Path $template) {
    Copy-Item $template $policyXml -Force
    Write-Host "  Using AllowMicrosoft base template." -ForegroundColor Gray
} else {
    Write-Host "  AllowMicrosoft.xml not found -- writing minimal policy." -ForegroundColor Yellow
    $x  = '<?xml version="1.0" encoding="utf-8"?>'
    $x += '<SiPolicy xmlns="urn:schemas-microsoft-com:sipolicy">'
    $x += '<VersionEx>10.0.0.0</VersionEx>'
    $x += '<PolicyTypeID>{A244370E-44C9-4C06-B551-F6016E563076}</PolicyTypeID>'
    $x += '<PlatformID>{2E07F7E4-194C-4D20-B096-2573F66476B9}</PlatformID>'
    $x += '<Rules>'
    $x += '<Rule><Option>Enabled:Unsigned System Integrity Policy</Option></Rule>'
    $x += '<Rule><Option>Enabled:Advanced Boot Options Menu</Option></Rule>'
    $x += '</Rules>'
    $x += '<EKUs/><FileRules/><Signers/>'
    $x += '<SigningScenarios>'
    $x += '<SigningScenario Value="131" ID="ID_SIGNINGSCENARIO_KMCI" FriendlyName="Kernel Mode"><ProductSigners/></SigningScenario>'
    $x += '<SigningScenario Value="12" ID="ID_SIGNINGSCENARIO_UMCI" FriendlyName="User Mode"><ProductSigners/></SigningScenario>'
    $x += '</SigningScenarios>'
    $x += '<UpdatePolicySigners/><CiSigners/><HvciOptions>0</HvciOptions>'
    $x += '</SiPolicy>'
    [System.IO.File]::WriteAllText($policyXml, $x, [System.Text.Encoding]::UTF8)
}

Add-SignerRule -FilePath $policyXml -CertificatePath $dbCer -Kernel -User -Update
Write-Host "  Added NvpwrDriverSigning cert as trusted kernel signer." -ForegroundColor Gray

# Option 6: unsigned policy allowed (no need to sign .p7b when HVCI is off)
Set-RuleOption -FilePath $policyXml -Option 6
# Remove HVCI flag if present
Set-RuleOption -FilePath $policyXml -Option 14 -Delete -ErrorAction SilentlyContinue
Write-Host "  Policy XML ready." -ForegroundColor Green

# [3/4] Convert XML to binary SiPolicy.p7b
Write-Host ""
Write-Host "[3/4] Converting policy to binary SiPolicy.p7b..." -ForegroundColor Cyan
$binPolicy = Join-Path $policyDir "SiPolicy.p7b"
ConvertFrom-CIPolicy -XmlFilePath $policyXml -BinaryFilePath $binPolicy
Write-Host "  Binary written: $binPolicy" -ForegroundColor Green

# [4/4] Deploy SiPolicy.p7b to the EFI boot partition
Write-Host ""
Write-Host "[4/4] Deploying SiPolicy.p7b to EFI boot partition..." -ForegroundColor Cyan

$efiMount = "Z:"
try { mountvol $efiMount /D 2>$null } catch {}

mountvol $efiMount /S
Start-Sleep -Milliseconds 1000

$efiDir = "Z:\EFI\Microsoft\Boot"
if (-not (Test-Path $efiDir)) {
    mountvol $efiMount /D
    throw "EFI boot directory not found at $efiDir. Ensure this is a UEFI system."
}

$destFile = Join-Path $efiDir "SiPolicy.p7b"
$backFile = Join-Path $efiDir "SiPolicy.p7b.bak"

if (Test-Path $destFile) {
    Copy-Item $destFile $backFile -Force
    Write-Host "  Backed up existing SiPolicy.p7b -> SiPolicy.p7b.bak" -ForegroundColor Gray
}

Copy-Item $binPolicy $efiDir -Force
Write-Host "  Deployed to $destFile" -ForegroundColor Green

# Also copy certificates to Z:\Certs so BIOS can read them directly from the internal SSD
$efiCertDir = "Z:\Certs"
New-Item -ItemType Directory -Force -Path $efiCertDir | Out-Null
Get-ChildItem $certDir -File | Where-Object { $_.Extension -in @('.cer', '.esl', '.der', '.crt', '.bin') } | ForEach-Object {
    Copy-Item $_.FullName $efiCertDir -Force
}
Write-Host "  Copied certs (.cer, .esl, .der, .bin) to $efiCertDir (internal SSD EFI partition)" -ForegroundColor Green

mountvol $efiMount /D

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  ALL AUTOMATED STEPS COMPLETE!                                " -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "REMAINING MANUAL STEPS:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  1. Copy these 3 files to the ROOT of a FAT32 USB drive:" -ForegroundColor White
Write-Host "       C:\NvpwrCerts\PK.cer" -ForegroundColor Cyan
Write-Host "       C:\NvpwrCerts\KEK.cer" -ForegroundColor Cyan
Write-Host "       C:\NvpwrCerts\db.cer" -ForegroundColor Cyan
Write-Host ""
Write-Host "  2. Reboot into MSI BIOS (Del/F2) > Security > Secure Boot > Key Management:" -ForegroundColor White
Write-Host "       db  -> Append  -> select db.cer  from USB" -ForegroundColor Cyan
Write-Host "       KEK -> Append  -> select KEK.cer from USB" -ForegroundColor Cyan
Write-Host "       PK  -> Set New -> select PK.cer  from USB" -ForegroundColor Cyan
Write-Host "     Save and Exit (F10)" -ForegroundColor White
Write-Host ""
Write-Host "  3. Back in Windows (elevated PowerShell or CMD):" -ForegroundColor White
Write-Host "       bcdedit /set testsigning off" -ForegroundColor Cyan
Write-Host "       shutdown /r /t 0" -ForegroundColor Cyan
Write-Host ""
Write-Host "  After reboot: Secure Boot ACTIVE, Test Mode OFF, Nvpwr.sys loads!" -ForegroundColor Green
Write-Host ""

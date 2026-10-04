$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$outDir = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outExe = Join-Path $outDir 'NvpwrControl.exe'

$candidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

$csc = $null
foreach ($p in $candidates) {
    if (Test-Path -LiteralPath $p) { $csc = $p; break }
}

if (-not $csc) {
    $cmd = Get-Command csc.exe -ErrorAction SilentlyContinue
    if ($cmd) { $csc = $cmd.Source }
}

if (-not $csc) {
    throw 'C# compiler csc.exe was not found. Install/enable .NET Framework 4.x or Visual Studio 2022.'
}

$src = Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName }
if (-not $src) { throw 'No source files found.' }

$targetExe = $outExe
if (Test-Path -LiteralPath $outExe) {
    try {
        $stream = [System.IO.File]::Open($outExe, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $stream.Close()
        $stream.Dispose()
    } catch {
        $targetExe = Join-Path $outDir 'NvpwrControl_Universal.exe'
        Write-Warning "NvpwrControl.exe is currently running. Building to NvpwrControl_Universal.exe instead."
    }
}

$args = @(
    '/nologo',
    '/target:winexe',
    '/platform:x64',
    '/optimize+',
    '/debug:pdbonly',
    "/out:$targetExe",
    "/win32manifest:$PSScriptRoot\app.manifest",
    "/win32icon:$PSScriptRoot\assets\NvpwrControl.ico",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Management.dll'
) + $src

Write-Host "Compiler: $csc"
& $csc @args
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
Write-Host "Build successful: $targetExe"

foreach ($doc in @('README_RU.md','COMPATIBILITY.md','KNOWN_PROFILE.txt','SOURCE_AUDIT.md','CHANGELOG_v2.4.0.md','CHANGELOG_v2.4.2.md','VERSION.txt')) {
    $srcDoc = Join-Path $PSScriptRoot $doc
    if (Test-Path -LiteralPath $srcDoc) { Copy-Item -LiteralPath $srcDoc -Destination $outDir -Force }
}

$toolsSrc = Join-Path $PSScriptRoot 'tools'
if (Test-Path -LiteralPath $toolsSrc) {
    $toolsDst = Join-Path $outDir 'tools'
    New-Item -ItemType Directory -Path $toolsDst -Force | Out-Null
    Copy-Item -Path (Join-Path $toolsSrc '*') -Destination $toolsDst -Recurse -Force
}
Write-Host ""
Write-Host "Built: $outExe" -ForegroundColor Green

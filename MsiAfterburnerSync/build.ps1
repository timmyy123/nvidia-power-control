$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$outDir = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outExe = Join-Path $outDir 'MsiAfterburnerSync.exe'

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
    throw 'C# compiler csc.exe was not found. Install/enable .NET Framework 4.x.'
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
        $targetExe = Join-Path $outDir 'MsiAfterburnerSync_new.exe'
        Write-Warning "MsiAfterburnerSync.exe is currently running. Building to MsiAfterburnerSync_new.exe instead."
    }
}

$manifestArg = $null
if (Test-Path (Join-Path $PSScriptRoot 'app.manifest')) {
    $manifestArg = "/win32manifest:$PSScriptRoot\app.manifest"
}

$compilerArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:x64',
    '/optimize+',
    '/debug:pdbonly',
    "/out:$targetExe",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Management.dll',
    '/reference:Microsoft.Win32.TaskScheduler.dll'
) + $src

if ($manifestArg) { $compilerArgs = @($manifestArg) + $compilerArgs }

# TaskScheduler dll is optional – filter it if not present
$tsDll = Join-Path $outDir 'Microsoft.Win32.TaskScheduler.dll'
if (-not (Test-Path $tsDll)) {
    $compilerArgs = $compilerArgs | Where-Object { $_ -ne '/reference:Microsoft.Win32.TaskScheduler.dll' }
}

Write-Host "Compiler: $csc"
& $csc @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
Write-Host "Build successful: $targetExe" -ForegroundColor Green

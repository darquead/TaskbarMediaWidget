$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path -LiteralPath $csc)) {
    throw "C# compiler not found in the .NET Framework folder."
}

$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Path $dist -Force | Out-Null

$arguments = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/codepage:65001",
    "/win32manifest:$root\app.manifest",
    "/out:$dist\TaskbarMediaWidget.exe",
    "$root\Config.cs",
    "$root\MediaEngine.cs",
    "$root\AppVolumeManager.cs",
    "$root\WidgetForm.cs",
    "$root\TrayContext.cs",
    "$root\Program.cs"
)

& $csc $arguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$distFonts = Join-Path $dist "fonts"
if (-not (Test-Path -LiteralPath $distFonts)) {
    New-Item -ItemType Directory -Path $distFonts -Force | Out-Null
}
if (Test-Path "$root\fonts") {
    Copy-Item "$root\fonts\*" "$distFonts\" -Force
}

Write-Host "Successfully built: $dist\TaskbarMediaWidget.exe"

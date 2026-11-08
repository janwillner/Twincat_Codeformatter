<#
.SYNOPSIS
    Builds and tests everything: core library, tests, CLI (tcfmt) and the VSIX for TcXaeShell.

.EXAMPLE
    .\build.ps1                               # test + build CLI + build VSIX (Release)
    .\build.ps1 -SkipTests
    .\build.ps1 -Version 1.2.0 -Package       # versioned build + artifacts\TcFormatter-1.2.0.zip
#>
param(
    [switch] $SkipTests,
    # Version stamped into the assemblies and the VSIX manifest, e.g. 1.2.0 or 1.2.0-beta.1
    [string] $Version,
    # also create artifacts\TcFormatter-<version>.zip (VSIX, tcfmt, install.ps1, presets, README)
    [switch] $Package
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$versionArgs = @()
if ($Version) {
    if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "Invalid version '$Version' (expected e.g. 1.2.0 or 1.2.0-beta.1)." }
    $versionArgs = @("-p:Version=$Version")
}

if (-not $SkipTests) {
    dotnet test tests\TcFormatter.Core.Tests -c Release @versionArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (Test-Path artifacts\cli) { Remove-Item artifacts\cli -Recurse -Force }
dotnet publish src\TcFormatter.Cli -c Release -o artifacts\cli @versionArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# The VSIX needs the full MSBuild of Visual Studio (VS SDK tasks run on .NET Framework).
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild of Visual Studio not found (install the "Visual Studio extension development" workload).' }

# The VSIX manifest carries its own version (numeric only). Stamp it for this build and restore it afterwards.
$manifest = Join-Path $PSScriptRoot 'src\TcFormatter.Vsix\source.extension.vsixmanifest'
$manifestOriginal = [System.IO.File]::ReadAllBytes($manifest)
try {
    if ($Version) {
        $numericVersion = ($Version -split '-')[0]
        $text = [System.IO.File]::ReadAllText($manifest)
        $text = [regex]::Replace($text, '(<Identity\b[^>]*\bVersion=")[^"]*(")', "`${1}$numericVersion`${2}")
        [System.IO.File]::WriteAllText($manifest, $text)
    }
    # the VS SDK does not notice a changed manifest version incrementally: rebuild when stamping one
    $target = if ($Version) { '/t:Rebuild' } else { '/t:Build' }
    & $msbuild src\TcFormatter.Vsix\TcFormatter.Vsix.csproj /restore $target /p:Configuration=Release /v:minimal /nologo @versionArgs
    if ($LASTEXITCODE -ne 0) { throw "VSIX build failed (MSBuild exit code $LASTEXITCODE)." }
}
finally {
    [System.IO.File]::WriteAllBytes($manifest, $manifestOriginal)
}

New-Item -ItemType Directory -Force artifacts | Out-Null
Copy-Item src\TcFormatter.Vsix\bin\Release\net472\TcFormatter.Vsix.vsix artifacts\TcFormatter.vsix -Force

if ($Package) {
    $name = if ($Version) { "TcFormatter-$Version" } else { 'TcFormatter' }
    $staging = Join-Path $PSScriptRoot "artifacts\package\$name"
    $zip = Join-Path $PSScriptRoot "artifacts\$name.zip"
    if (Test-Path artifacts\package) { Remove-Item artifacts\package -Recurse -Force }
    if (Test-Path $zip) { Remove-Item $zip -Force }

    New-Item -ItemType Directory -Force $staging | Out-Null
    Copy-Item artifacts\TcFormatter.vsix $staging
    Copy-Item install.ps1, README.md $staging
    Copy-Item presets $staging -Recurse
    New-Item -ItemType Directory -Force (Join-Path $staging 'tcfmt') | Out-Null
    Get-ChildItem artifacts\cli -Exclude *.pdb | Copy-Item -Destination (Join-Path $staging 'tcfmt')

    # entry names written explicitly: under Windows PowerShell 5.1 both Compress-Archive and
    # ZipFile.CreateFromDirectory write '\' into them, which breaks other unzip tools
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $root = Split-Path $staging -Parent
    $archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem $staging -Recurse -File) {
            $entry = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
    Write-Host "Package: $zip"
}

Write-Host ''
Write-Host 'Artifacts:'
Write-Host '  artifacts\cli\tcfmt.exe        command line tool'
Write-Host '  artifacts\TcFormatter.vsix     extension (install with .\install.ps1 -Vsix artifacts\TcFormatter.vsix)'

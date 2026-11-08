<#
.SYNOPSIS
    Builds and tests everything: core library, tests, CLI (tcfmt) and the VSIX for TcXaeShell.

.EXAMPLE
    .\build.ps1             # test + build CLI + build VSIX (Release)
    .\build.ps1 -SkipTests
#>
param([switch] $SkipTests)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not $SkipTests) {
    dotnet test tests\TcFormatter.Core.Tests -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

dotnet publish src\TcFormatter.Cli -c Release -o artifacts\cli
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# The VSIX needs the full MSBuild of Visual Studio (VS SDK tasks run on .NET Framework).
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild of Visual Studio not found (install the "Visual Studio extension development" workload).' }

& $msbuild src\TcFormatter.Vsix\TcFormatter.Vsix.csproj /restore /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force artifacts | Out-Null
Copy-Item src\TcFormatter.Vsix\bin\Release\net472\TcFormatter.Vsix.vsix artifacts\TcFormatter.vsix -Force
Write-Host ''
Write-Host 'Artifacts:'
Write-Host '  artifacts\cli\tcfmt.exe        command line tool'
Write-Host '  artifacts\TcFormatter.vsix     extension (install with .\install.ps1 -Vsix artifacts\TcFormatter.vsix)'

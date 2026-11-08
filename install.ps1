<#
.SYNOPSIS
    Installs (or removes) the TcFormatter extension in TcXaeShell 4024 and/or 4026.

.DESCRIPTION
    The TcXaeShell is an isolated Visual Studio shell. Its VSIXInstaller does not work stand-alone, so the
    extension is installed like Beckhoff's own extensions: the VSIX is extracted into
    <TcXaeShell>\Common7\IDE\Extensions\TcFormatter and the shell is told to rebuild its extension cache.

      TwinCAT 3.1.4026: C:\Program Files\Beckhoff\TcXaeShell            (64 bit, VS 2022 based)
      TwinCAT 3.1.4024: C:\Program Files (x86)\Beckhoff\TcXaeShell      (32 bit, VS 2017/2019 based)

    Close all TcXaeShell instances first. Requires administrator rights (the script asks for them).

.EXAMPLE
    .\install.ps1                      # install the built VSIX into every TcXaeShell found
    .\install.ps1 -Vsix .\TcFormatter.Vsix.vsix
    .\install.ps1 -Uninstall
#>
param(
    [string] $Vsix,
    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'

# default: the VSIX next to this script (release zip), otherwise the local build output
if (-not $Vsix) {
    $Vsix = Join-Path $PSScriptRoot 'TcFormatter.vsix'
    if (-not (Test-Path $Vsix)) {
        $Vsix = Join-Path $PSScriptRoot 'src\TcFormatter.Vsix\bin\Release\net472\TcFormatter.Vsix.vsix'
    }
}

# the elevated copy of this script starts in System32: pass an absolute path on
if (-not [System.IO.Path]::IsPathRooted($Vsix)) {
    $Vsix = Join-Path (Get-Location).Path $Vsix
}

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-Vsix', "`"$Vsix`"")
    if ($Uninstall) { $arguments += '-Uninstall' }
    $process = Start-Process powershell.exe -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    exit $process.ExitCode
}

$shells = @(
    'C:\Program Files\Beckhoff\TcXaeShell\Common7\IDE',
    'C:\Program Files (x86)\Beckhoff\TcXaeShell\Common7\IDE'
) | Where-Object { Test-Path (Join-Path $_ 'TcXaeShell.exe') }

if (-not $shells) {
    Write-Error 'No TcXaeShell installation found.'
    exit 1
}

if (Get-Process TcXaeShell -ErrorAction SilentlyContinue) {
    Write-Error 'Close all TcXaeShell instances before installing.'
    exit 1
}

if (-not $Uninstall -and -not (Test-Path $Vsix)) {
    Write-Error "VSIX not found: $Vsix (build it with .\build.ps1)"
    exit 1
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($ide in $shells) {
    $target = Join-Path $ide 'Extensions\TcFormatter'
    if (Test-Path $target) {
        Remove-Item $target -Recurse -Force
    }
    if (-not $Uninstall) {
        [System.IO.Compression.ZipFile]::ExtractToDirectory($Vsix, $target)
        # files that only belong to the VSIX container
        Remove-Item -LiteralPath (Join-Path $target '[Content_Types].xml') -Force -ErrorAction SilentlyContinue
        Write-Host "Installed TcFormatter into $target"
    } else {
        Write-Host "Removed TcFormatter from $ide"
    }

    # tell the shell to rebuild its extension / MEF / menu cache on next start
    # (TcXaeShell does not support "/updateconfiguration" and only shows an "Invalid Command Line" dialog)
    $marker = Join-Path $ide 'Extensions\extensions.configurationchanged'
    Set-Content -Path $marker -Value '' -Force
}

exit 0

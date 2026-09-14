# Builds launcher\dist\Shoal.exe with the C# compiler that ships with Windows (nothing to download).
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }

$dist = Join-Path $here 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
    "/win32icon:$here\shoal.ico" "/out:$dist\Shoal.exe" `
    /r:System.Windows.Forms.dll "$here\Launcher.cs"
if ($LASTEXITCODE -ne 0) { throw "Compile failed (exit $LASTEXITCODE)" }

Get-Item "$dist\Shoal.exe" | Select-Object FullName, Length, LastWriteTime

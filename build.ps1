$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$root = $PSScriptRoot
$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

$common = @('/nologo', '/codepage:65001', '/optimize+',
    '/r:System.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll')
$lib = @(Get-ChildItem (Join-Path $root 'src\*.cs') | Where-Object { $_.Name -ne 'Program.cs' } | ForEach-Object { $_.FullName })
$tests = @(Get-ChildItem (Join-Path $root 'tests\*.cs') | ForEach-Object { $_.FullName })

& $csc @common /target:winexe "/out:$bin\WinWidget.exe" (Join-Path $root 'src\Program.cs') @lib
if ($LASTEXITCODE -ne 0) { throw 'WinWidget.exe build failed' }

& $csc @common /target:exe "/out:$bin\WinWidget.Tests.exe" @tests @lib
if ($LASTEXITCODE -ne 0) { throw 'WinWidget.Tests.exe build failed' }

Write-Host 'Build OK'

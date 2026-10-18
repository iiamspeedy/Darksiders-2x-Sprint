$ErrorActionPreference = 'Stop'
$taskSource = Join-Path $PSScriptRoot 'DarksidersSprint.cs'
$taskOutput = Join-Path (Split-Path $PSScriptRoot) 'DarksidersSprint.exe'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
& $taskCompiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$taskOutput" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $taskSource
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output "Built $taskOutput"

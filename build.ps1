$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }
New-Item -ItemType Directory -Path "$PSScriptRoot\bin" -Force | Out-Null
& $compiler /nologo /target:winexe /out:"$PSScriptRoot\bin\ColonizationNeeds.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\src\ColonizationNeeds.cs" "$PSScriptRoot\src\JournalCargoTracker.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed. Close the running app before rebuilding.' }
$check = Start-Process -FilePath "$PSScriptRoot\bin\ColonizationNeeds.exe" -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($check.ExitCode -ne 0) { throw "Self-tests failed: $($check.ExitCode)" }
Write-Output 'Built bin\ColonizationNeeds.exe; self-tests passed.'

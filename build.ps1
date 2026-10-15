$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }
New-Item -ItemType Directory -Path "$PSScriptRoot\bin" -Force | Out-Null
& $compiler /nologo /codepage:65001 /target:winexe /win32icon:"$PSScriptRoot\assets\ColonizationNeeds.ico" /out:"$PSScriptRoot\bin\ColonizationNeeds.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\src\ColonizationNeeds.cs" "$PSScriptRoot\src\JournalCargoTracker.cs" "$PSScriptRoot\src\SharedInventoryClient.cs" "$PSScriptRoot\src\RavenDeliveryReporter.cs" "$PSScriptRoot\src\RavenConstruction.cs" "$PSScriptRoot\src\PlayerProfiles.cs" "$PSScriptRoot\src\CarrierInventory.cs" "$PSScriptRoot\src\CarrierUI.cs" "$PSScriptRoot\src\CarrierTests.cs" "$PSScriptRoot\src\CarrierDiscovery.cs" "$PSScriptRoot\src\TransitLedger.cs" "$PSScriptRoot\src\CompactUI.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed. Close the running app before rebuilding.' }
& "$PSScriptRoot\tests\Test-Carrier.ps1"
& "$PSScriptRoot\tests\Test-Reporting.ps1"
Write-Output 'Built bin\ColonizationNeeds.exe; self-tests passed.'


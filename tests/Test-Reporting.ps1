$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('ColonizationNeeds-report-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'bin\ColonizationNeeds.exe') -Destination (Join-Path $testRoot 'ColonizationNeeds.exe')
    & $compiler /nologo /out:"$testRoot\RavenReporterTests.exe" /reference:"$testRoot\ColonizationNeeds.exe" /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\RavenReporterTests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Reporting tests could not compile.' }
    & "$testRoot\RavenReporterTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Reporting tests failed.' }
} finally {
    Get-ChildItem -LiteralPath $testRoot -File | Remove-Item
    Remove-Item -LiteralPath $testRoot
}

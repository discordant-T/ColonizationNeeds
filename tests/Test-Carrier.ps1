param([string]$PreviewPath = '')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('ColonizationNeeds-carrier-check-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'bin\ColonizationNeeds.exe') -Destination (Join-Path $testRoot 'ColonizationNeeds.exe')
    foreach($testName in @('SelfTestRunner','TransitTests','InboundTests','SharedCarrierTests','UITests','CarrierSetupUITests','QuietStatusUITests','CompactUITests')) {
        & $taskCompiler /nologo /out:"$testRoot\$testName.exe" /reference:"$testRoot\ColonizationNeeds.exe" /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\$testName.cs"
        if($LASTEXITCODE -ne 0) {throw "$testName compilation failed"}
        if($testName -like '*UITests') {
            $preview=if($PreviewPath) {$PreviewPath} else {Join-Path $testRoot 'preview.png'}
            if($testName -eq 'CarrierSetupUITests') { $preview=$preview+'.setup.png' }
            $check=Start-Process -FilePath "$testRoot\$testName.exe" -ArgumentList ('"'+$preview+'"') -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput "$testRoot\ui-output.txt" -RedirectStandardError "$testRoot\ui-error.txt"
            Get-Content -LiteralPath "$testRoot\ui-output.txt"
            Get-Content -LiteralPath "$testRoot\ui-error.txt"
            if($check.ExitCode -ne 0) {throw "UI checks failed: $($check.ExitCode)"}
        } else {
            & "$testRoot\$testName.exe"
            if($LASTEXITCODE -ne 0) {throw "$testName failed"}
        }
    }
} finally {
    Get-ChildItem -LiteralPath $testRoot -File | Remove-Item
    Remove-Item -LiteralPath $testRoot
}

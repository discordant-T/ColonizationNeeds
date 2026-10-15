param(
    [Parameter(Mandatory=$true)][string]$PlanName,
    [Parameter(Mandatory=$true)][string]$BuildId,
    [Parameter(Mandatory=$true)][long]$SystemAddress,
    [Parameter(Mandatory=$true)][long]$MarketId,
    [Parameter(Mandatory=$true)][string]$Commander,
    [switch]$InspectOnly
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$baseUrl = 'https://ravencolonial100-awcbdvabgze4c5cq.canadacentral-01.azurewebsites.net/api/'
$secret = Read-Host 'Your Raven Colonial API key (not a Cloudflare token)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
$headers = $null
try {
    $headers = @{
        'rcc-key' = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
        'rcc-cmdr0' = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Commander))
    }
    $project = Invoke-RestMethod -Uri ($baseUrl + 'project/' + [Uri]::EscapeDataString($BuildId)) -Headers $headers
    if ($project.buildId -ne $BuildId -or [long]$project.marketId -ne $MarketId -or [long]$project.systemAddress -ne $SystemAddress) {
        throw 'The project does not match the expected construction site. Nothing was changed.'
    }
    if ($project.complete -or [string]::IsNullOrWhiteSpace($project.buildType)) {
        throw 'The project must be incomplete and have its correct build type set first. Nothing was changed.'
    }
    $sitesUrl = $baseUrl + 'v2/system/' + $SystemAddress + '/sites'
    $sitesResponse = Invoke-RestMethod -Uri $sitesUrl -Headers $headers
    $sites = @($sitesResponse | ForEach-Object { $_ })
    if ($sites.Count -eq 0 -or @($sites | Where-Object { [string]::IsNullOrWhiteSpace($_.id) }).Count -gt 0) {
        throw 'Raven returned an unexpected site-list format. Nothing was changed.'
    }
    if ($InspectOnly) {
        Write-Output 'READ ONLY: no changes will be made.'
        Write-Output 'Existing project:'
        $project | Select-Object buildId, buildName, buildType, marketId, bodyNum, systemAddress | ConvertTo-Json -Depth 10
        Write-Output 'Matching planned site or existing links:'
        $sites | Where-Object { $_.name -eq $PlanName -or $_.buildId -eq $BuildId -or [string]$_.marketId -eq [string]$MarketId } | Select-Object id, name, status, buildId, marketId, bodyNum, buildType | ConvertTo-Json -Depth 10
        return
    }
    $already = @($sites | Where-Object { $_.buildId -eq $BuildId })
    if ($already.Count -gt 0) {
        if ($already.Count -eq 1 -and $already[0].status -eq 'build' -and [long]$already[0].marketId -eq $MarketId) {
            Write-Output ('Already linked: ' + $already[0].name + '. Reload the Raven system page.')
            return
        }
        throw 'This project is already linked to a site with inconsistent fields. Nothing was changed.'
    }
    $matches = @($sites | Where-Object { $_.name -eq $PlanName -and $_.status -eq 'plan' })
    if ($matches.Count -ne 1) { throw 'Expected exactly one planned site with that name. Nothing was changed.' }
    $site = $matches[0]
    if ([string]::IsNullOrWhiteSpace($site.id) -or $site.buildId -or ($site.marketId -and [long]$site.marketId -ne $MarketId)) {
        throw 'The planned site already has a different identity or project link. Nothing was changed.'
    }
    if ($null -eq $project.bodyNum -or [int]$site.bodyNum -ne [int]$project.bodyNum) {
        throw 'The planned site and project are on different bodies. Nothing was changed.'
    }
    if ([string]$site.buildType -ne [string]$project.buildType) {
        throw ('Construction types differ: plan=' + $site.buildType + ', project=' + $project.buildType + '. Check the exact variants before repairing. Nothing was changed.')
    }
    $displayName = ([string]$project.buildName) -replace '^Orbital Construction Site:\s*', ''
    $displayName = $displayName -replace '^Surface Construction Site:\s*', ''
    Write-Output ('Linking ' + $PlanName + ' to ' + $displayName + ' in ' + $project.systemName + ', body ' + $project.bodyNum + ', MarketID ' + $MarketId)
    $site | Add-Member -NotePropertyName buildId -NotePropertyValue $BuildId -Force
    $site | Add-Member -NotePropertyName marketId -NotePropertyValue $MarketId -Force
    $site.status = 'build'
    $site.name = $displayName
    $body = @{ update = @($site); delete = @() } | ConvertTo-Json -Depth 30
    $null = Invoke-RestMethod -Method Put -Uri $sitesUrl -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
    $afterResponse = Invoke-RestMethod -Uri $sitesUrl -Headers $headers
    $after = @($afterResponse | ForEach-Object { $_ })
    $verified = @($after | Where-Object { $_.id -eq $site.id -and $_.buildId -eq $BuildId -and $_.status -eq 'build' -and [long]$_.marketId -eq $MarketId })
    if ($verified.Count -ne 1) { throw 'The update was sent but could not be verified. Check Raven before retrying.' }
    Write-Output ('Verified: ' + $verified[0].name + ' is under construction and linked to the existing project. Reload the Raven system page.')
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    if ($headers) { $headers.Clear() }
    $secret.Dispose()
}

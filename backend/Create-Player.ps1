param(
    [string]$ServerUrl = 'https://colonizationneeds-api.macytr.workers.dev',
    [string]$GroupId = 'squad-carrier',
    [string]$PlayerName,
    [ValidateSet('member','admin')][string]$Role = 'admin'
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$uri = [Uri]$ServerUrl
if ($uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.AbsolutePath -ne '/') { throw 'Use the HTTPS Worker URL without a path.' }
if (!$PlayerName) { $PlayerName = Read-Host 'Player name (your commander name)' }
if ([string]::IsNullOrWhiteSpace($PlayerName)) { throw 'Player name is required.' }
$secret = Read-Host 'OWNER_TOKEN from Cloudflare (hidden input)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $ownerToken = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $body = @{ groupId=$GroupId; name=$PlayerName; role=$Role } | ConvertTo-Json
    $result = Invoke-RestMethod -Uri ($ServerUrl.TrimEnd('/') + '/admin/players') -Method Post -ContentType 'application/json' -Headers @{ Authorization=('Bearer ' + $ownerToken) } -Body $body
    Set-Clipboard -Value $result.accessToken
    Write-Host "Created $Role player '$($result.name)' in group '$($result.groupId)'."
    Write-Host 'The player token is on your clipboard. Paste it into Settings > Shared inventory > Player access token.'
    Write-Host 'Enable Use shared inventory, test the connection, then Save. Keep this token private.'
    Write-Host 'For another player, run this script with -Role member -PlayerName THEIR_NAME and the same -GroupId.'
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $ownerToken=$null
    $secret.Dispose()
}

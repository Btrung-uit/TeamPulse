[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\.env")
)

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $resolvedOutput) {
    throw "Refusing to overwrite existing file: $resolvedOutput"
}

$docker = Get-Command docker -ErrorAction SilentlyContinue
if ($null -ne $docker) {
    $existingVolume = & docker volume ls `
        --filter "label=com.docker.compose.project=teampulse" `
        --filter "label=com.docker.compose.volume=teampulse-sqlserver-data" `
        --quiet 2>$null
    if ($LASTEXITCODE -eq 0 -and $existingVolume) {
        throw @"
The TeamPulse SQL volume already exists but .env is missing.
Recover the previous MSSQL_SA_PASSWORD or rotate the SQL login before creating new secrets.
The existing database volume was not changed.
"@
    }
}

$randomBytes = [Security.Cryptography.RandomNumberGenerator]::GetBytes(36)
$saPassword = "Tp!" + [Convert]::ToBase64String($randomBytes)
$jwtKey = [Convert]::ToBase64String(
    [Security.Cryptography.RandomNumberGenerator]::GetBytes(64))

$content = @"
# Generated locally. Do not commit this file.
MSSQL_SA_PASSWORD=$saPassword
MSSQL_PORT=1433
JWT_SIGNING_KEY=$jwtKey
APP_HOST=0.0.0.0
APP_PORT=8080
AUTH_RATE_LIMIT=30
IMPORT_RATE_LIMIT=10
RATE_LIMIT_WINDOW_SECONDS=60
"@

[System.IO.File]::WriteAllText(
    $resolvedOutput,
    $content,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Created $resolvedOutput with random local secrets."

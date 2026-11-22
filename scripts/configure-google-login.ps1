param([string]$ClientId)

$ErrorActionPreference = 'Stop'
$apiProject = Join-Path $PSScriptRoot '../backend/src/PokeArena.Api/PokeArena.Api.csproj'
if ($ClientId -and $ClientId -notmatch '^[A-Za-z0-9.-]+\.apps\.googleusercontent\.com$') {
    throw 'El Client ID debe corresponder a una aplicación web de Google.'
}

# Conserva una clave existente para no invalidar sesiones al repetir la configuración.
[xml]$projectXml = Get-Content -LiteralPath $apiProject -Raw
$secretsId = [string]$projectXml.Project.PropertyGroup.UserSecretsId
$secretsPath = Join-Path $env:APPDATA "Microsoft/UserSecrets/$secretsId/secrets.json"
$existingKey = $null
if (Test-Path -LiteralPath $secretsPath) {
    $existing = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
    $existingKey = $existing.PSObject.Properties['Authentication:Platform:SigningKeyBase64'].Value
}
if ($existingKey) {
    if ([Convert]::FromBase64String($existingKey).Length -lt 32) { throw 'La clave existente debe tener al menos 32 bytes.' }
} else {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $existingKey = [Convert]::ToBase64String($bytes)
}

$settings = @{
    'Authentication:Platform:SigningKeyBase64' = $existingKey
    'Authentication:Platform:Issuer' = 'PokeArena'
    'Authentication:Platform:Audience' = 'PokeArena.Api'
    'Authentication:Platform:LifetimeMinutes' = '15'
}
if ($ClientId) { $settings['Authentication:Google:ClientId'] = $ClientId }

# Envía los secretos por stdin; no imprime la clave ni la incluye en el repositorio.
$settings | ConvertTo-Json -Compress | dotnet user-secrets set --project $apiProject
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron guardar los secretos de desarrollo.' }
Write-Output 'Firma de plataforma configurada. Reinicie la API para cargar los cambios.'
if (-not $ClientId) { Write-Output 'Google sigue pendiente: repita este script con -ClientId cuando lo obtenga.' }

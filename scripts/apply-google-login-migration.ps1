$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$migration = Join-Path $repositoryRoot 'database/postgres/migrations/003_google_login.sql'
$previousEncoding = $OutputEncoding
Push-Location -LiteralPath $repositoryRoot
try {
    # Se aplica al PostgreSQL local de Compose sin recrear contenedores ni borrar registros.
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
    Get-Content -LiteralPath $migration -Encoding UTF8 -Raw |
        docker compose exec -T postgres psql -U pokearena -d pokearena -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo aplicar la migración de inicio de sesión.' }
} finally {
    $OutputEncoding = $previousEncoding
    Pop-Location
}

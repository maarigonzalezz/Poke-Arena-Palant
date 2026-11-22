$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$migration = Join-Path $repositoryRoot 'database/postgres/migrations/004_catalog_scale.sql'
$previousEncoding = $OutputEncoding
Push-Location -LiteralPath $repositoryRoot
try {
    # Agrega el índice y normaliza nombres sin borrar cartas ni modificar sus códigos.
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
    Get-Content -LiteralPath $migration -Encoding UTF8 -Raw |
        docker compose exec -T postgres psql -U pokearena -d pokearena -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo aplicar la migración de escala del catálogo.' }
} finally {
    $OutputEncoding = $previousEncoding
    Pop-Location
}

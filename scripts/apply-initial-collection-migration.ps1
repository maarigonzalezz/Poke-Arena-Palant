$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$previousEncoding = $OutputEncoding
Push-Location -LiteralPath $repositoryRoot
try {
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
    # Completa el esquema sin cargar cartas de prueba ni modificar saldos existentes.
    foreach ($name in @('005_player_inventory.sql', '007_initial_collection.sql')) {
        Get-Content -LiteralPath (Join-Path $repositoryRoot "database/postgres/migrations/$name") -Encoding UTF8 -Raw |
            docker compose exec -T postgres psql -U pokearena -d pokearena -v ON_ERROR_STOP=1
        if ($LASTEXITCODE -ne 0) { throw "No se pudo aplicar $name." }
    }
} finally {
    $OutputEncoding = $previousEncoding
    Pop-Location
}

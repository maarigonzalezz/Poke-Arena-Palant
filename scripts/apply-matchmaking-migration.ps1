$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$previousEncoding = $OutputEncoding
Push-Location -LiteralPath $repositoryRoot
try {
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
    # Crea únicamente cola y partidas; no crea decks ni altera cartas, saldos o inventarios.
    Get-Content -LiteralPath (Join-Path $repositoryRoot 'database/postgres/migrations/008_matchmaking.sql') -Encoding UTF8 -Raw |
        docker compose exec -T postgres psql -U pokearena -d pokearena -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo aplicar la migración de Buscar Partida.' }
} finally {
    $OutputEncoding = $previousEncoding
    Pop-Location
}

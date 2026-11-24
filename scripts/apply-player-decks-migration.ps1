$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$previousEncoding = $OutputEncoding
Push-Location -LiteralPath $repositoryRoot
try {
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
    Get-Content -LiteralPath 'database/postgres/migrations/010_player_decks.sql' -Encoding UTF8 -Raw |
        docker compose exec -T postgres psql -U pokearena -d pokearena -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo aplicar 010_player_decks.sql.' }
} finally {
    $OutputEncoding = $previousEncoding
    Pop-Location
}

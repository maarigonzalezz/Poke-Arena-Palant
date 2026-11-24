param([string]$Container = 'pokearena-postgres')

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$previousEncoding = $OutputEncoding
Push-Location -LiteralPath $repositoryRoot
try {
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
    foreach ($migration in @(
        '008_matchmaking.sql',
        '009_matchmaking_lifecycle.sql',
        '010_match_preparation.sql',
        '011_player_decks.sql',
        '012_match_card_actions.sql',
        '013_match_results.sql'
    )) {
        Get-Content -LiteralPath (Join-Path $repositoryRoot "database/postgres/migrations/$migration") -Encoding UTF8 -Raw |
            docker exec -i $Container psql -U pokearena -d pokearena -v ON_ERROR_STOP=1
        if ($LASTEXITCODE -ne 0) { throw "No se pudo aplicar $migration." }
    }
} finally {
    $OutputEncoding = $previousEncoding
    Pop-Location
}

# Ejecuta la API contra un contenedor temporal; no usa las bases del proyecto.
$ErrorActionPreference = 'Stop'
$previous = $env:POKEARENA_INTEGRATION_TESTS
try {
    $env:POKEARENA_INTEGRATION_TESTS = '1'
    $project = Join-Path $PSScriptRoot '../backend/tests/PokeArena.Tests/PokeArena.Tests.csproj'
    dotnet test $project --filter 'Category=Integration' --logger 'console;verbosity=normal'
    $testExitCode = $LASTEXITCODE
} finally {
    $env:POKEARENA_INTEGRATION_TESTS = $previous
}
exit $testExitCode

param([string]$Container = 'pokearena-postgres')

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$filesRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'backend/files'))
$previousEncoding = $OutputEncoding
$previousConsoleEncoding = [Console]::OutputEncoding

# Solo acepta imágenes del almacenamiento local y comprueba ambas versiones antes de copiarlas.
function Local-Image([string]$url) {
    if (!$url.StartsWith('/files/cards/')) { return $null }
    $path = [IO.Path]::GetFullPath((Join-Path $filesRoot $url.Substring(7)))
    if (!$path.StartsWith($filesRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { return $null }
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    if ((Get-Item -LiteralPath $path).Length -eq 0) { return $null }
    return $path
}

# Las cadenas se escapan para SQL; ningún valor se interpreta como un comando de shell.
function Sql-Text([string]$value) { return "'" + $value.Replace("'", "''") + "'" }

try {
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
    [Console]::OutputEncoding = $OutputEncoding
    $json = & docker exec $Container psql -U pokearena -d pokearena -At -v ON_ERROR_STOP=1 -c 'SELECT COALESCE(json_agg(c),''[]''::json) FROM (SELECT code,name,card_type,image_status,image_original_path,image_small_path FROM cards ORDER BY id) c;'
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar el catálogo local.' }
    $cards = ($json -join "`n") | ConvertFrom-Json
    $donors = @($cards | Where-Object {
        $_.image_status -eq 'Ready' -and $_.image_original_path -and $_.image_small_path -and
        (Local-Image $_.image_original_path) -and (Local-Image $_.image_small_path)
    })
    if ($donors.Count -eq 0) { throw 'Primero cree una carta con sus dos imágenes en el catálogo local.' }

    $seed = Get-Content -LiteralPath (Join-Path $repositoryRoot 'database/postgres/migrations/006_test_cards.sql') -Encoding UTF8 -Raw
    $entries = [regex]::Matches($seed, "\('(?<code>PKA-[A-Z]+-[0-9]+)', '(?<name>[^']+)',")
    $rows = [Collections.Generic.List[string]]::new()
    foreach ($entry in $entries) {
        $code = $entry.Groups['code'].Value
        $name = $entry.Groups['name'].Value
        $existing = $cards | Where-Object code -eq $code | Select-Object -First 1
        # Conserva las cartas manuales que ocupan códigos del seed y las que ya están listas.
        if ($existing -and ($existing.name -cne $name -or
            ($existing.image_status -eq 'Ready' -and $existing.image_original_path -and $existing.image_small_path))) { continue }
        $donor = $donors | Where-Object name -eq $name | Select-Object -First 1
        if (!$donor) { $donor = $donors | Where-Object { $_.code.Substring(4,3) -eq $code.Substring(4,3) } | Select-Object -First 1 }
        if (!$donor) { $donor = $donors[0] }
        $original = Local-Image $donor.image_original_path
        $small = Local-Image $donor.image_small_path
        $extension = [IO.Path]::GetExtension($original).ToLowerInvariant()
        if ($extension -notin @('.png', '.jpg', '.jpeg', '.webp')) { throw 'La imagen de origen tiene un formato no permitido.' }
        $folder = 'cards/' + [Guid]::NewGuid().ToString('N')
        $destination = Join-Path $filesRoot $folder
        New-Item -ItemType Directory -Path $destination | Out-Null
        # Cada carta recibe copias propias para no depender de la eliminación de otra carta.
        Copy-Item -LiteralPath $original -Destination (Join-Path $destination "original$extension")
        Copy-Item -LiteralPath $small -Destination (Join-Path $destination 'small.png')
        $rows.Add('(' + ((@($code, $name, "/files/$folder/original$extension", "/files/$folder/small.png") | ForEach-Object { Sql-Text $_ }) -join ',') + ')')
    }
    if ($rows.Count -eq 0) { Write-Output 'Las cartas de prueba ya tienen imágenes; no hay cambios.'; return }

    # Cartas y versiones se confirman juntas; un fallo SQL deja únicamente archivos sin referencias.
    $seed = $seed -replace '(?m)^BEGIN;\s*', '' -replace '(?m)^END;\s*', ''
    $sql = @"
BEGIN;
$seed
CREATE TEMP TABLE imported_images(code text, name text, original text, small text) ON COMMIT DROP;
INSERT INTO imported_images VALUES $($rows -join ',');
WITH changed AS (
    UPDATE cards c SET image_original_path=i.original, image_small_path=i.small, image_status='Ready',
        current_version=c.current_version + CASE WHEN EXISTS (
            SELECT 1 FROM card_versions v WHERE v.card_id=c.id AND v.version=c.current_version) THEN 1 ELSE 0 END
    FROM imported_images i WHERE c.code=i.code AND c.name=i.name
      AND (c.image_status<>'Ready' OR c.image_original_path IS NULL OR c.image_small_path IS NULL)
    RETURNING c.*
)
INSERT INTO card_versions(card_id,version,snapshot)
SELECT c.id,c.current_version,jsonb_build_object(
    'id',c.id,'code',c.code,'name',c.name,'cost',c.cost,'power',c.power,'type',c.card_type,
    'rarity',c.rarity,'imageStatus',c.image_status,'imageUrl',c.image_original_path,
    'imageSmallUrl',c.image_small_path,'abilityCode',a.code,'abilityName',a.name,
    'abilityTrigger',a.trigger_type,'abilityEffect',a.effect_type,'abilityParameters',a.parameters::text)
FROM changed c JOIN abilities a ON a.code=c.ability_code;
COMMIT;
"@
    $sql | & docker exec -i $Container psql -U pokearena -d pokearena -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'La importación se revirtió; revise el error antes de reintentar.' }
    Write-Output "Imágenes preparadas para $($rows.Count) cartas de prueba; se conservaron los códigos y los inventarios."
} finally {
    $OutputEncoding = $previousEncoding
    [Console]::OutputEncoding = $previousConsoleEncoding
}

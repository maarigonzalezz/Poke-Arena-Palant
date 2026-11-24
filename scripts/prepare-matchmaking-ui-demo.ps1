param([string]$Container = 'pokearena-postgres')

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$filesRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'backend/files'))

& (Join-Path $PSScriptRoot 'apply-matchmaking-migration.ps1') -Container $Container
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron aplicar las migraciones de matchmaking.' }
& (Join-Path $PSScriptRoot 'import-test-cards-with-images.ps1') -Container $Container
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron preparar las imágenes locales de cartas.' }

function Local-Image([string]$url) {
    if (!$url -or !$url.StartsWith('/files/cards/')) { return $null }
    $path = [IO.Path]::GetFullPath((Join-Path $filesRoot $url.Substring(7)))
    if (!$path.StartsWith($filesRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { return $null }
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) { return $null }
    return $path
}

function Sql-Text([string]$value) { return "'" + $value.Replace("'", "''") + "'" }

$source = & docker exec $Container psql -U pokearena -d pokearena -At -v ON_ERROR_STOP=1 -c @'
SELECT image_original_path || E'\n' || image_small_path
FROM cards
WHERE active AND image_status='Ready' AND image_original_path IS NOT NULL AND image_small_path IS NOT NULL
ORDER BY id
LIMIT 1;
'@
if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar la imagen local de referencia.' }
$paths = ($source -join "`n").Trim().Split("`n")
if ($paths.Count -ne 2 -or !(Local-Image $paths[0].Trim()) -or !(Local-Image $paths[1].Trim())) {
    throw 'No hay una carta Ready con imágenes locales válidas para reutilizar en el catálogo de demo.'
}
$original = Sql-Text $paths[0].Trim()
$small = Sql-Text $paths[1].Trim()

$sql = @"
BEGIN;
WITH demo(name,description,max_cards,energy_mode,extra_energy,effect_type,effect_parameters) AS (
    VALUES
        ('Demo - Canyon','Test location with fixed energy.',4,'Fixed',1,'None','{}'::jsonb),
        ('Demo - Lagoon','Test location with no bonus energy.',4,'Fixed',0,'None','{}'::jsonb),
        ('Demo - Forest','Grants energy for each own card at turn start.',4,'PerCard',1,'None','{}'::jsonb)
), inserted AS (
    INSERT INTO locations(name,description,max_cards,energy_mode,extra_energy,effect_type,effect_parameters,
        image_status,image_original_path,image_small_path,current_version,active)
    SELECT d.name,d.description,d.max_cards,d.energy_mode,d.extra_energy,d.effect_type,d.effect_parameters,
        'Ready',$original,$small,1,true
    FROM demo d
    WHERE NOT EXISTS (SELECT 1 FROM locations l WHERE l.name=d.name)
      AND (SELECT count(*) FROM locations WHERE name LIKE 'Demo - %') < 3
    RETURNING *
)
INSERT INTO location_versions(location_id,version,snapshot)
SELECT id,1,jsonb_build_object(
    'id',id,'name',name,'description',description,'maxCards',max_cards,'energyMode',energy_mode,
    'extraEnergy',extra_energy,'effectType',effect_type,'effectParameters',effect_parameters::text,
    'imageStatus',image_status,'imageOriginalPath',image_original_path,'imageSmallPath',image_small_path,
    'currentVersion',current_version,'active',active)
FROM inserted;
COMMIT;
"@
$sql | & docker exec -i $Container psql -U pokearena -d pokearena -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron crear las ubicaciones demo.' }

$count = & docker exec $Container psql -U pokearena -d pokearena -At -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM locations WHERE active AND image_status='Ready' AND max_cards>0 AND effect_type<>'TurnRestriction';"
if ($LASTEXITCODE -ne 0 -or [int]($count -join '') -lt 2) {
    throw 'El catálogo todavía no tiene dos ubicaciones jugables y sin restricción de turno.'
}
Write-Output 'Demo preparada: cartas con imagenes locales y ubicaciones listas. Inicie sesion, guarde un deck de 12 cartas y busque una partida.'

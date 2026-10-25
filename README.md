# Poké-Arena — integración del Sprint 1

Integra **Crear Carta**, **Buscar Carta** y **Crear Ubicación** en `sprint1`,
con las ramas `feature/create_card`, `feature/ubicaciones` y el trabajo de
`feature/search_card`. Los comandos siguientes son para **PowerShell desde
la raíz del repositorio**.

## Qué está implementado

| Función | Comportamiento |
| --- | --- |
| Crear Carta | POST real, validaciones, habilidad compatible, código automático y versión inicial |
| Imágenes de cartas | Conserva el original; genera PNG de 200 × 280 antes de guardar como Ready |
| Buscar Carta | Filtros AND, nombre sin tildes/mayúsculas, rangos y sesiones paginadas estables |
| Crear Ubicación | POST real con imagen, energía, capacidad, efectos y versión inicial |
| Imágenes de ubicaciones | Conserva el original y genera PNG de 320 × 240 |
| Habilidades | GET del catálogo activo de PostgreSQL, incluida HAB-000 |
| Reintentos | Idempotencia persistente por usuario, operación y clave; conflicto si cambian los datos |
| Permisos | JWT obligatorio y rol ContentAdmin; respuestas 401 y 403 distinguibles |
| Simulación | Sigue disponible para trabajar en la interfaz sin API ni Docker |

Las imágenes se procesan dentro de la creación. Si falla el guardado, se
revierte la transacción y se eliminan los archivos nuevos. Las tablas
`card_versions` y `location_versions` conservan la versión inicial.

Las partidas deben consumir las vistas **`playable_cards`** y
**`available_locations`**, que exigen estado Ready y ambas rutas de imagen.
Este sprint no implementa el motor de partidas ni un proveedor de identidad.
MongoDB sigue disponible en Compose, pero estas tres historias usan PostgreSQL
y archivos locales. El outbox no se necesita para este procesamiento síncrono.

## 1. Instalación

Instalar Git, Node **22.12 o superior de la rama 22**, **.NET SDK 9** y Docker
Desktop. npm y Compose vienen incluidos con Node y Docker, respectivamente.

```powershell
winget install --exact --id Git.Git --source winget
winget install --exact --id OpenJS.NodeJS.22 --source winget
winget install --exact --id Microsoft.DotNet.SDK.9 --source winget
winget install --exact --id Docker.DockerDesktop --source winget
```

Referencias de instalación: [Git](https://git-scm.com/install/windows),
[Node 22](https://developer.microsoft.com/blog/winget-configuration-set-up-your-dev-machine-in-one-command/),
[.NET](https://learn.microsoft.com/en-us/dotnet/core/install/windows) y
[Docker Desktop](https://docs.docker.com/desktop/setup/install/windows-install/).
Si Docker necesita WSL 2, ejecutar `wsl --install` como administrador y
reiniciar; para actualizar una instalación existente, `wsl --update`.
Abrir Docker Desktop y usar contenedores Linux.

En una terminal nueva:

```powershell
git --version
node --version
npm.cmd --version
dotnet --list-sdks
docker version
docker compose version
```

Para descargar el proyecto, reemplazar la URL por la del equipo:

```powershell
git clone <URL_DEL_REPOSITORIO> Poke-Arena-Palant
cd Poke-Arena-Palant
git switch sprint1
dotnet restore backend/PokeArena.sln
npm.cmd --prefix frontend ci
```

No instalar paquetes npm globalmente. `npm ci` respeta el lockfile.
SkiaSharp **4.153.1** y sus archivos nativos se restauran con NuGet; reemplaza
ImageSharp para que Debug y Release no requieran una clave de compilación.
Consultar [SkiaSharp](https://www.nuget.org/packages/SkiaSharp/4.153.1).
En Linux/macOS usar `npm` y adaptar las variables de entorno de PowerShell.

## 2. Bases de datos y migración

Definir contraseñas locales propias y mantener esta terminal para Compose:

```powershell
$env:POSTGRES_PASSWORD = 'REEMPLAZAR_CON_CLAVE_LOCAL_POSTGRES'
$env:MONGO_PASSWORD = 'REEMPLAZAR_CON_CLAVE_LOCAL_MONGO'
docker compose pull
docker compose up -d
docker compose ps
```

También puede copiarse `.env.example` a `.env`, excluido de Git. No subir contraseñas,
JWT ni archivos de imágenes generados. Las variables de entorno duran hasta
cerrar esa terminal.

Una base nueva ejecuta `init.sql` y la migración automáticamente. **Si el
volumen ya existía antes de integrar las ramas**, ejecutar:

```powershell
docker compose cp database/postgres/migrations/001_integrate_catalog.sql postgres:/tmp/integrate.sql
docker compose exec -T postgres psql -U pokearena -d pokearena -v ON_ERROR_STOP=1 -f /tmp/integrate.sql
```

La migración conserva datos, agrega habilidades de demostración si faltan,
reconcilia consecutivos, protege el código inmutable y crea las vistas para
partidas. Puede repetirse. Reiniciar Docker no vuelve a ejecutar `init.sql`.

Opcionalmente, cargar las 12 cartas SQL de demostración:

```powershell
docker compose cp database/postgres/seed_test_cards.sql postgres:/tmp/seed.sql
docker compose exec -T postgres psql -U pokearena -d pokearena -v ON_ERROR_STOP=1 -f /tmp/seed.sql
docker compose exec -T postgres psql -U pokearena -d pokearena -c 'SELECT COUNT(*) FROM cards;'
```

Estas cartas tienen imágenes pendientes y no aparecen en `playable_cards`.
Sus códigos empiezan en 901; el generador continúa después del mayor código
existente de cada tipo. El catálogo SQL es distinto al de 21 cartas simuladas.

Configurar la API desde la misma terminal:

```powershell
dotnet user-secrets set 'ConnectionStrings:Postgres' "Host=localhost;Port=5432;Database=pokearena;Username=pokearena;Password='$env:POSTGRES_PASSWORD'" --project backend/src/PokeArena.Api
```

La API ya tiene `UserSecretsId`. User Secrets es configuración local fuera
del repositorio, no almacenamiento cifrado. También se puede usar la variable
`ConnectionStrings__Postgres`. ASP.NET no carga el `.env` de Compose.

| Servicio | Dirección / configuración |
| --- | --- |
| Frontend | http://localhost:5173 |
| API HTTP | http://localhost:5003 |
| PostgreSQL | localhost:5432; usuario y base pokearena |
| MongoDB | localhost:27017; usuario pokearena; authSource=admin |
| Imágenes | backend/files, publicadas bajo /files |

`Storage:Root` permite cambiar la carpeta de imágenes. Respaldarla junto a
PostgreSQL: los registros contienen sus rutas. Si la API se ejecuta dentro
de Compose en el futuro, usar hosts `postgres` y `mongodb`, no localhost.
Cambiar las variables de contraseña no cambia las de un volumen existente.

## 3. Ejecutar las tres historias contra la API real

**Terminal 1 — backend:**

```powershell
dotnet run --project backend/src/PokeArena.Api --launch-profile http
```

**Terminal 2 — frontend:**

```powershell
$env:VITE_API_URL = 'http://localhost:5003'
$env:VITE_MOCK_API = 'false'
npm.cmd --prefix frontend run dev
```

Abrir **http://localhost:5173**. Estas variables prevalecen sobre los archivos
de Vite. Alternativamente, definirlas en `frontend/.env.development`.
Reiniciar Vite cuando cambien; `VITE_*` es público y no debe contener secretos.

**Terminal 3 — JWT de desarrollo:**

```powershell
dotnet user-jwts create --project backend/src/PokeArena.Api --role ContentAdmin --audience http://localhost:5003 --valid-for 1h --output token
```

Si es el primer token o cambió la clave local, reiniciar la API. Pegar el
token en **Acceso administrativo de pruebas → Conectar**. El frontend verifica
el acceso al catálogo de habilidades y recarga los formularios. Desconectar
borra el token de memoria. No se almacena en localStorage.

Este acceso manual solo se muestra en desarrollo. Para un despliegue con
usuarios reales, conectar el proveedor del equipo al servicio
`setCatalogAccessToken(token)` y configurar:

```powershell
dotnet user-secrets set 'Authentication:Authority' 'https://EMISOR_DEL_EQUIPO' --project backend/src/PokeArena.Api
dotnet user-secrets set 'Authentication:Audience' 'AUDIENCIA_DEL_EQUIPO' --project backend/src/PokeArena.Api
```

Usar el proveedor externo **o** los JWT locales para la configuración de
desarrollo elegida; no asumir que un token local es válido ante un emisor externo.
Se valida firma, emisor, audiencia, expiración y rol `ContentAdmin`; el
identificador se obtiene de `sub` o `NameIdentifier`. No quitar `[Authorize]`.

Comprobar disponibilidad HTTP:

```powershell
Invoke-RestMethod 'http://localhost:5003/api/health'
```

El health no comprueba la base de datos. CORS autoriza localhost:5173 por
defecto. Otros orígenes se configuran en `Cors:Origins`, por ejemplo mediante
`Cors__Origins__0`; mantener una lista explícita.

## 4. Ejecutar solo la simulación

```powershell
npm.cmd --prefix frontend run dev:mock
```

No requiere Docker, backend ni JWT. La franja superior indica el modo y
permite restablecer los datos. Las cartas y ubicaciones se guardan en ese
navegador; las sesiones y claves de reintento se pierden al recargar.
`dev:mock` fuerza simulación aunque la variable indique false. El build de
producción siempre deshabilita mocks.

## 5. Contratos y reglas para desarrollar

| Método y ruta | Entrada / respuesta |
| --- | --- |
| GET /api/abilities | Arreglo con código, nombre, disparador, efecto, parámetros y restricción de tipo |
| GET /api/cards | Filtros por query; responde items, searchId, nextPosition y totalCount |
| POST /api/cards | FormData: name, cost, power, type, rarity, abilityCode, image |
| POST /api/locations | FormData: name, description, maxCards, energyMode, extraEnergy, effectType, effectParameters, image |

Ambos POST requieren **Idempotency-Key** y devuelven **201 con JSON**, incluso
al repetir exitosamente la misma operación. La carta devuelve `{ item }`;
la ubicación devuelve el registro creado. Los errores incluyen `message`
y, cuando corresponde, `errors`. Un reintento con la misma clave y datos
diferentes devuelve 409. No fijar Content-Type manualmente al enviar FormData.

**Cartas:** nombre de 1–120 caracteres, costo entero 0–10, poder entero
-20–20, tipo y rareza definidos, habilidad activa y compatible. El servidor
asigna versión 1 y código `PKA-<TIPO>-<NNN>`; nunca los recibe del cliente.
Los prefijos son FUE, AGU, PLA, ELE, PSI y LUC. Los consecutivos son atómicos
por tipo; al llegar a 999 se devuelve 409 y se debe acordar una ampliación
del formato, sin reutilizar códigos.

**Imágenes:** un solo archivo PNG, JPG o WebP no vacío, máximo 5 MB. Se
verifica el contenido real, no la extensión. Cartas: mínimo 600 × 840 y 5:7.
Ubicaciones: mínimo 800 × 600 y 4:3. Se admiten imágenes estáticas; archivos
corruptos o animados se rechazan. Las rutas no dependen del nombre enviado.

**Ubicaciones:** nombre y descripción obligatorios; límite entero de 0–5
cartas por jugador (0 impide jugar cartas). Energía `Fixed` de 0–3 o
`PerCard` de 0–2. `effectParameters` es texto JSON dentro del formulario:

| Efecto | Parámetros |
| --- | --- |
| None | `{}` |
| TypeBonus | `{ "type": "Fuego", "power": 2 }`; poder entre -5 y 5 |
| CostModifier | `{ "affectedType": null, "amount": -1 }`; valor entre -5 y 5; null para todos los tipos |
| TurnRestriction | `{ "turns": [1, 6] }`; al menos uno, únicos, entre 1 y 6 |

**Búsqueda:** todos los filtros activos se combinan con AND. Código exacto;
nombre parcial sin tildes ni mayúsculas. Costo/poder aceptan exacto o rango,
sin mezclarlos. Habilidad admite código (incluida HAB-000), disparador y efecto.
`pageSize` admite 1–100; sin filtros recorre todo el catálogo administrativo.
Sin coincidencias devuelve una lista vacía.

Para continuar enviar solo `searchId`, `after` y `pageSize`.
`nextPosition=null` indica el final. La sesión dura 30 minutos, pertenece al
usuario y fija IDs y orden, aunque se creen cartas entre páginas. Los atributos
se leen al consultar cada página. Las sesiones vencidas se limpian cada cinco
minutos. Una sesión ajena, inexistente o vencida devuelve 404.

### Dónde implementar cambios

| Ruta | Responsabilidad |
| --- | --- |
| frontend/src/pages | Pantallas y validaciones de presentación |
| frontend/src/services | HTTP, token e imágenes; centralizar llamadas en catalogApi.ts |
| frontend/src/types/catalogs.ts | Contratos y valores del cliente |
| frontend/src/mocks | Simulación y datos de demostración |
| backend/src/PokeArena.Api | Controllers, formularios HTTP, permisos y configuración |
| backend/src/PokeArena.Application | Contratos y validadores |
| backend/src/PokeArena.Domain | Modelos del dominio |
| backend/src/PokeArena.Infrastructure | SQL, transacciones, idempotencia y archivos |
| database/postgres/migrations | Cambios de esquema para bases existentes |

Agregar un archivo a Application/Cards no publica una ruta REST: definir
la acción en Controllers y registrar las dependencias en Program.cs.
Mantener validación en el servidor, SQL parametrizado y CancellationToken.
Cambiar contratos, mocks y pruebas juntos.

## 6. Compilación y pruebas

### Frontend

```powershell
npm.cmd --prefix frontend run test:mocks
npm.cmd --prefix frontend run lint
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend run preview -- --port 5173 --strictPort
```

Las 38 pruebas de mocks verifican filtros, paginación, validaciones y reintentos.
Sustituyen la generación de miniaturas; no prueban el backend.
`preview` requiere build y puerto libre, usa API real y no es un servidor
de producción. La URL de la API se incorpora al compilar.

### Backend sin Docker

```powershell
dotnet restore backend/PokeArena.sln
dotnet test backend/PokeArena.sln --no-restore
dotnet build backend/PokeArena.sln -c Release --no-restore
dotnet test backend/tests/PokeArena.Tests/PokeArena.Tests.csproj --collect:"XPlat Code Coverage" --results-directory "$env:TEMP/PokeArena-TestResults"
```

Incluye pruebas de validadores e imágenes reales PNG/JPG/WebP. Las pruebas
de integración aparecen omitidas si no se habilita su ejecución explícita.

### Integración con Docker

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-integration.ps1
```

El script crea **un contenedor PostgreSQL temporal**, aplica el esquema y la
migración, prueba la API con JWT firmados y elimina contenedor y archivos al
terminar. No consulta ni borra las bases de desarrollo. Verifica permisos,
formularios multipart, imágenes, creación/búsqueda, concurrencia, idempotencia,
rollback y paginación de **3.000 cartas** mientras se crea otra.

### Comprobación manual desde la web

1. Conectar un JWT administrativo; Crear Carta debe cargar las habilidades.
2. Crear una carta con una imagen válida y buscarla combinando nombre, tipo,
   costo/poder y habilidad. Debe mostrar imagen, estado y parámetros.
3. Probar nombre sin tildes, combinación sin resultados, filtros inválidos y
   paginación. Crear otra carta mientras se recorre una búsqueda.
4. Crear una ubicación con cada efecto; comprobar límite 0, energía según
   modalidad e imágenes incorrectas.
5. Desconectar y comprobar 401; usar un JWT con otro rol y comprobar 403.

Para probar solo HTTP con un JWT válido:

```powershell
$catalogToken = Read-Host 'JWT administrativo'
$catalogHeaders = @{ Authorization = "Bearer $catalogToken" }
$page = Invoke-RestMethod 'http://localhost:5003/api/cards?type=Fuego&minCost=1&maxCost=3&pageSize=20' -Headers $catalogHeaders
$page | ConvertTo-Json -Depth 6
if ($null -ne $page.nextPosition) {
    Invoke-RestMethod "http://localhost:5003/api/cards?searchId=$($page.searchId)&after=$($page.nextPosition)&pageSize=20" -Headers $catalogHeaders
}
Remove-Variable catalogToken, catalogHeaders
```

## 7. Operación y trabajo del equipo

```powershell
dotnet watch --project backend/src/PokeArena.Api run --launch-profile http
docker compose ps
docker compose logs --tail 100 postgres mongodb
docker compose exec postgres psql -U pokearena -d pokearena
docker compose exec mongodb mongosh --username pokearena --authenticationDatabase admin --password
docker compose stop
docker compose start
docker compose down
```

Elegir `dotnet run` o `watch`, no ambos a la vez. Detener API/frontend con
Ctrl+C; salir de psql con `\q` y de mongosh con `exit`.
`down` conserva los volúmenes; **`down -v` borra los datos**.
No borrar backend/files al conservar la base de datos.

Desde la rama base acordada, con el trabajo previo guardado:

```powershell
git status
git pull --ff-only
git switch -c feature/nombre-de-la-tarea
# Desarrollar y ejecutar las pruebas.
git diff --check
git add <ARCHIVOS_REVISADOS>
git diff --cached
git commit -m "Describe el cambio realizado"
git push -u origin feature/nombre-de-la-tarea
```

Revisar archivos preparados y completar la plantilla de PR con cambios,
pruebas y limitaciones. No subir secretos ni carpetas generadas. Conservar
los tres tsconfig del frontend. Al agregar dependencias npm, entregar
package.json y package-lock.json juntos.

## Problemas frecuentes

| Problema | Solución |
| --- | --- |
| Puerto 5173 ocupado | Detener la instancia anterior; identificar con Get-NetTCPConnection -LocalPort 5173 -State Listen |
| npm.ps1 bloqueado | Usar npm.cmd |
| API apunta a 5000 | Definir VITE_API_URL=http://localhost:5003 y reiniciar Vite |
| Sigue en simulación | Usar dev y VITE_MOCK_API=false; dev:mock siempre simula |
| 401 / 403 | Revisar token vigente, emisor, audiencia, identificador y rol ContentAdmin |
| Token local no funciona | Crear con user-jwts, reiniciar API y comprobar que no se configuró otro Authority |
| Falta conexión PostgreSQL | Configurar User Secrets para la API; Compose no configura ASP.NET |
| Esquema antiguo | Aplicar 001_integrate_catalog.sql; reiniciar Docker no migra |
| 409 de código | El tipo llegó a 999; acordar ampliación del formato |
| 409 de idempotencia | No reutilizar la misma clave al cambiar los datos |
| Error de imagen | Revisar contenido, peso, resolución, proporción y permisos de Storage:Root |
| Imagen perdida tras restaurar DB | Restaurar también el almacenamiento de archivos |
| Pruebas de integración omitidas | Ejecutar scripts/test-integration.ps1 con Docker iniciado |

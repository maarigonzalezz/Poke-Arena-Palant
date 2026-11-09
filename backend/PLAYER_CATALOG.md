# Catálogo y colección del jugador

Ambas rutas requieren `Authorization: Bearer <token de la plataforma>` y obtienen
el jugador del claim `sub`. No aceptan un propietario elegido por el cliente.

- `GET /api/player/catalog`: catálogo con pertenencia y número de copias.
- `GET /api/player/collection`: la misma consulta con `ownedOnly=true` obligatorio.

Parámetros opcionales: `type`, `cost` (0–10), `rarity`, `ownedOnly`,
`pageSize` (1–100, predeterminado 12) y `after` (ID no negativo, inicialmente 0).
Todos los filtros se combinan con AND antes de contar y paginar.

Ejemplo: `/api/player/catalog?type=Fuego&cost=3&pageSize=12`.
La respuesta contiene `items`, `totalCount` y `nextPosition`.
Para continuar, enviar `after=nextPosition` conservando los filtros.
`nextPosition: null` indica el final; al cambiar filtros se reinicia `after=0`.
`totalCount` cuenta todas las coincidencias, independientemente del cursor.

Cada carta incluye los campos utilizados por `PlayerCatalogCard` del frontend,
su `id`, `owned` y `quantity`. `quantity` es el valor de `player_cards.copies`,
o cero cuando no existe pertenencia. `imageUrl` usa la imagen original grande.
Solo se publican cartas activas con imagen Ready y ruta original disponible.

Los resultados vacíos devuelven 200 con `items: []`, total cero y cursor nulo.
Los filtros o parámetros inválidos devuelven 400 con errores por campo.
Las cuentas inexistentes devuelven 401 y las deshabilitadas 403.

Cada solicitud observa una instantánea consistente para el total y la página.
Entre solicitudes los datos pueden cambiar: una nueva búsqueda ve cartas nuevas
y cambios de copias. No se conserva una sesión de búsqueda administrativa.

## Base de datos y verificación

Debe estar aplicada la migración `005_player_inventory.sql` con la columna
`copies INTEGER NOT NULL DEFAULT 1`. La consulta no necesita nuevas tablas.

Pruebas unitarias: `dotnet test backend/PokeArena.sln --filter Category!=Integration`.
Pruebas con PostgreSQL desechable: ejecutar `scripts/test-integration.ps1` con Docker
disponible. Estas pruebas no usan la base local del jugador.

El ingreso con Google conecta estos endpoints con `PlayerExperiencePage`: el token
permanece en memoria y se limpia al salir o vencer. Catálogo y colección cargan páginas
de doce cartas, con estados de carga, errores y reintentos. Las imágenes relativas se
resuelven contra la API. La demostración del estudio administrativo conserva sus datos
simulados; las compras simuladas están deshabilitadas en la experiencia autenticada.

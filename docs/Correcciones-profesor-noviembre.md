# Correcciones de revisión

## Asignación solo al recibir la colección

- `InventoryService.EnsureInitialCollectionAsync` devuelve `grantedNow=true` únicamente cuando la transacción acaba de entregar las cartas y los cubos.
- Los ingresos posteriores devuelven `Completed` con `grantedNow=false`.
- `LoginPage.tsx` abre la bienvenida solo con esa señal; los demás ingresos llevan directamente al catálogo.
- Si el catálogo era insuficiente, la bienvenida aparece cuando se completa la entrega en un ingreso posterior.
- Las cuentas con entrega pendiente muestran el mensaje recibido del backend.

## Cartas reutilizables y miniaturas

- `PlayerCard.tsx` comparte la presentación entre asignación, catálogo, colección y mazos.
- La variante `mini` usa `imageSmallUrl`; la variante completa usa `imageUrl`.
- `CardImage.tsx` resuelve rutas contra la API y muestra un texto accesible si falta el archivo.
- La búsqueda administrativa también reutiliza `CardImage`.
- `PlayerCatalogItem` incluye ahora la ruta de la miniatura guardada; no se reconstruye ni se sustituye por iconos.

## Reglas en la capa lógica

- `InventoryService` valida identidad, cuenta activa, cursor, entrega anterior, catálogo suficiente y premio; también selecciona las cartas y arma el mazo sugerido.
- `PostgresInventoryRepository` consulta datos y ejecuta las escrituras solicitadas por el servicio.
- `WithPlayerLockAsync` mantiene la transacción y `FOR UPDATE` durante la decisión del servicio para evitar entregas parciales o duplicadas.
- `PlayerCatalogService` valida la cuenta; el repositorio de catálogo implementa filtros, joins y paginación SQL.
- `PlayerCatalogRules` define la visibilidad de cartas; el repositorio recibe la condición y la traduce a SQL.
- `PlayerAccess` comparte la comprobación de cuentas activas entre los casos de uso.
- `Player` concentra los valores de alta y las reglas de actualización del perfil; `CardCodes` define consecutivos y formato.
- La búsqueda informa desde `CardService` que una sesión no existe; el repositorio devuelve ausencia de datos.

## Errores y constantes

- `UnhandledExceptionHandler` centraliza respuestas 500 con mensaje genérico y un identificador de trazabilidad.
- `CatalogExceptionFilter` conserva 400 para validaciones, 401/403 para acceso, 409 para conflictos y 503 para fallos conocidos de infraestructura.
- Los controladores delegan las reglas al servicio; la lectura de identidad, el enlace de parámetros y la autenticación HTTP permanecen en la API.
- `GameRules.cs` concentra cantidades de entrega, cubos, mazo, páginas y duración de búsquedas.
- `gameRules.ts` concentra cantidades usadas en las pantallas; las constantes de frontend no determinan premios en el servidor.

## Comprobación manual

1. Ingresar con una cuenta que aún no recibió la colección: muestra la asignación con miniaturas y 1.000 cubos.
2. Cerrar sesión e ingresar con la misma cuenta: abre el catálogo sin repetir la asignación.
3. Abrir colección y mazos: reutilizan la misma carta con imagen grande y pertenencia real.
4. Con menos de 15 comunes, la API comunica entrega pendiente; tras completar el catálogo, el siguiente ingreso entrega una sola vez.
5. Revisar filtros y cursores inválidos: responden 400; sin credenciales, 401; cuenta deshabilitada, 403.

Las pruebas de integración usan PostgreSQL temporal real. No se requieren migraciones nuevas ni modificaciones a los inventarios existentes.

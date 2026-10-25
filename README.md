# Poké-Arena — Guía de desarrollo

## 1. Descripción del proyecto

Poké-Arena utiliza React, TypeScript y Vite para el frontend, ASP.NET Core 9 para el backend, y PostgreSQL y MongoDB para el almacenamiento.

El desarrollo contempla tres funcionalidades administrativas: **Buscar Carta, Crear Carta y Crear Ubicación**.

## 2. Estado del desarrollo

| Funcionalidad | Estado |
|---|---|
| Buscar Carta | Interfaz, filtros combinados y API implementados |
| Paginación | Implementada mediante sesiones de búsqueda |
| Crear Carta | Formulario y simulación implementados; backend pendiente |
| Crear Ubicación | Validación, endpoint y persistencia PostgreSQL implementados; imagen e idempotencia pendientes |
| Catálogo de habilidades | Simulado; endpoint real pendiente |
| Autenticación | API protegida mediante JWT; integración con frontend pendiente |
| PostgreSQL | Esquema y consultas de búsqueda disponibles |
| MongoDB | Configurado, pero sin integración |

## 3. Ejecución del proyecto

Los comandos se ejecutan desde la raíz del repositorio en PowerShell.

### Frontend en modo simulación

```powershell
npm.cmd --prefix frontend run dev:mock
```

Acceda a `http://localhost:5173`.

Este modo permite probar las tres interfaces sin ejecutar el backend. Incluye 21 cartas de prueba y permite guardar cartas y ubicaciones simuladas en el navegador.

### Bases de datos

Configure las contraseñas locales y ejecute:

```powershell
docker compose up -d
docker compose ps
```

PostgreSQL utiliza el puerto `5432` y MongoDB el `27017`.

### Backend

```powershell
dotnet run --project backend/src/PokeArena.Api --launch-profile http
```

La API utiliza `http://localhost:5003`.

La conexión a PostgreSQL debe configurarse mediante User Secrets o variables de entorno.

### Frontend conectado al backend

```powershell
$env:VITE_API_URL = 'http://localhost:5003'
$env:VITE_MOCK_API = 'false'

npm.cmd --prefix frontend run dev
```

La búsqueda real requiere autenticación JWT con el rol `ContentAdmin`.

## 4. Contratos de la API

| Endpoint | Función |
|---|---|
| `GET /api/cards` | Buscar cartas mediante filtros |
| `GET /api/abilities` | Consultar habilidades disponibles; pendiente |
| `POST /api/cards` | Crear una carta; pendiente |
| `POST /api/locations` | Crear una ubicación (requiere rol `ContentAdmin`) |

Los formularios de creación envían los datos mediante `FormData` y el encabezado `Idempotency-Key`. La persistencia de idempotencia aún está pendiente.

### Buscar Carta

Permite combinar filtros por código, nombre, costo, poder, tipo, rareza y habilidad.

- Los filtros se combinan mediante AND.
- La búsqueda por nombre admite coincidencias parciales sin distinguir mayúsculas ni tildes.
- El costo permitido es de 0 a 10 y el poder de −20 a 20.
- La paginación utiliza `searchId`, `after` y `pageSize`.
- Los filtros inválidos deben generar mensajes de error.

### Crear Carta

Debe validar nombre, tipo, rareza, costo, poder, habilidad e imagen.

- La habilidad debe pertenecer al catálogo y ser compatible con el tipo de carta.
- Las imágenes admiten PNG, JPG y WebP, máximo 5 MB.
- La proporción requerida es 5:7, con resolución mínima de 600 × 840.
- El backend debe generar el código único y procesar la miniatura.

### Crear Ubicación

La creación de ubicaciones ya está conectada desde el formulario hasta PostgreSQL:

1. El frontend valida los datos y, en modo simulado, requiere una imagen.
2. En modo API, envía los campos de la ubicación como `multipart/form-data` a `POST /api/locations`.
3. La API exige un JWT con el rol `ContentAdmin`, valida los campos y devuelve `400 Bad Request` cuando encuentra errores.
4. El repositorio guarda la ubicación y su versión inicial dentro de una transacción. Si se crea correctamente, la API responde `201 Created`.

El formulario real todavía no carga ni persiste la imagen. El encabezado `Idempotency-Key` se envía desde el frontend, pero el endpoint aún no implementa su procesamiento; un reintento puede crear otra ubicación.

- Capacidad: 0 a 5 cartas.
- Energía fija: 0 a 3.
- Energía por carta: 0 a 2.
- Efectos: bonificación por tipo, modificación de costo, restricción por turno o sin efecto.
- El nombre es obligatorio y admite hasta 120 caracteres; la descripción también es obligatoria.
- Los tipos válidos para los parámetros de efecto son `Fuego`, `Agua`, `Planta`, `Eléctrico`, `Psíquico` y `Lucha`.
- `TypeBonus` requiere `type` y `power` (entero entre −5 y 5). `CostModifier` requiere `amount` (entero entre −5 y 5) y admite `affectedType` válido o `null`. `TurnRestriction` requiere una lista no vacía de turnos únicos entre 1 y 6.
- Los parámetros del efecto se reciben como un objeto JSON en el campo `effectParameters`.
- El estado de imagen se inicializa en `Pending`; la imagen 4:3, mínimo 800 × 600 y máximo 5 MB aún no se almacena ni procesa.

#### Campos enviados al crear una ubicación

El formulario envía estos campos como `multipart/form-data`:

| Campo | Tipo | Ejemplo |
|---|---|---|
| `name` | Texto | `Volcán` |
| `description` | Texto | `Una ubicación volcánica.` |
| `maxCards` | Entero | `4` |
| `energyMode` | `Fixed` o `PerCard` | `Fixed` |
| `extraEnergy` | Entero | `2` |
| `effectType` | `None`, `TypeBonus`, `CostModifier` o `TurnRestriction` | `TypeBonus` |
| `effectParameters` | Objeto JSON serializado como texto | `{"type":"Fuego","power":2}` |

Ejemplos de `effectParameters` por tipo:

```json
{"type":"Fuego","power":2}
{"amount":-1,"affectedType":null}
{"turns":[1,3,5]}
{}
```

Los ejemplos corresponden, en orden, a `TypeBonus`, `CostModifier`, `TurnRestriction` y `None`. La tabla `locations` almacena la configuración actual; `location_versions` conserva una instantánea JSON de la versión inicial (versión `1`). Las rutas de imagen quedan vacías hasta que se implemente el almacenamiento.

#### Componentes añadidos

- `CreateLocationRequest` define los campos aceptados por la API.
- `LocationValidator` valida los campos generales y la estructura específica de cada efecto.
- `LocationsController` expone `POST /api/locations`, aplica autorización y convierte errores de validación en una respuesta `400`.
- `ILocationRepository` separa el contrato de persistencia de la aplicación; `PostgresLocationRepository` inserta la ubicación y su instantánea de versión en una única transacción.
- `Location` representa la entidad persistida, incluyendo el estado de imagen, la versión vigente y si está activa.
- `LocationValidatorTests` cubre entradas válidas, límites y parámetros de efecto inválidos.

## 5. Pruebas

### Frontend

```powershell
npm.cmd --prefix frontend run test:mocks
npm.cmd --prefix frontend run lint
npm.cmd --prefix frontend run build
```

### Backend

```powershell
dotnet restore backend/PokeArena.sln
dotnet build backend/PokeArena.sln --no-restore
dotnet test backend/PokeArena.sln --no-build
```

### Pruebas manuales recomendadas

| Prueba | Resultado esperado |
|---|---|
| Buscar sin filtros | 21 cartas |
| Fuego y costo entre 1 y 3 | Charmander y Volcán Ágil |
| Agua, rareza Rara y poder mínimo 10 | Blastoise y Marea Fría |
| Nombre `VOLCAN` | Volcán Ágil |
| Nombre inexistente | Sin resultados |
| Rangos inválidos | Mensaje de validación |
| Crear carta simulada | Debe aparecer en Buscar Carta |
| Crear ubicación simulada | Debe guardarse correctamente |

Las pruebas de simulación no sustituyen las pruebas de integración con la API y las bases de datos.

## 6. Trabajo pendiente

1. Implementar `GET /api/abilities` y completar la autenticación administrativa.
2. Desarrollar el backend de Crear Carta.
3. Implementar idempotencia persistente, procesamiento de imágenes y versionado de cartas.
4. Integrar MongoDB y la sincronización de datos.
5. Completar las pruebas reales de autorización, consultas, concurrencia y persistencia.
6. Verificar el funcionamiento con múltiples instancias de la API.

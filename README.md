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
| Crear Ubicación | Formulario y simulación implementados; backend pendiente |
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
| `POST /api/locations` | Crear una ubicación; pendiente |

Los endpoints de creación reciben los datos mediante `FormData` y utilizan el encabezado `Idempotency-Key`.

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

Debe validar nombre, descripción, capacidad, energía, efecto e imagen.

- Capacidad: 0 a 5 cartas.
- Energía fija: 0 a 3.
- Energía por carta: 0 a 2.
- Efectos: bonificación por tipo, modificación de costo, restricción por turno o sin efecto.
- Imagen 4:3, mínimo 800 × 600 y máximo 5 MB.

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
2. Desarrollar el backend de Crear Carta y Crear Ubicación.
3. Implementar idempotencia persistente, procesamiento de imágenes y versionado.
4. Integrar MongoDB y la sincronización de datos.
5. Completar las pruebas reales de autorización, consultas, concurrencia y persistencia.
6. Verificar el funcionamiento con múltiples instancias de la API.
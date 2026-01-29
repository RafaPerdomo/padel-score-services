# Pádel Score API

Backend .NET 8 para persistencia de partidos de pádel en PostgreSQL.

## Stack Técnico

- **.NET 8** - Minimal APIs
- **PostgreSQL** - Base de datos (Neon DB)
- **Dapper** - Micro-ORM
- **Swagger** - Documentación API

## Características

- ✅ Persistencia de matches con estado completo (JSONB)
- ✅ Registro de eventos append-only
- ✅ Optimistic concurrency control
- ✅ Soporte multi-dispositivo (resume)
- ✅ Regla: 1 solo match LIVE por userId

## Estructura del Proyecto

```
src/PadelApi/
├── Program.cs                    # Minimal API endpoints
├── Models/                       # Entidades de dominio
├── DTOs/                         # Request/Response DTOs
├── Repositories/                 # Acceso a datos con Dapper
├── Services/                     # Lógica de negocio
└── Exceptions/                   # Excepciones custom
```

## Base de Datos

### Tablas

1. **users** - Usuarios (relojes)
2. **matches** - Partidos
3. **match_state** - Estado actual del partido (JSONB + version)
4. **match_events** - Eventos append-only (POINT, UNDO, START, MATCH_END)

### Setup

#### Opción 1: Neon DB (Producción)

La aplicación está configurada para usar Neon DB en producción. El connection string ya está en `appsettings.Production.json`.

```bash
# Ejecutar schema en Neon DB
psql 'postgresql://neondb_owner:npg_ETbmV1DeuLI0@ep-royal-fire-ahxc0v77-pooler.c-3.us-east-1.aws.neon.tech/PadelScoreDB?sslmode=require&channel_binding=require' -f db/schema.sql
```

#### Opción 2: Postgres Local (Desarrollo)

```bash
# 1. Levantar Postgres con Docker
docker-compose up -d

# 2. El schema se ejecuta automáticamente al iniciar el contenedor
# O ejecutarlo manualmente:
docker exec -i padel-postgres psql -U padel -d padeldb < db/schema.sql
```

## Ejecutar la API

### Desarrollo (Postgres Local)

```bash
cd src/PadelApi
dotnet restore
dotnet run
```

### Producción (Neon DB)

```bash
cd src/PadelApi
dotnet run --environment Production
```

La API estará disponible en:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger: `https://localhost:5001/swagger`

## Endpoints

### 1. Upsert User
```http
POST /users/{userId}
Content-Type: application/json

{
  "name": "Juan Pérez",
  "email": "juan@example.com"
}
```

### 2. Crear Match
```http
POST /matches
Content-Type: application/json

{
  "userId": "watch-123",
  "mode": "PRO",
  "goldenPoint": true,
  "players": ["Juan", "Pedro", "Luis", "Ana"],
  "initialState": { ... }  // opcional
}
```

**Comportamiento:**
- Si ya existe un match LIVE para el userId → devuelve el existente (200)
- Si no existe → crea nuevo match (201)

### 3. Recuperar Match Activo
```http
GET /matches/active?userId=watch-123
```

### 4. Registrar Punto
```http
PUT /matches/{matchId}/point
Content-Type: application/json

{
  "userId": "watch-123",
  "winner": "A",
  "expectedVersion": 12,
  "newState": { ... }
}
```

**Respuesta 409 (Conflict):**
```json
{
  "error": "Version conflict",
  "details": {
    "currentVersion": 14,
    "currentState": { ... }
  }
}
```

### 5. Undo
```http
POST /matches/{matchId}/undo
Content-Type: application/json

{
  "userId": "watch-123",
  "expectedVersion": 13,
  "newState": { ... }
}
```

### 6. Snapshot State
```http
PUT /matches/{matchId}/state
Content-Type: application/json

{
  "userId": "watch-123",
  "expectedVersion": 13,
  "state": { ... }
}
```

### 7. Finish Match
```http
POST /matches/{matchId}/finish
Content-Type: application/json

{
  "userId": "watch-123",
  "won": true,
  "expectedVersion": 20,
  "finalStats": {
    "duration": 3600,
    "finalScore": "6-4,6-2",
    "healthStats": {
      "calories": 450,
      "steps": 3000
    }
  }
}
```

## Testing

### Con VS Code REST Client

Abre `requests.http` y ejecuta las requests.

### Con curl

```bash
# Crear usuario
curl -X POST https://localhost:5001/users/watch-123 \
  -H "Content-Type: application/json" \
  -d '{"name":"Juan","email":"juan@example.com"}' \
  -k

# Crear match
curl -X POST https://localhost:5001/matches \
  -H "Content-Type: application/json" \
  -d '{"userId":"watch-123","mode":"PRO","goldenPoint":true,"players":["A","B","C","D"]}' \
  -k
```

### Con Swagger

Navega a `https://localhost:5001/swagger` y prueba los endpoints interactivamente.

## Reglas de Negocio

### 1 LIVE por userId
Solo puede existir un match con status LIVE por userId. Si se intenta crear otro, la API devuelve el existente.

### Optimistic Concurrency
Todos los updates de `match_state` requieren `expectedVersion`. Si la versión no coincide, se devuelve 409 Conflict con el estado actual.

### Validación de Ownership
Todas las operaciones validan que `match.user_id = request.userId`. Si no coincide, se devuelve 403 Forbidden.

### Transaccionalidad
Las operaciones de punto/undo son transaccionales:
1. Insertar evento en `match_events`
2. Actualizar `match_state` con optimistic concurrency

Si falla el update de state, el evento NO se inserta (rollback).

## Configuración

### Variables de Entorno

```bash
# Connection string (sobrescribe appsettings)
export ConnectionStrings__Default="Host=...;Database=...;Username=...;Password=..."

# Ambiente
export ASPNETCORE_ENVIRONMENT=Production
```

### appsettings

- `appsettings.json` - Configuración base
- `appsettings.Development.json` - Postgres local
- `appsettings.Production.json` - Neon DB

## Troubleshooting

### Error: "relation does not exist"
Ejecuta el schema SQL:
```bash
psql <connection-string> -f db/schema.sql
```

### Error: "SSL connection required"
Asegúrate de que el connection string incluya `SSL Mode=Require` para Neon DB.

### Error: "Version conflict"
El cliente tiene una versión desactualizada. Obtén el estado actual del match y reintenta con la versión correcta.

## Próximos Pasos

- [ ] Agregar autenticación (JWT)
- [ ] Implementar paginación en eventos
- [ ] Agregar endpoint para obtener historial de matches
- [ ] Implementar soft delete para matches
- [ ] Agregar health checks
- [ ] Agregar métricas y observabilidad

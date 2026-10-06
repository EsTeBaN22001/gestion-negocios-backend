# Backend — Sistema de Gestión de Negocios

API REST desarrollada con ASP.NET Core .NET 8.

## Tecnología

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core 8.0.13
- Pomelo.EntityFrameworkCore.MySql 8.0.3
- Swagger / OpenAPI (Swashbuckle 6.6.2)

## Estructura

```text
backend/
├── GestionNegocios.sln
├── Dockerfile
├── .gitignore
├── .dockerignore
├── README.md
└── src/
    └── GestionNegocios.Api/
        ├── Controllers/
        ├── Data/
        ├── DTOs/
        ├── Helpers/
        ├── Middleware/
        ├── Models/
        ├── Services/
        ├── Program.cs
        ├── appsettings.json
        └── appsettings.Development.json
```

## Ejecución local

### Requisitos

- .NET 8 SDK
- MySQL corriendo en localhost:3306

### Configuración

Copiar y ajustar la cadena de conexión:

```bash
# El archivo appsettings.Development.json se usa en modo Development.
# Para sobreescribir sin tocar el repo, usar User Secrets o variables de entorno:
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=gestion_negocios_dev;User=appuser;Password=tu_password;"
```

### Comandos

```bash
# Restaurar dependencias
dotnet restore

# Compilar
dotnet build

# Ejecutar migraciones (cuando existan)
dotnet ef database update --project src/GestionNegocios.Api

# Ejecutar API
dotnet run --project src/GestionNegocios.Api
```

La API arranca en: `http://localhost:5000`

Swagger UI: `http://localhost:5000/swagger`

Health check: `http://localhost:5000/api/health`

Health check DB: `http://localhost:5000/api/health/db`

## Ejecución con Docker

Ver `docker-compose.yml` en la raíz del workspace.

```bash
docker compose up --build
```

## Variables de entorno

| Variable | Descripción |
|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena de conexión MySQL |
| `ASPNETCORE_ENVIRONMENT` | `Development` o `Production` |
| `Cors__AllowedOrigins__0` | URL del frontend permitida |

## Puertos

| Entorno | Puerto |
|---|---|
| Local | 5000 |
| Docker | 8080 (interno), mapeado a 5000 |

## Rama activa

`develop`

# MS.Cobroup.Backend

Microservicio operativo de COBROUP. Esta fase contiene únicamente la base hexagonal, el contrato inicial de alcance y un health check; no incluye CRUD, persistencia ni motor financiero.

## Requisitos

- .NET SDK 10.0.401 (LTS, fijado en `global.json`).

## Ejecutar

```powershell
dotnet restore MS.Cobroup.Backend.slnx
dotnet build MS.Cobroup.Backend.slnx --no-restore
dotnet test MS.Cobroup.Backend.slnx --no-build
dotnet run --project MS.Cobroup.Backend.Api --urls http://127.0.0.1:5081
```

El endpoint técnico es `GET /health`. La autenticación real, OpenAPI de negocio y persistencia se definirán en fases posteriores.

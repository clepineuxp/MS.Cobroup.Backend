# MS.Cobroup.Backend

Microservicio operativo de COBROUP. En fase 2 valida los access tokens emitidos por `MS.Cobroup.BackendAdmin` y exige contexto de organización en los recursos protegidos; todavía no contiene CRUD ni lógica financiera.

## Requisitos

- .NET SDK 10.0.401 (LTS, fijado en `global.json`).

## Ejecutar

```powershell
dotnet restore MS.Cobroup.Backend.slnx
dotnet build MS.Cobroup.Backend.slnx --no-restore
dotnet test MS.Cobroup.Backend.slnx --no-build
dotnet run --project MS.Cobroup.Backend.Api --urls http://127.0.0.1:5081
```

El endpoint técnico público es `GET /health`. `GET /api/v1/session/me` exige un bearer token válido con `sub`, audiencia `cobroup-api` y `organization_id`. El issuer se configura con `Identity__Issuer`.

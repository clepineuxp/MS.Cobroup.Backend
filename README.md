# MS.Cobroup.Backend

Microservicio operativo de COBROUP. Valida los access tokens emitidos por `MS.Cobroup.BackendAdmin`, exige contexto de organización y en fase 3 publica la lectura de rutas autorizadas sin duplicar su propiedad. Todavía no contiene CRUD de clientes ni lógica financiera.

## Requisitos

- .NET SDK 10.0.401 (LTS, fijado en `global.json`).

## Ejecutar

```powershell
dotnet restore MS.Cobroup.Backend.slnx
dotnet build MS.Cobroup.Backend.slnx --no-restore
dotnet test MS.Cobroup.Backend.slnx --no-build
dotnet run --project MS.Cobroup.Backend.Api --urls http://127.0.0.1:5081
```

Los endpoints se exponen mediante controladores separados por responsabilidad. `GET /health` es público; `GET /api/v1/session/me` y `GET /api/operations/v1/routes` exigen un bearer token válido con `sub`, audiencia `cobroup-api` y `organization_id`. La lectura de rutas reenvía el token al contrato `GET /api/v1/route-access` de BackendAdmin, devuelve solo rutas activas autorizadas y convierte indisponibilidad del servicio propietario en `503`. El issuer se configura con `Identity__Issuer` y la autoridad administrativa con `AdministrativeApi__BaseUrl`.

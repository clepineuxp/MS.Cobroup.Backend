# MS.Cobroup.Backend

Microservicio operativo de COBROUP. Valida los access tokens JWT emitidos exclusivamente por `MS.Cobroup.BackendAdmin`, exige contexto de organización y consume rutas autorizadas sin duplicar su propiedad. En fase 4 es la autoridad de clientes, teléfonos y ubicaciones; todavía no contiene lógica financiera.

Clientes: `GET|POST /api/operations/v1/customers` y `GET|PUT /api/operations/v1/customers/{id}`. Todas las operaciones aplican tenant y alcance de ruta en servidor; el cupo activo se valida transaccionalmente.

## Requisitos

- .NET SDK 10.0.401 (LTS, fijado en `global.json`).

## Ejecutar

```powershell
dotnet restore MS.Cobroup.Backend.slnx
dotnet build MS.Cobroup.Backend.slnx --no-restore
dotnet test MS.Cobroup.Backend.slnx --no-build
dotnet run --project MS.Cobroup.Backend.Api --urls http://127.0.0.1:5081
```

Los endpoints se exponen mediante controladores separados por responsabilidad. `GET /health` es público; `GET /api/v1/session/me` y `GET /api/operations/v1/routes` exigen un bearer JWT válido con `typ=access`, `sub`, `iss`, audiencia `cobroup-api`, `exp`, `iat`, `nbf`, `jti`, `organization_id` y rol. La firma se valida de forma asimétrica con la clave pública publicada por BackendAdmin en `/api/v1/auth/.well-known/jwks.json`; este servicio no recibe ni conserva la clave privada. Debe rechazar algoritmo, emisor, audiencia, firma, vigencia o `kid` inválidos y no puede usar HMAC, claves por defecto ni fallbacks silenciosos. La lectura de rutas reenvía el token al contrato `GET /api/v1/route-access` de BackendAdmin, devuelve solo rutas activas autorizadas y convierte indisponibilidad del servicio propietario en `503`. El issuer se configura con `Identity__Issuer` y la autoridad administrativa con `AdministrativeApi__BaseUrl`.

El contrato completo y los criterios auditables están en [`DECISION_AUTENTICACION_JWT.md`](../cobroup.md/DECISION_AUTENTICACION_JWT.md). La implementación actual permanece pendiente de conformidad y de una nueva auditoría; esta descripción no constituye aprobación técnica.

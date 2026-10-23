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

Los endpoints se exponen mediante controladores separados por responsabilidad. `GET /health` es público; `GET /api/v1/session/me` y `GET /api/operations/v1/routes` exigen un bearer JWT válido con `typ=access`, `sub`, `iss`, audiencia `cobroup-api`, `exp`, `iat`, `nbf`, `jti`, `organization_id` y rol. La firma se valida localmente y exclusivamente con `HS256`, usando `Jwt__SigningKeyBase64`, el mismo secreto del ambiente que recibe BackendAdmin. Este servicio no emite tokens. Debe rechazar algoritmo alternativo, emisor, audiencia, firma, vigencia o `kid` inválidos, así como secretos ausentes, mal codificados o menores de 32 bytes; no puede usar claves por defecto ni fallbacks. La lectura de rutas reenvía el token al contrato `GET /api/v1/route-access` de BackendAdmin, devuelve solo rutas activas autorizadas y convierte indisponibilidad del servicio propietario en `503`. El issuer se configura con `Identity__Issuer` y la autoridad administrativa con `AdministrativeApi__BaseUrl`.

El contrato completo de identidad y los criterios auditables están en [`DECISION_AUTENTICACION_JWT.md`](../cobroup.md/DECISION_AUTENTICACION_JWT.md).

## Almacenamiento durable de evidencias y archivos

Conforme a [`DECISION_ALMACENAMIENTO_ARCHIVOS.md`](../cobroup.md/DECISION_ALMACENAMIENTO_ARCHIVOS.md) y [ADR-0004](docs/decisions/0004-durable-file-storage-pvc.md):
- Todas las fotografías de evidencias y archivos binarios se almacenan actualmente en un **PersistentVolumeClaim (PVC)** de Kubernetes mediante el adaptador `FileSystemObjectStorage` (`Storage:Provider=FileSystem`).
- La arquitectura hexagonal mantiene el puerto agnóstico `IFileStorageService` en Domain desacoplado de la infraestructura; la clave canónica normalizada es `tenants/{tenantId}/{category}/{aggregateId}/{sha256}.{ext}` (con allowlist de servidor y compatibilidad retrocompatible con claves legadas `{tenantId}/{paymentId}/{sha256}.{ext}`).
- En producción con dos réplicas (`replicas: 2`), el PVC utiliza el modo de acceso compartido concurrente **`ReadWriteMany` (RWX)**.
- El streaming realiza validación estructural completa de imágenes (`ImageStructuralValidator`: SOI/SOF/EOI, IHDR/IEND, VP8/VP8L/VP8X, máx 8192px contra decompression bombs), límite de 10 MB, hash SHA-256 incremental y publicación atómica con `File.Move(..., overwrite: false)` (idempotente si el hash coincide, error de integridad si difiere).
- La arquitectura permite sustituir el adaptador por AWS S3 en el futuro mediante configuración (`Storage:Provider=S3`), sin alterar Domain, Core ni los controladores HTTP.

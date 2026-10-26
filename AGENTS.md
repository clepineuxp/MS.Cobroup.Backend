# AGENTS — MS.Cobroup.Backend

## Identidad y restricciones del proyecto

- Producto: COBROUP
- Rol: microservicio operativo; autoridad futura de Customer, Loan, Installment, Payment y PaymentEvidence.
- Documento maestro: `../cobroup.md/COBROUP.md` 1.15.0 y `../cobroup.md/DECISION_AUTENTICACION_JWT.md`.
- Estándar: `../cobroup.md/FRAMEWORK_DESARROLLO_CSHARP.md` 2.0.
- Rama activa: `feature/customer-identification`, creada desde `origin/dev`; árbol limpio al iniciar.
- Prohibidos push, merge, tag y PR sin autorización.

## Estado actual

- Fase: 9
- Resultado: IDENTIDAD_JWT_REABIERTA_PARA_ADECUACION_Y_AUDITORIA
- Plataforma: .NET SDK 10.0.401, `net10.0`, C# 14.
- Auditoría independiente: realizada el 2026-10-22; resultado técnico NO APROBADO.
- Línea base funcional versionada; autenticación JWT exclusiva pendiente de conformidad. Auditoría técnica: `../cobroup.md/informe/auditoria-backend-csharp-2026-10-22-v2.html`; adenda vigente: `../cobroup.md/informe/linea-base-auditoria-jwt-2026-10-22.html`.
- Corte actual: el árbol contiene cambios JWT sin commit. No se autoriza cierre, commit ni despliegue por el solo hecho de compilar.

## Decisión vigente de autenticación

- Se usarán exclusivamente access y refresh tokens JWT conforme a `../cobroup.md/DECISION_AUTENTICACION_JWT.md`.
- Este servicio es la API operativa y validador JWT; no autentica contraseñas, no emite tokens y nunca genera credenciales.
- Valida exclusivamente firma simétrica `HS256`, `kid`, issuer, audience (`cobroup-api`), tipo (`access`), tiempos y claims obligatorios (`sub`, `organization_id`, `role`, `jti`, `iat`, `nbf`, `exp`) usando el secreto simétrico inyectado por ambiente vía `jwt-signing-secret` (`Jwt__SigningKeyBase64`, `Jwt__ActiveKeyId`, y opcionalmente `Jwt__PreviousValidationKeys__0__KeyId`, `Jwt__PreviousValidationKeys__0__SigningKeyBase64`).
- Se prohíbe terminantemente la emisión de JWT en este servicio, el uso de JWKS/OIDC/RSA en runtime, claves/defaults de respaldo, `ValidateIssuer=false`, `ValidateAudience=false`, `alg=none` o algoritmos no autorizados.
- El access token vive solo en memoria del frontend.
- Toda operación mantiene autorización tenant/rol/recurso aunque el JWT sea válido.
- Almacenamiento de archivos: implementa puerto agnóstico (`IFileStorageService`) sobre PVC durable con clave canónica `tenants/{tenantId}/{category}/{aggregateId}/{sha256}.{ext}`, validación estructural de imágenes contra decompression bombs, pertenencia estricta a root, atomicidad y prevención de symlinks/reparse points.
- Las referencias de identidad anteriores dentro del historial append-only son evidencia histórica y están supersedidas; no son planeación ni criterio de aceptación vigente.

## Historial cronológico (append-only)

### 2026-10-21 — Inicialización fase 1

- Prompt: inicializar exactamente cuatro repositorios COBROUP, sin lógica de negocio completa.
- Autorización Git: el usuario autorizó `feature/fase-1-inicializacion`; commit autorizado: NO.
- Alcance: solución hexagonal, API, health check, contratos preliminares, pruebas y documentación.
- Decisiones: API única; sin Worker, base de datos, broker, proveedor de identidad ni endpoints CRUD.
- Archivos: solución, Domain/Core/Shared/Infrastructure/Api, pruebas, `README.md`, `.gitignore`, `global.json`, documentos y este registro.
- Comandos iniciales: `dotnet new`, `dotnet sln add`, `dotnet add reference`; completados.
- Verificación final: `dotnet restore`, `dotnet build --no-restore` (0 warnings, 0 errors), `dotnet test --no-build` (5/5) y API temporal; `GET http://127.0.0.1:5081/health` respondió 200 `Healthy`. El escaneo de residuos/secretos conocidos no encontró coincidencias.
- Bloqueos/riesgos: ADR-001 a ADR-012 siguen pendientes; no hay proveedor de identidad ni persistencia.
- Próxima acción sugerida: cerrar fase 1 y solicitar aprobación explícita para identidad/tenant.

### 2026-10-21 — Renombrado y redirección GitHub

- Prompt: cambiar la carpeta backend a `MS.Cobroup.Backend` y redirigirla al repositorio GitHub indicado.
- Rama: `feature/fase-1-inicializacion`; commit autorizado: NO.
- Cambios: carpeta renombrada desde `MS.cobroup-backend`; `origin` actualizado a `https://github.com/clepineuxp/MS.Cobroup.Backend.git`; referencias documentales sincronizadas.
- Validación: rama, estado local y remoto verificados después del movimiento; no se ejecutó push.

### 2026-10-21 — Cierre versionado de fase 1

- Prompt: realizar commits de fase 1 y pasar a fase 2.
- Resultado: fase 1 validada; commit autorizado: SÍ, únicamente para el alcance acumulado de fase 1.
- Operaciones no autorizadas: push, merge, tag y PR.
- Siguiente rama prevista: `feature/fase-2-identidad-tenant`.

### 2026-10-21 — Validación central de identidad, fase 2

- Decisión: este servicio confía en la autoridad OIDC de `MS.Cobroup.BackendAdmin`; no guarda contraseñas ni emite tokens.
- Implementación: validación remota de issuer y audiencia `cobroup-api`; política `tenant-user`; endpoint protegido `/api/v1/session/me`.
- Validación: build sin warnings y pruebas 5/5.
- Integración: validada contra la autoridad central ya migrada y provista con una organización y los tres roles base.
- Git: commit de cierre de fase 2 autorizado por el usuario; sin push, merge, tag ni PR.

### 2026-10-21 — Inicio de fase 3

- Rama creada para configuración y rutas.
- El primer contrato se implementa en `MS.Cobroup.BackendAdmin`, autoridad de rutas y asignaciones. Este servicio operativo no incorpora todavía consumidores hasta estabilizar consultas y alcance por ruta.
- Git: sin cambios funcionales de fase 3; push, merge, tag y PR no autorizados.

### 2026-10-21 — Convención de controladores

- `GET /health` y `GET /api/v1/session/me` fueron trasladados a `HealthController` y `SessionController`; `Program.cs` queda limitado a composición y middleware.
- Regla establecida: cada responsabilidad HTTP futura tendrá un controlador propio en API.
- Git: cambios sin commit; push, merge, tag y PR no autorizados.

### 2026-10-21 — Cierre funcional de fase 3

- Contrato: `GET /api/operations/v1/routes` valida el token y consume el directorio administrativo sin compartir base de datos ni duplicar ownership.
- Seguridad: el bearer token se reenvía a BackendAdmin, que vuelve a validar tenant, rol y asignación; el endpoint no acepta tenant/usuario por parámetro.
- Resiliencia: conserva `401/403`; timeout, red o fallo inesperado del propietario se presenta como `503`, nunca como lista vacía engañosa.
- Validación: build Release 0 warnings/errores; Core 6/6 e Infrastructure 5/5; contrato extremo a extremo autenticado respondió 200.
- Git: cambios de fase 3 sin commit; push, merge, tag y PR no autorizados.

### 2026-10-21 — Cierre funcional de fase 4

- Dominio: clientes por tenant/ruta, 1–6 teléfonos, 1–4 ubicaciones, principal única, origen manual/GPS, estado y validación estricta de teléfonos.
- Seguridad: consulta y acceso directo filtrados por tenant y rutas devueltas por la autoridad administrativa; solo `Admin` traslada o cambia estado.
- Persistencia: cupo de clientes activos serializado con advisory locks por ruta; auditoría atómica de creación, edición, traslado y estado con actor/fecha/antes/después.
- Migraciones: `AddCustomerManagement`, `AddCustomerAudit` y `NormalizeCustomerAuditActor` aplicadas; segunda ejecución sin pendientes.
- Pruebas reales: aislamiento cross-tenant aun compartiendo ruta, actualización de contactos sin colisión y dos altas concurrentes con cupo 1 producen exactamente un alta y un rechazo.
- Validación: build Release 0 warnings/errores; Core 18/18, Infrastructure 9/9 e integración OIDC directa/fachada HTTP 200.
- Git: commit de fase 4 autorizado; push, merge, tag y PR permanecen prohibidos.

### 2026-10-21 — Inicio de fase 5: consulta operativa de plantillas

- Rama: `feature/fase-5-plantillas-excepciones`; fase 4 ya comprometida y repositorio limpio al iniciar.
- Contrato: `GET /api/operations/v1/loan-templates` reenvía el bearer a la autoridad administrativa y expone únicamente plantillas publicadas del tenant vigente.
- Resiliencia: conserva `401/403`; red, timeout y errores del propietario se presentan como `503`.
- Validación: build Release sin advertencias; Core 20/20 e Infrastructure 13/13.
- Pendiente de fase: persistir solicitudes excepcionales ligadas a clientes visibles y exponer transición consistente para decisión administrativa; no se originan préstamos en esta fase.
- Git: cambios de fase 5 sin commit; push, merge, tag y PR no autorizados.

### 2026-10-21 — Cierre funcional de fase 5: excepciones de préstamo

- Entidades: agregada `LoanExceptionRequest` y enums `LoanExceptionStatus` (`Pending`, `Approved`, `Rejected`, `Cancelled`) y `LoanExceptionDecision`.
- Reglas: monto > 0, días > 0, porcentaje > 0, motivo obligatorio y no mayor a 500 caracteres; cliente visible, activo y dentro de ruta autorizada.
- Autoridad de decisión: AC-08 implementado y probado; endpoint `POST /api/operations/v1/loan-exceptions/{id}/decision` protegido por política `tenant-admin` y validado estrictamente en Core (`actor.Role is UserRole.Admin`); cualquier intento de aprobación o rechazo por Collector o Supervisor es rechazado con 403 Forbidden sin alterar el estado.
- Idempotencia: decisiones idénticas sobre el mismo estado no alteran datos y retornan exitosamente.
- Cancelación: permitida al autor o a un Admin mientras permanezca en estado `Pending`.
- Persistencia: migración EF `AddLoanExceptionRequests` creada y aplicada en el esquema PostgreSQL `operations`; repositorio `EfLoanExceptionRepository` con índices tenant-safe y join informativo para nombre de cliente.
- Validación: build Release con 0 warnings y 0 errores; suite de pruebas ampliada a Core 42/42 e Infrastructure 16/16 (total 58/58).
- Git: commit de fase 5 autorizado por el usuario: `8cc1e42` (`feat(loan-exceptions): implement loan exception requests and decision enforcement`); push, merge, tag y PR permanecen prohibidos.

### 2026-10-21 — Cierre funcional de fase 6: préstamos y cronograma de cuotas

- Dominio y reglas financieras:
  - Entidades `Loan`, `Installment`, `LoanTermsSnapshot`, y enums `PaymentFrequency` (`Daily`, `Weekly`, `EveryNDays`), `LoanStatus` (`Active`, `PaidOff`, `Cancelled`), `InstallmentStatus` (`Pending`, `PartiallyPaid`, `Paid`).
  - Calculador `LoanScheduleCalculator`: genera cronograma decimal garantizando `sum(installments) == totalAmount` mediante ajuste de centavos residuales en la última cuota (AC-10).
  - Servicio `LoanService`: valida cliente activo y dentro de ruta autorizada (AC-06), verifica cupo de préstamos activos de la ruta frente a cupo efectivo, valida coincidencia con plantilla publicada o excepción de crédito en estado `Approved` (AC-08, AC-09).
- Persistencia y migraciones:
  - Mapeo EF Core con esquema `operations` en `OperationsDbContext`, mapeando `Loan` (con `ComplexProperty` para `LoanTermsSnapshot`) y colección de `Installment` con borrado en cascada.
  - Repositorio `EfLoanRepository`: estrategia de consulta en dos fases (página con `AsNoTracking` y posterior join de nombres de cliente en memoria) para compatibilidad absoluta entre PostgreSQL e InMemory provider.
  - Migración EF Core `20261021210019_AddLoansAndInstallments` generada y aplicada exitosamente en base de datos PostgreSQL real.
- API y contratos:
  - `LoansController` exponiendo `POST /api/operations/v1/loans` (originación), `POST /api/operations/v1/loans/preview-schedule` (simulación reactiva sin persistir), `GET /api/operations/v1/loans` (búsqueda con paginación y filtros), `GET /api/operations/v1/loans/{id}` (detalle con cuotas), `GET /api/operations/v1/loans/by-customer/{customerId}`.
- Pruebas automatizadas:
  - Suite ampliada a 81 pruebas en verde: Core 62/62 (pruebas de cálculo matemático, reglas de cupo, validación de plantillas y excepciones, estados de préstamo) e Infrastructure 19/19 (repositorio EF, mapeos, PostgreSQL real y cliente administrativo).
- Build: Release compilado con 0 warnings y 0 errors (`dotnet build -c Release`).
- Git: commit local `2537925` (`feat(loans): implement loan origination, schedule calculation and active loans quotas`) en rama `feature/fase-6-prestamos-cronograma`; push, merge, tag y PR no autorizados.

### 2026-10-21 — Cierre funcional de fase 7: pagos, evidencias y conciliación

- Dominio y reglas financieras:
  - Entidades `Payment`, `PaymentAllocation`, `PaymentEvidence` en `MS.Cobroup.Backend.Domain.Payments`.
  - Métodos `ApplyPayment(decimal amount)` en `Loan` e `Installment` que aplican el pago en cascada sobre las cuotas pendientes ordenadas cronológicamente (AC-11), amortizando saldo restante y pasando el préstamo a `LoanStatus.PaidOff` exclusivamente cuando el saldo llega a cero.
  - Garantía de idempotencia (AC-12) vía `IdempotencyKey` única por tenant contra cobros duplicados en reintentos o pérdida de red.
  - Validación de medio de pago activo y autorizado para el tenant (AC-13).
  - Soporte de fotografía de evidencia opcional (`PaymentEvidence`) en formatos `image/jpeg`, `image/png`, `image/webp` con retiro auditable restringido exclusivamente al rol `Admin` (AC-14).
  - Motor de conciliación diaria de recaudo por ruta y medio de pago (AC-17).
- Persistencia y base de datos:
  - Mapeo EF Core en `OperationsDbContext` dentro del esquema `operations` para `payments`, `payment_allocations` y `payment_evidences` con índices tenant-safe e índices de fecha/ruta.
  - Repositorio `EfPaymentRepository` implementando `IPaymentRepository`.
  - Migración EF Core `20261021220458_AddPaymentsAndEvidences` aplicada exitosamente en PostgreSQL.
- API y controladores:
  - `PaymentsController` en `MS.Cobroup.Backend.Api/Controllers`: endpoints `POST /api/operations/v1/payments`, `GET /api/operations/v1/payments/by-loan/{loanId}`, `GET /api/operations/v1/payments/daily-reconciliation`, `POST /api/operations/v1/payments/{paymentId}/evidence`, `DELETE /api/operations/v1/payments/{paymentId}/evidence/{evidenceId}`, `GET /api/operations/v1/payments/payment-methods`.
  - Cliente proxy `AdminPaymentMethodDirectoryClient` para resolución de medios de pago autorizados.
- Pruebas automatizadas:
  - Suite ampliada a 94 pruebas en verde: Core 71/71 (cascada, saldo cero, idempotencia, restricción de evidencia admin-only, conciliación por medio) e Infrastructure 23/23 (repositorio EF, aislamiento multi-tenant, persistencia y filtros de fecha/ruta).
- Validación de arquitectura: cumplimiento estricto de FRAMEWORK_DESARROLLO_CSHARP 2.0 (hexagonal, capas desacopladas, invariantes en entidades de dominio, DTOs explícitos y repositorios testeados).
- Build: Release compilado con 0 warnings y 0 errors.
- Git: rama `feature/fase-7-pagos-conciliacion`; push, merge, tag y PR no autorizados.

### 2026-10-21 — Cierre versionado de fase 7 en Git

- Prompt: correcto, hacer commits de fase 7 e iniciar fase 8.
- Resultado: fase 7 validada; commit autorizado: SÍ, commit local `03b2748` (`feat(payments): implement cascade allocation, idempotency and daily reconciliation`).
- Rama de trabajo creada y activa: `feature/fase-8-mapas-analitica`.
- Operaciones no autorizadas: push, merge, tag y PR.

### 2026-10-21 — Implementación de fase 8: mapas, georreferenciación y analítica

- Dominio y reglas geográficas/analíticas:
  - Enumeración `AgendaStatus` (`Overdue`, `DueToday`, `UpToDate`, `CollectedToday`) en `Domain/Agenda`.
  - Calculador puro `HaversineGeoCalculator`: cálculo de distancia geodésica mediante fórmula del semiverseno con radio terrestre medio de 6.371 km; algoritmo voraz de ordenamiento secuencial de paradas por cercanía (Nearest Neighbor) y acumulación métrica de distancias.
  - Puerto `IAgendaRepository` en `Domain/Interfaces/Repositories`.
- Casos de uso y Core:
  - `AgendaContracts`: DTOs inmutables para agenda diaria, paradas georreferenciadas, optimización secuencial y analítica de cartera.
  - `AgendaService`:
    - Cálculo de agenda de cobro diaria con vencimientos, cuotas del día y mora según fecha local (AC-16).
    - Optimización de secuencia de visitas minimizando distancia entre paradas.
    - Extracción de ubicaciones georreferenciadas de clientes de la ruta con semáforo de estados (`#ef4444` mora, `#eab308` vence hoy, `#22c55e` al día/cobrado, `#3b82f6` sin cuota hoy).
    - Motor analítico de cartera en tiempo real: cartera colocada, recaudado histórico, saldo activo, mora vencida, índice PAR% e informe de envejecimiento de deuda (Aging Report) segmentado en 0d, 1-7d, 8-15d, 16-30d y >30d.
- Persistencia e infraestructura:
  - `EfAgendaRepository` implementado sobre `OperationsDbContext` y PostgreSQL, consultando eficientemente `loans`, `installments`, `customers` y `customer_addresses`.
- API y controladores:
  - `AgendaController` en `Api/Controllers`: endpoints `GET /api/operations/v1/agenda/daily`, `POST /api/operations/v1/agenda/optimize-route`, `GET /api/operations/v1/agenda/map-locations`, `GET /api/operations/v1/agenda/portfolio-analytics`.
  - Registro de dependencias en `Program.cs`.
- Pruebas automatizadas:
  - Suite ampliada a 106 pruebas en verde: Core 81/81 (algoritmo Haversine, optimización secuencial, estados de agenda, aging buckets y métricas PAR%) e Infrastructure 25/25 (repositorio EF sobre PostgreSQL).
- Validación de arquitectura: cumplimiento estricto de FRAMEWORK_DESARROLLO_CSHARP 2.0 (hexagonal, dominio puro sin dependencias externas, DTOs inmutables, controladores delgados).
- Build: Release compilado con 0 warnings y 0 errors (`dotnet build -c Release`).
- Git: rama `feature/fase-8-mapas-analitica`; push, merge, tag y PR no autorizados.

### 2026-10-21 — Cierre versionado de fase 8 en Git

- Prompt: correcto, hacer commits de fase 7 e iniciar fase 8 / proceder con la fase final.
- Resultado: fase 8 validada; commit autorizado: SÍ, commit local `cf65b00` (`feat(agenda): implement daily agenda, haversine geo sequence and portfolio analytics`).
- Rama de trabajo creada y activa: `feature/fase-9-endurecimiento-despliegue`.
- Operaciones no autorizadas: push, merge, tag y PR.

### 2026-10-21 — Implementación de fase 9: endurecimiento y despliegue K3s/Kubernetes

- Observabilidad y Health Checks:
  - `OperationsDatabaseHealthCheck` creado en `MS.Cobroup.Backend.Api/Health/` implementando `IHealthCheck` para verificar conectividad activa con PostgreSQL (`SELECT 1`).
  - Registro en `Program.cs` de probes: `GET /health/live` (liveness) y `GET /health/ready` (readiness con comprobación de BD).
- Seguridad HTTP e Ingress:
  - `UseForwardedHeaders` configurado para soporte de cabeceras de proxy (`X-Forwarded-For`, `X-Forwarded-Proto`) de Ingress Traefik.
  - Middleware de cabeceras de seguridad: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-XSS-Protection: 0`.
  - Correlation ID middleware (`X-Correlation-ID`) para trazabilidad transversal.
- Contenedorización OCI / Docker:
  - `Dockerfile` multi-stage: build con .NET SDK 10, ejecución con runtime ASP.NET Core 10, usuario no root `1000:1000`, puerto 8080.
  - `.dockerignore` configurado.
- Orquestación K3s y CI/CD:
  - Manifiestos K3s (`k8s/base/backend-operations.yaml`, `namespace.yaml`, `ingress.yaml`, `configmap.yaml`, `secrets-template.yaml`).
  - Pipeline `.github/workflows/ci-cd.yml`: ejecución de tests, build y push a GHCR (`ghcr.io/clepineuxp/ms-cobroup-backend`), y despliegue automatizado por SSH a K3s para ambientes `cobroup-dev`, `cobroup-qa`, `cobroup-prod`.
- Pruebas automatizadas y build:
  - 106 pruebas en verde: Core 81/81, Infrastructure 25/25.
  - Compilación Release con 0 warnings y 0 errores.
- Git: rama `feature/fase-9-endurecimiento-despliegue`; push, merge, tag y PR no autorizados.

### 2026-10-22 — Rebaselining auditor de identidad JWT

- Prompt: formalizar en toda la planeación la decisión de usar exclusivamente JWT y retirar el mecanismo anterior, actuando solo como auditor.
- Decisión: BackendAdmin es el único emisor; este backend solo valida access JWT asimétricos mediante JWKS. La fase 2 queda reabierta.
- Documentación: maestro 1.7.0, decisión JWT, README, contexto, contrato, ADR, roadmap, avance y adenda HTML alineados.
- Auditoría: el contrato queda aprobado; la implementación actual sigue NO APROBADA y debe demostrar validación estricta, rotación de claves, tenant/rol/recurso y pruebas negativas.
- Verificación: búsqueda transversal sin referencias normativas activas al mecanismo retirado; los informes y entradas históricas se conservaron como evidencia.
- Git: no se modificó código en esta actividad y no se creó commit, push, merge, tag ni PR.

### 2026-10-22 — Implementación de validación JWKS asimétrica, evidencias streaming y remediación de hallazgos (Bloques E, F, G)

- Validación JWT asimétrica (B-02, B-03): implementado `JwksValidationService` que consume `GET /api/v1/auth/.well-known/jwks.json` de BackendAdmin. Validación estricta de firma RS256, audience `cobroup-api`, issuer HTTPS y claims obligatorios (`organization_id`, `role`, `typ=access`). Este backend operativo nunca recibe la clave privada.
- Aislamiento y streaming de evidencias (B-08, B-09, I-05): eliminado endpoint legado `POST {paymentId}/evidence` y DTO `AttachPaymentEvidenceRequest`. Solo se permite creación mediante `POST {paymentId}/evidence/upload` con streaming en bloques, límite de 10 MB aplicado durante lectura, hash SHA-256 incremental y magic bytes. En caso de fallo en BD, se compensa eliminando el archivo con `DeleteEvidenceAsync`.
- Autorización de descarga por ruta (B-09): `DownloadEvidence` valida que el actor tenga acceso explícito a la ruta del cobro mediante `routeDirectory.ListAccessibleAsync` antes de servir el archivo.
- Aprobación de excepciones de crédito (B-10): en `LoanService`, se exige coincidencia exacta del plazo aprobado (`scheduleDurationDays != exception.ProposedDays`), rechazando plazos menores y mayores.
- Zona horaria IANA e intervalos semiabiertos (I-06): conciliación diaria y agenda ajustadas a intervalos semiabiertos `[startUtc, endUtc)`.
- Propagación de Correlation ID (I-12): middleware asigna/reutiliza `X-Correlation-ID` y `CorrelationIdDelegatingHandler` lo propaga en clientes HTTP hacia BackendAdmin.
- Validación: 106/106 pruebas unitarias y de integración pasando en verde (Core 81/81, Infrastructure 25/25), build Release 0 warnings/errores, 0 cambios pendientes de modelo EF Core.
- Git: cambios preservados en el working tree sin commit, push, merge, tag ni PR.

### 2026-10-22 — Remediación Bloqueante B-09 (Auditoría v5): Almacenamiento durable en PVC y desacoplamiento hexagonal hacia AWS S3

- **Solicitud**: Remediar el bloqueante B-09 documentado en la auditoría v5 (`cobroup.md/informe/auditoria-backend-csharp-2026-10-22-v5.html`), garantizando almacenamiento durable no efímero de evidencias mediante PVC de Kubernetes, desacoplado bajo arquitectura hexagonal con previsión de evolución futura a AWS S3.
- **Decisión de almacenamiento PVC→S3**:
  - Proveedor actual: PersistentVolumeClaim (PVC) de Kubernetes montado en `/var/cobroup/evidences`. En producción (2 réplicas) requiere `ReadWriteMany` (10Gi).
  - Proveedor futuro previsto: AWS S3 mediante inyección de dependencias y configuración (`Storage:Provider=S3`), sin instalar SDK de AWS ni modificar Core, Domain ni contratos HTTP.
  - La base de datos almacena exclusivamente claves lógicas relativas normalizadas (`StorageKey`), nunca rutas absolutas del pod ni del PVC.
- **Implementación técnica**:
  - `MS.Cobroup.Backend.Domain/Interfaces/Services/IFileStorageService.cs`: Puerto agnóstico con `StoredFileResult(StorageKey, ContentType, ByteSize, FileHashSha256)` y métodos asíncronos `SaveEvidenceAsync`, `ReadEvidenceAsync` y `DeleteEvidenceAsync` con `CancellationToken`.
  - `MS.Cobroup.Backend.Infrastructure/Storage/FileSystemStorageOptions.cs`: Opciones tipadas vinculadas a la sección `Storage`.
  - `MS.Cobroup.Backend.Infrastructure/Storage/FileSystemObjectStorage.cs`: Adaptador durable que rechaza path traversal (`..`, `:`, rutas absolutas), efectúa streaming en bloques con buffers de 64 KB, valida magic bytes (JPEG/PNG/WebP), restringe tamaño a 10 MB durante la lectura, computa hash SHA-256 incremental, escribe en `.tmp/` dentro del mismo volumen y realiza movimiento atómico al destino final, con limpieza garantizada en `finally`. En lecturas usa `FileShare.ReadWrite | FileShare.Delete` para interoperabilidad concurrente entre réplicas.
  - `MS.Cobroup.Backend.Infrastructure/Storage/LocalFileStorageService.cs`: Mantenido como fachada delegada para preservar compatibilidad.
  - `MS.Cobroup.Backend.Api/Program.cs`: Configuración tipada de almacenamiento. Si `Storage:Provider=S3` lanza `NotSupportedException` descriptiva; fail-fast ante proveedores no reconocidos fuera de pruebas.
- **Archivos modificados/creados**:
  - Modificados: `MS.Cobroup.Backend.Domain/Interfaces/Services/IFileStorageService.cs`, `MS.Cobroup.Backend.Infrastructure/Storage/LocalFileStorageService.cs`, `MS.Cobroup.Backend.Api/Program.cs`, `README.md`, `docs/context/INDEX.md`, `AGENTS.md`.
  - Creados: `MS.Cobroup.Backend.Infrastructure/Storage/FileSystemStorageOptions.cs`, `MS.Cobroup.Backend.Infrastructure/Storage/FileSystemObjectStorage.cs`, `docs/decisions/0004-durable-file-storage-pvc.md`, `tests/MS.Cobroup.Backend.Infrastructure.Tests/Storage/FileSystemObjectStorageTests.cs`.
- **Resultados de pruebas**:
  - Suite `MS.Cobroup.Backend.Infrastructure.Tests/Storage/FileSystemObjectStorageTests.cs`: 8 pruebas unitarias e integración en verde (normalización y rechazo traversal, límites de streaming, validación magic bytes, SHA-256, colisiones, atomic move, cancelación y persistencia/lectura compartida entre réplicas).
  - Suite completa `MS.Cobroup.Backend.slnx`: 157/157 pruebas pasando (Core 81/81, Infrastructure 76/76).
  - Compilación Release: 0 warnings, 0 errors (`dotnet build -c Release --no-incremental`).
  - EF Core: 0 cambios pendientes de modelo (`dotnet ef migrations has-pending-model-changes`).
- **Git**: Cambios preservados en working tree sobre rama `feature/solucion-hallazgos-iniciales`; sin commit, push, merge, tag ni PR.

### 2026-10-22 — Decisión arquitectónica: Alineación con proveedor RSA efímero de BackendAdmin y validación JWKS

- **Decisión obligatoria aplicada**:
  > COBROUP continuará usando JWT firmados. En la fase actual de desarrollo y validación no se configurará una clave privada externa en formato PEM. BackendAdmin utilizará un proveedor RSA efímero generado en memoria, habilitado de forma explícita y restringido a entornos no productivos. La clave y el `kid` cambian al reiniciar la instancia; en consecuencia, los JWT emitidos antes del reinicio pueden dejar de ser válidos. Este modo no se considera apto para producción, alta disponibilidad ni múltiples réplicas emisoras. La custodia de una clave persistente, su rotación, solapamiento de claves y recuperación operativa mediante Kubernetes Secret, PFX, PEM, AWS KMS u otro proveedor aprobado quedan registrados como backlog obligatorio previo a producción.
- **Alineación operativa de Backend**:
  - `MS.Cobroup.Backend` valida tokens exclusivamente mediante JWKS asimétrico (`GET /api/v1/auth/.well-known/jwks.json`) expuesto por `BackendAdmin`. No posee ni recibe claves privadas ni secretos simétricos.
  - Se reconoce la limitación operativa: al reiniciar BackendAdmin en modo efímero, se genera un nuevo `kid` y nuevo par de claves; el backend operativo actualiza JWKS según su política, pero los tokens emitidos antes del reinicio pueden ser rechazados inmediatamente.
- **Git**: Cambios preservados en working tree sobre rama `feature/solucion-hallazgos-iniciales`; sin commit, push, merge, tag ni PR.

### 2026-10-22 / 2026-10-23 — Migración integral a JWT simétrico HS256 por ambiente (Backend)

- **Decisión aplicada**: `cobroup.md/DECISION_AUTENTICACION_JWT.md`, `BACKLOG_SEGURIDAD_FIRMA_SIMETRICA.md` y `MS.Cobroup.Backend/docs/decisions/0005-symmetric-hs256-validation.md`.
- **Componentes retirados**:
  - Eliminados: `JwksHealthCheck.cs`, `JwksPreloadHostedService.cs`, `JwksValidationService.cs` y `JwksSingletonIdentityTests.cs`.
  - Eliminadas opciones y clientes HTTP dedicados a JWKS en `Program.cs`.
- **Implementación técnica**:
  - `MS.Cobroup.Backend.Api/Security/ISymmetricTokenValidationService.cs` y `SymmetricTokenValidationService.cs`:
    - Validador simétrico local singleton con fail-fast estricto en startup: exige `Jwt:SigningKeyBase64` presente, no vacío, sin placeholders, Base64 válido y >= 32 bytes (256 bits).
    - Allowlist exacta: `SecurityAlgorithms.HmacSha256` (`alg=HS256`).
    - Soporte de `Jwt:ActiveKeyId` y `Jwt:PreviousValidationKeys` para rotación con solapamiento seguro.
  - `MS.Cobroup.Backend.Api/Security/AccessJwtValidator.cs`:
    - Validación de claims obligatorios (`sub`, `typ=access`, `organization_id`, `role`, `jti`, `iat`, `nbf`, `exp`) y audiencia `cobroup-api`.
  - `MS.Cobroup.Backend.Api/Program.cs`:
    - Registro de `ISymmetricTokenValidationService` e integración con `AddJwtBearer`.
  - **Frontera de no emisión estrictamente auditada**:
    - Backend no contiene servicios, casos de uso, controladores ni endpoints generadores de JWT.
- **Pruebas automatizadas**:
  - Creada suite `SymmetricValidationServiceTests.cs` (12 pruebas): fail-fast por clave ausente, vacía, placeholder, base64 inválido, 31 bytes; aceptación de 32 bytes; validación de clave activa y anterior; rechazo por retiro de anterior; consistencia multirréplica; rechazo de `none`, RS256, ES256, HS384, HS512; y reflexión que audita que Backend NO contiene tipos ni endpoints de emisión de tokens.
  - Actualizadas `AccessJwtValidationTests.cs` y `JwtBearerPipelineHttpTests.cs`.
  - Suite completa: 174/174 pruebas pasando en Release (Core 81/81, Infrastructure 93/93).
  - Compilación Release: 0 warnings, 0 errors.
  - EF Core: 0 cambios pendientes de modelo (`dotnet ef migrations has-pending-model-changes`).
- **Git**: Cambios preservados en working tree sobre rama `feature/solucion-hallazgos-iniciales`; sin commit, push, merge, tag ni PR.

### 2026-10-23 — Remediación v9: Contrato único de rotación HS256 y cierre técnico B-09/PVC durable

- **Auditoría atendida**: `cobroup.md/informe/auditoria-backend-csharp-2026-10-23-v9.html`.
- **Rotación HS256 y fail-fast en validador**:
  - Implementado contrato estricto: `Jwt__ActiveKeyId`, `Jwt__SigningKeyBase64`, `Jwt__PreviousValidationKeys__0__KeyId`, `Jwt__PreviousValidationKeys__0__SigningKeyBase64`.
  - Fail-fast estricto en `SymmetricTokenValidationService`: validación de regex `^[A-Za-z0-9._-]{1,64}$`, rechazo de defaults en runtime (`k1`), verificación estricta de pares en previous keys (ambos presentes o ambos ausentes), rechazo de hijos incompletos (sin `continue` silencioso), verificación de propiedades desconocidas y rechazo de coincidencia de material o `kid` entre activo y previo.
- **Almacenamiento durable de archivos (Cierre B-09)**:
  - `IFileStorageService`: refactorizado a puerto agnóstico general para archivos binarios (`PutAsync`, `OpenReadAsync`, `DeleteAsync`, `ExistsAsync`, `GetMetadataAsync`) con fachada temporal retrocompatible para payment evidences.
  - Clave lógica canónica: `tenants/{tenantId}/{category}/{aggregateId}/{sha256}.{ext}` con allowlist de servidor (`evidences`, `payments`, `documents`, `attachments`), y compatibilidad explícita con claves legadas `{tenantId}/{paymentId}/{sha256}.{ext}`.
  - `ImageStructuralValidator`: validación estructural completa de imágenes JPEG (marcadores, SOF, EOI), PNG (firma, chunks IHDR/IEND), WebP (VP8/VP8L/VP8X) y límite estricto de resolución <= 8192px contra decompression bombs.
  - `FileSystemObjectStorage`:
    - Normalización canónica y verificación estricta de límites de raíz (`rootWithSep`).
    - Detección y rechazo de symlinks / reparse points en la raíz y en cada componente intermedio de la ruta.
    - Publicación atómica mediante archivo temporal en el mismo volumen (`.tmp/`) con `File.Move(..., overwrite: false)`.
    - Idempotencia segura: si el destino existe con idéntico tamaño y SHA-256 se considera éxito idempotente sin sobrescritura destructiva; si el contenido difiere, arroja `InvalidOperationException` (violación de integridad).
    - Reconciliación segura de temporales huérfanos (`ReconcileOrphanTempsAsync`).
- **Pruebas y verificación**:
  - Suite completa: 189/189 pruebas pasando (Core 81/81, Infrastructure 108/108).
  - Cero cambios pendientes de modelo en EF Core (`has-pending-model-changes`).
- **Git**: Cambios preservados en working tree sobre rama `feature/solucion-hallazgos-iniciales`; sin commit, push, merge, tag ni PR.

### 2026-10-23 — Protección de autonomía de pruebas del Backend operativo

- **Responsabilidad confirmada**: El Backend operativo ya prueba autónomamente su validador JWT (`SymmetricTokenValidationService`), incluyendo clave activa, ventana de solapamiento con clave anterior, rechazo de clave retirada y `kid` desconocido, sin referenciar proyectos de BackendAdmin.
- **Protección añadida**: Prueba arquitectónica que inspecciona todos los `.csproj` y falla si cualquier `ProjectReference` resuelve fuera de la raíz de `MS.Cobroup.Backend`.
- **Verificación**: 206/206 pruebas aprobadas en Release (Core 81/81, Infrastructure 125/125).
- **Git**: Cambios preservados en working tree; sin commit, push, merge, tag ni PR.
### 2026-10-23 — Remediación obligatoria de hallazgo B-21: Fail-fast de configuración y validación de rotación

- **Auditoría atendida**: `cobroup.md/informe/auditoria-backend-csharp-2026-10-23-v10.html` (Hallazgo B-21).
- **Alineación de configuración y tiempo de diseño**:
  - Eliminado `"ActiveKeyId": "k1"` de `MS.Cobroup.Backend.Api/appsettings.json`. Fail-fast inmediato en arranque si `Jwt:ActiveKeyId` falta o no cumple el patrón regex `^[A-Za-z0-9._-]{1,64}$`.
  - Añadido `OperationsDbContextFactory` para soporte estricto de `IDesignTimeDbContextFactory<OperationsDbContext>` en herramientas CLI (`dotnet ef`).
- **Verificación de validación y solapamiento de rotación**:
  - `SymmetricTokenValidationService`: Valida access JWT emitidos por `BackendAdmin` con clave activa y claves en slot anterior.
  - Comprobación de interoperabilidad y solapamiento en pruebas: durante la rotación, el backend operativo procesa concurrentemente tokens firmados con la clave activa y la clave anterior sin interrupción.
- **Pruebas y verificación**:
  - Suite completa: 189/189 pruebas pasando (Core 81/81, Infrastructure 108/108).
  - Cero cambios pendientes de modelo en EF Core (`has-pending-model-changes`).
- **Git**: Cambios preservados en working tree sobre rama `feature/solucion-hallazgos-iniciales`; sin commit, push, merge, tag ni PR.


### 2026-10-23 — Plantillas por rangos y rutas; cuotas mensuales

- Solicitud: explicar y modificar plantillas para monto/porcentaje mínimo y máximo, y listar las de la ruta al prestar. Ampliación: agregar cobros mensuales.
- Regla: rangos continuos inclusivos, asignación explícita a rutas del tenant y validación de alcance/versión en servidor. Mensual usa meses calendario anclados al día original y conserva el cargo total pactado.
- Datos: mínimo monetario y rutas versionados, migración aditiva AddLoanTemplateRangesAndRoutes y SQL idempotente preparado. Plantillas históricas sin asignación requieren edición; no se cambian préstamos existentes.
- Trabajo previo de aprovisionamiento conservado. Sin commit, push ni despliegue solicitados para esta intervención. Migración no aplicada ni pruebas sobre datos reales.
- Validación final: Core administrativo 69/69 y operativo 97/97; frontend administrativo 49/49 y operativo 53/53. Pruebas dirigidas de modelo EF 6/6 y directorio HTTP 4/4. EF sin cambios de modelo pendientes y SQL idempotente generado. No se ejecutó migración ni integración PostgreSQL desplegada.

### 2026-10-23 — Commits autorizados de plantillas, mensual y mapa

- Solicitud expresa: realizar los commits e incluir la actualización del mapa.
- Alcance: rangos de crédito, asignación a rutas, cuotas mensuales, migración y documentación; ambas interfaces incluyen la compatibilidad de importación de Leaflet del mapa.
- Validación registrada: 278 pruebas aprobadas y compilaciones correctas; sin nuevos cambios funcionales posteriores.
- Commit autorizado: SÍ. Sin push ni despliegue; migración administrativa pendiente de aplicación.


### 2026-10-24 — Balance y movimientos financieros por ruta

- Solicitud: saldo inicial por ruta, afectación automática por préstamos/cobros y secciones de ingresos/inversiones y gastos en ambas aplicaciones.
- Modelo: el balance se calcula sin límites como `saldo inicial - capital prestado + cobros + ingresos/inversiones - gastos`; usa los préstamos y pagos persistidos, por lo que no duplica efectos ni exige backfill de movimientos históricos.
- Implementación: agregado `RouteFinancialEntry` con tipo Income/Investment/Expense, concepto, descripción, monto positivo, ruta/tenant, actor creador y actor de última edición. API bajo `/api/operations/v1/route-finances` para balance, listado, alta y modificación.
- Autorización: alcance de ruta se resuelve siempre mediante el directorio administrativo. Collector y Supervisor solo pueden modificar durante el mismo día calendario de creación en la zona horaria configurada para el tenant; Admin puede modificar cualquier fecha. La fecha se asigna en servidor.
- Persistencia: migración `AddRouteFinancialEntries` creada; modelo EF sin cambios pendientes. No se aplicó la migración ni se modificaron datos reales.
- Verificación: build Release sin warnings/errores; Core 100/100. La suite Infrastructure conservó 118 aprobadas y 7 fallos preexistentes por esquema PostgreSQL local desactualizado (`customers.Identification`) y datos de prueba en conflicto, no relacionados con esta feature.
- Git: cambios locales sin commit, push, merge, tag, PR ni despliegue.
=======

### 2026-10-24 — Identificación obligatoria de clientes e intercambio visual de etiquetas

- Solicitud expresa: invertir Alias/Nombre completo solo en interfaz y agregar identificación obligatoria, con 0 solo para clientes anteriores. Maestro 1.15.0, frameworks frontend 1.0 / C# 2.0 y JWT vigente.
- Dominio Customer, comandos, requests/respuestas, proyección y auditoría incorporan Identification requerida (string, 1–64 caracteres tras trim). Crea sin default, rechaza vacío y 0 nuevo; actualización permite retener 0 solo mientras el registro existente ya tiene 0. Conserva ceros iniciales, no introduce unicidad ni restricciones numéricas no solicitadas. Búsqueda incluye identificación y mantiene los filtros de tenant/ruta.
- EF: columna operations.customers.Identification varchar(64) NOT NULL sin default persistente. Migración 20261024055522_AddCustomerIdentification añade columna anulable, rellena solo filas existentes con 0 y luego aplica NOT NULL. Snapshot/Designer y SQL idempotente incluidos. Factory de diseño alineada con el host: historial EF en operations.__EFMigrationsHistory; evita interpretar como pendientes migraciones ya aplicadas en ese esquema.
- Aplicación: comprobación previa exigió base exacta dev_cobroup_operations y únicamente esta migración pendiente. Aplicada correctamente: 7 clientes anteriores con 0, 0 identificaciones vacías/nulas, default de columna ausente y 0 migraciones pendientes. Sin cambios a nombres/alias/préstamos de esos clientes. No se aplicó en QA/Prod ni se desplegó API.
- Verificación: Core 105/105 e Infrastructure 127/127 completos; integración PostgreSQL valida la migración sobre tabla temporal y rollback (0 solo a anteriores, nueva fila sin ID rechazada, 001234 conservado). Tests existentes con DB inicialmente fallaron por columna ausente y pasan tras aplicar la migración. Modelo EF sin cambios pendientes; SQL idempotente regenerado con historial correcto. .NET SDK 10.0.402, net10.0, C#14 y EF 10.0.11; build sin errores.
- Despliegue coordinado requerido: las versiones anteriores de API/formularios no envían identificación. Desplegar el backend operativo actualizado y ambos frontends juntos; la migración de Dev ya está aplicada. El servidor valida también las solicitudes directas y las reenviadas por BackendAdmin.
- Git: sin commit, push, merge, PR ni despliegue solicitados para este alcance. Auditoría independiente no realizada. Decisión canónica: ../cobroup.md/DECISION_IDENTIFICACION_CLIENTE.md.

### 2026-10-24 — Commits autorizados de identificación de clientes

- Solicitud expresa del usuario: hacer commits del alcance de identificación obligatoria, intercambio visual de etiquetas, migración, pruebas y documentación asociadas.
- Se conservan las verificaciones registradas y la migración aplicada en Dev a 7 clientes. Sin nuevos cambios funcionales. Commit autorizado: SÍ. Sin push, merge, tag, PR ni despliegue.

### 2026-10-24 — Verificación de rama y destino de publicación

- El usuario pidió validar que el trabajo y su publicación correspondan a la rama feature, al observar cambios enviados a dev.
- Rama activa verificada: feature/customer-identification. Eliminado el seguimiento incorrecto de origin/dev. Configuración local: push.default=current, push.autoSetupRemote=true y branch.autoSetupMerge=false; futuros pushes por defecto usarán el mismo nombre de rama y nuevas features no heredarán origin/dev automáticamente.
- La rama remota feature/customer-identification no existía al verificar; no se hizo push. Los commits permanecen locales. En documentación se creó esta rama desde la fundación local existente, conservando los cinco commits previos sin reset ni merge.


### 2026-10-26 — Corrección y validación del último cambio de Gemini

- Solicitud: investigar por qué no ejecutan los backend y conservar operativa la funcionalidad nueva.
- Hallazgos: BackendAdmin no compilaba por import faltante de UserRole; cambios EF sin migraciones; endpoint administrativo de auditoría ausente y nombres de filtros distintos entre frontend y API operativa. Instancia Debug operativa bajo Visual Studio bloquea sus DLL.
- Correcciones: import de Authorization; migración AddDayClosingAndFinancialAudit y SQL idempotente; auditoría limitada a rutas autorizadas y propagación de fallos del directorio; cierre y su auditoría se guardan en un único SaveChanges.
- Validación: build Release sin errores; Core 114/114 e Infrastructure aislada 119/119. Pruebas nuevas de bloqueo del día cerrado, reapertura con permisos y aislamiento por ruta. Modelo EF sin diferencias pendientes. Arranque HTTP y /health/live 200; auditoría sin token 401.
- Limitación: PostgreSQL dev_cobroup_operations en la conexión configurada agota el tiempo de espera incluso al consultar historial EF; migraciones no aplicadas y flujo autenticado contra BD real pendiente. Conexión TCP abre, pero sesión PostgreSQL no completa. No se alteraron datos reales.
- Git: cambios existentes conservados en feature/unique-transaction. Sin commit, push ni despliegue.

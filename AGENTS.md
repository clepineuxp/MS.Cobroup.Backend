# AGENTS — MS.Cobroup.Backend

## Identidad y restricciones del proyecto

- Producto: COBROUP
- Rol: microservicio operativo; autoridad futura de Customer, Loan, Installment, Payment y PaymentEvidence.
- Documento maestro: `../cobroup.md/COBROUP.md` 1.7.0 y `../cobroup.md/DECISION_AUTENTICACION_JWT.md`.
- Estándar: `../cobroup.md/FRAMEWORK_DESARROLLO_CSHARP.md` 2.0.
- Rama activa observada: `feature/solucion-hallazgos-iniciales`.
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
- Este servicio no autentica contraseñas, no emite tokens y no recibe la clave privada. Valida firma asimétrica, `kid`, issuer, audience, tipo, tiempos y claims obligatorios mediante claves públicas rotables.
- Se prohíben secretos HMAC compartidos, claves/defaults de respaldo, `ValidateIssuer=false`, `ValidateAudience=false` y aceptación de algoritmos no autorizados.
- El access token vive solo en memoria del frontend. El refresh JWT viaja únicamente en cookie HttpOnly/Secure/SameSite y nunca en Web Storage, cuerpo persistente o base de datos en claro.
- Toda operación mantiene autorización tenant/rol/recurso aunque el JWT sea válido.
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



# AGENTS — MS.Cobroup.Backend

## Identidad y restricciones del proyecto

- Producto: COBROUP
- Rol: microservicio operativo; autoridad futura de Customer, Loan, Installment, Payment y PaymentEvidence.
- Documento maestro: `../cobroup.md/COBROUP.md` 1.5.0.
- Estándar: `../cobroup.md/FRAMEWORK_DESARROLLO_CSHARP.md` 2.0.
- Rama requerida y activa: `feature/fase-8-mapas-analitica`.
- Prohibidos push, merge, tag y PR sin autorización.

## Estado actual

- Fase: 8
- Resultado: COMPLETADA_FUNCIONALMENTE
- Plataforma: .NET SDK 10.0.401, `net10.0`, C# 14.
- Auditoría independiente: no realizada.
- Línea base: 9 fases totales; 8 completas funcionalmente, 1 posterior pendiente. Informe: `../cobroup.md/informe/avance.md`.
- Corte actual: Fase 7 cerrada en Git (`03b2748`); Fase 8 completada: cálculo de agenda diaria y vencimientos exactos (AC-16), optimización secuencial de ruta por proximidad geográfica mediante algoritmo de Haversine en el dominio, ubicaciones georreferenciadas con semáforo de colores, y motor de analítica de cartera en tiempo real con Aging Report (0d, 1-7d, 8-15d, 16-30d, >30d) e índice PAR%; validación estricta de FRAMEWORK_DESARROLLO_CSHARP 2.0; build Release 0 warnings/errores; Core 81/81 e Infrastructure 25/25 (106 pruebas en verde).

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


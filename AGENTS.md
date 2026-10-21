# AGENTS — MS.Cobroup.Backend

## Identidad y restricciones del proyecto

- Producto: COBROUP
- Rol: microservicio operativo; autoridad futura de Customer, Loan, Installment, Payment y PaymentEvidence.
- Documento maestro: `../cobroup.md/COBROUP.md` 1.0.5.
- Estándar: `../cobroup.md/FRAMEWORK_DESARROLLO_CSHARP.md` 2.0.
- Rama requerida y activa: `feature/fase-3-configuracion-rutas`.
- Prohibidos push, merge, tag y PR sin autorización.

## Estado actual

- Fase: 3
- Resultado: COMPLETADA
- Plataforma: .NET SDK 10.0.401, `net10.0`, C# 14.
- Auditoría independiente: no realizada.
- Línea base: 9 fases totales; 3 completas y 6 pendientes. Informe: `../cobroup.md/informe/avance.md`.

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

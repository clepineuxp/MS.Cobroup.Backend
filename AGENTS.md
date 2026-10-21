# AGENTS — MS.Cobroup.Backend

## Identidad y restricciones del proyecto

- Producto: COBROUP
- Rol: microservicio operativo; autoridad futura de Customer, Loan, Installment, Payment y PaymentEvidence.
- Documento maestro: `../cobroup.md/COBROUP.md` 1.0.1.
- Estándar: `../cobroup.md/FRAMEWORK_DESARROLLO_CSHARP.md` 2.0.
- Rama requerida y activa: `feature/fase-1-inicializacion`.
- Prohibidos commits, push y cambios fuera de fase 1 sin autorización.

## Estado actual

- Fase: 1
- Resultado: COMPLETADA
- Plataforma: .NET SDK 10.0.401, `net10.0`, C# 14.
- Auditoría independiente: no realizada.

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

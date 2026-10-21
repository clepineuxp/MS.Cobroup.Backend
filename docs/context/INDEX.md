# Contexto — MS.Cobroup.Backend

## Propósito

Autoridad de escritura operativa para clientes, préstamos, cuotas, pagos y evidencias, conforme a `../cobroup.md/COBROUP.md` 1.0.1. En fase 1 solo se crea la base ejecutable.

## Límites

- Propio: contratos y futuros casos de uso operativos.
- No propio: configuración de organización, rutas, asignaciones, plantillas, medios, etiquetas y aprobación de excepciones.
- Prohibido: leer o escribir directamente almacenamiento propiedad de `MS.Cobroup.BackendAdmin`.

## Seguridad inicial

`AuthenticatedActor` modela `user_id`, `tenant_id`, roles `ADMIN`/`SUPERVISOR`/`COLLECTOR` y rutas autorizadas. El cliente nunca será autoridad del tenant. `AccessScopeAuthorizer` deniega tenant ajeno y rutas no asignadas; no sustituye la autenticación futura.

## Estado técnico

- .NET 10 / C# 14, solución `.slnx`, sin `src/`.
- Health check: `/health`.
- Persistencia, proveedor de identidad y secretos: pendientes de ADR.
- Contratos: [fronteras v1](../contracts/platform-boundaries-v1.md).
- Decisiones: [fundación fase 1](../decisions/0001-phase-1-foundation.md).
- Roadmap propuesto: [fases siguientes](../roadmap/phase-proposal.md).

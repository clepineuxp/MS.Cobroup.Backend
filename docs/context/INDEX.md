# Contexto — MS.Cobroup.Backend

## Propósito

Autoridad de escritura operativa para clientes, préstamos, cuotas, pagos y evidencias, conforme a `../../../cobroup.md/COBROUP.md` 1.7.0. Valida access tokens JWT de la autoridad administrativa y publica rutas autorizadas; los agregados operativos comienzan en la fase de clientes.

## Límites

- Propio: contratos y futuros casos de uso operativos.
- No propio: configuración de organización, rutas, asignaciones, plantillas, medios, etiquetas y aprobación de excepciones.
- Prohibido: leer o escribir directamente almacenamiento propiedad de `MS.Cobroup.BackendAdmin`.

## Seguridad inicial

`AuthenticatedActor` modela `user_id`, `organization_id`, roles `ADMIN`/`SUPERVISOR`/`COLLECTOR` y rutas autorizadas. El cliente nunca es autoridad del tenant. `AccessScopeAuthorizer` deniega tenant ajeno y rutas no asignadas; se aplica después de validar estrictamente firma y claims del JWT.

## Estado técnico

- .NET 10 / C# 14, solución `.slnx`, sin `src/`.
- Health check: `/health`.
- Lectura contractual de rutas autorizadas implementada. La validación de identidad JWT está reabierta y pendiente de conformidad con la decisión del 2026-10-22.
- Contratos: [fronteras v1](../contracts/platform-boundaries-v1.md).
- Decisiones: [fundación fase 1](../decisions/0001-phase-1-foundation.md), [confianza JWT](../decisions/0002-trust-central-identity.md) y [almacenamiento durable PVC](../decisions/0004-durable-file-storage-pvc.md).
- Roadmap propuesto: [fases siguientes](../roadmap/phase-proposal.md).

## Plantillas por rangos y cobro mensual — 2026-10-23

Decisión vigente: [rangos, rutas y mensual](../../../cobroup.md/DECISION_PLANTILLAS_RANGOS_RUTAS.md). Monto/porcentaje mínimo y máximo inclusivos; plantillas publicadas asignadas explícitamente a la ruta del cliente, con validación del servidor. Mensual conserva el día de inicio por meses calendario. Migración administrativa preparada y pendiente de aplicación; no modifica condiciones de préstamos existentes.


## Identificación del cliente — 2026-10-24

Decisión vigente: [identificación obligatoria y etiquetas](../../../cobroup.md/DECISION_IDENTIFICACION_CLIENTE.md). Identification viaja en alta, edición y lectura; sin default para nuevos. Migración operativa aplicada en Dev: 7 clientes anteriores con 0, sin pendientes. El modal invierte solo etiquetas Alias/Nombre completo, sin mover datos. Evidencias y despliegue coordinado en AGENTS.md.

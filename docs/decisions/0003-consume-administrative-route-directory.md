# ADR-0003 — Consumo del directorio administrativo de rutas

## Estado

Aceptada para cierre de fase 3 (2026-10-21).

## Decisión

`MS.Cobroup.Backend` no replica ni consulta la base administrativa. `RoutesController` valida el token y delega en un puerto Core; el adaptador Infrastructure llama `GET /api/v1/route-access` de BackendAdmin reenviando el bearer token. BackendAdmin vuelve a validar identidad, tenant, rol y asignación antes de responder.

## Fallos

`401` y `403` se conservan. Timeout, red caída o respuestas inesperadas del propietario se convierten en `503`; no se devuelve una lista vacía que pueda confundirse con ausencia real de asignaciones.

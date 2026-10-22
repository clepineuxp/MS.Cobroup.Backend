# ADR-002 — Confianza en la autoridad central

Estado: SUPERSEDIDA parcialmente el 2026-10-22 por `DECISION_AUTENTICACION_JWT.md` y BackendAdmin ADR-0007 en todo lo relativo a algoritmo y distribución de claves. Se conserva como trazabilidad histórica; siguen vigentes autoridad central, no emisión y aislamiento tenant.

El backend operativo no almacena credenciales, refresh tokens ni material privado, y no emite tokens. Valida únicamente access tokens JWT firmados de manera asimétrica por `MS.Cobroup.BackendAdmin`, usando las claves públicas publicadas en `/api/v1/auth/.well-known/jwks.json`.

La validación exige algoritmo permitido (`RS256` o `ES256`), firma, `kid`, emisor exacto, audiencia `cobroup-api`, vigencia y los claims `typ=access`, `sub`, `exp`, `iat`, `nbf`, `jti`, `organization_id` y rol. La ausencia o invalidez de cualquiera de estos datos produce rechazo. No se admiten HMAC, claves compartidas, secretos de respaldo, algoritmos alternativos ni validación deshabilitada.

Los casos de uso derivan tenant, identidad y rol del principal validado, nunca de valores confiados del cliente. El contrato normativo completo está en `cobroup.md/DECISION_AUTENTICACION_JWT.md`; la fase de identidad se mantiene reabierta hasta demostrar todos sus criterios.

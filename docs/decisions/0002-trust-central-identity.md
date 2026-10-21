# ADR-002 — Confianza en la autoridad central

Estado: aceptada para fase 2.

El backend operativo no almacena credenciales ni emite tokens. Valida mediante discovery OIDC los JWT firmados por `MS.Cobroup.BackendAdmin`, exige audiencia `cobroup-api` y rechaza identidades sin `sub` u `organization_id`. Los futuros casos de uso deberán derivar el tenant del principal validado, nunca de un valor confiado del cliente.

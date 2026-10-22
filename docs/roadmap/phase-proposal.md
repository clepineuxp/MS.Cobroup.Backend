# Roadmap vigente de revalidación

Actualizado: 2026-10-22.

1. **Fase 2 — reabierta y bloqueante:** sustituir la validación actual por el contrato JWT `HS256` definido en `../../../cobroup.md/DECISION_AUTENTICACION_JWT.md`, retirar JWKS/RSA y demostrar validación estricta con el secreto propio del ambiente.
2. **Fases 3 a 9 — cierre histórico conservado:** su funcionalidad versionada no se elimina, pero toda evidencia que dependa de autenticación debe repetirse con JWT antes de aprobar producción.
3. **Cierre de identidad:** exige pruebas negativas de firma, allowlist exacta `HS256`, emisor, audiencia, tipo, vigencia, `kid`, tenant y rol, además de rotación coordinada sin interrupción.

La planeación consolidada mantiene 8 fases cerradas y 1 reabierta. Este documento no aprueba la implementación actual ni autoriza despliegue. Ver `../../../cobroup.md/informe/avance.md`.

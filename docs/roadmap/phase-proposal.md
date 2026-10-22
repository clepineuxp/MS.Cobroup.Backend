# Roadmap vigente de revalidación

Actualizado: 2026-10-22.

1. **Fase 2 — reabierta y bloqueante:** sustituir la implementación de identidad por el contrato JWT asimétrico definido en `../../../cobroup.md/DECISION_AUTENTICACION_JWT.md` y demostrar validación estricta desde este backend.
2. **Fases 3 a 9 — cierre histórico conservado:** su funcionalidad versionada no se elimina, pero toda evidencia que dependa de autenticación debe repetirse con JWT antes de aprobar producción.
3. **Cierre de identidad:** exige pruebas negativas de firma, algoritmo, emisor, audiencia, tipo, vigencia, `kid`, tenant y rol, además de rotación de claves sin interrupción.

La planeación consolidada mantiene 8 fases cerradas y 1 reabierta. Este documento no aprueba la implementación actual ni autoriza despliegue. Ver `../../../cobroup.md/informe/avance.md`.

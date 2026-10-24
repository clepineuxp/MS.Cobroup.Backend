# Identificación de clientes

El POST/PUT /api/operations/v1/customers exige identification (string no vacío, 1–64 caracteres tras trim). Es texto: 001234 debe conservarse como 001234. No hay unicidad nueva. El valor exacto 0 solo puede conservarse al actualizar un registro que ya tiene 0; se rechaza en altas y al sustituir un ID válido. GET de detalle y listado incluye identification. El proxy administrativo /api/v1/customers transporta la misma propiedad.

Campo en el JSON de escritura: `"identification": "001234567"`. Los demás campos de nombre, ruta y contactos mantienen el contrato anterior. No se debe omitir la identificación al actualizar ruta/estado.

Migración: ../customer-identification-migration.sql (idempotente sobre el historial operations.__EFMigrationsHistory). Solo las filas existentes reciben 0; no existe DEFAULT para nuevas filas. Aplicada en Dev el 2026-10-24 (7 clientes), no en QA/Prod. Deploy del backend operativo y frontends debe coordinarse. Decisión canónica: ../../../cobroup.md/DECISION_IDENTIFICACION_CLIENTE.md.

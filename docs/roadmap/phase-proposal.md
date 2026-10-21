# Propuesta priorizada posterior a fase 1

1. Identidad y aislamiento tenant: cerrar ADR-001/011 y pruebas cross-tenant. Afecta ambos backend y ambos frontend. Riesgo alto; rollback por feature flag de autenticación.
2. Configuración y rutas: cerrar ADR-002/005/009; implementar ownership administrativo y contratos de lectura. Riesgo alto de autoridad duplicada.
3. Clientes y alcance operativo: teléfonos, ubicaciones y cupos con pruebas AC-01/04/05/20. Depende de identidad y rutas.
4. Plantillas y excepciones: contratos versionados y aprobación ADMIN. Depende de configuración e identidad.
5. Préstamos y cronograma: cerrar ADR-003/004/007 antes de reglas financieras. Pruebas de precisión y calendario.
6. Pagos, evidencias y agenda: idempotencia, privacidad, mora y conciliación. Depende del préstamo estable.
7. Mapas, analítica y auditoría: cerrar ADR-006/010; proyecciones de lectura sin escrituras cruzadas.
8. Endurecimiento y despliegue: ADR-012, observabilidad, recuperación, seguridad y pruebas E2E.

Esta propuesta no autoriza implementación adicional ni fechas.

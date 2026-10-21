# Contratos preliminares v1

## Identidad y autorización

Los tokens futuros deberán aportar `sub`, `tenant_id`, roles y vigencia. La API resolverá internamente el alcance de rutas; no confiará en un `tenant_id` enviado como parámetro. Roles canónicos: `ADMIN`, `SUPERVISOR`, `COLLECTOR`.

## Contratos consumidores

| Consumidor | Servicio | Capacidad | Propietario lógico |
|---|---|---|---|
| `MF.cobroup-frontend` | Este servicio | Lecturas y comandos operativos futuros | Backend operativo |
| `MF.cobroup-frontend-admin` | Backend admin | Configuración y administración | Backend admin |
| Backend admin | Este servicio | Consultas operativas para mapas/reportes mediante API/proyección | Backend operativo |
| Este servicio | Backend admin | Lectura versionada de configuración y decisión de excepciones | Backend admin |

Los errores usarán un sobre versionado con código estable, mensaje seguro, `traceId` y detalles no sensibles. Fechas auditables serán UTC; moneda y zona horaria procederán del tenant. Los comandos financieros futuros requerirán idempotencia.

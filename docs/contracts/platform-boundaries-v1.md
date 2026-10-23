# Contratos preliminares v1

## Identidad y autorización

La API acepta únicamente access tokens JWT `HS256` emitidos por BackendAdmin para audiencia `cobroup-api`. Deben aportar `typ=access`, `sub`, `iss`, `aud`, `exp`, `iat`, `nbf`, `jti`, `organization_id` y rol. La API valida localmente firma, algoritmo exacto, `kid`, emisor, audiencia y vigencia usando el secreto del ambiente; no emite tokens ni confía en un tenant enviado por parámetro. Roles canónicos: `ADMIN`, `SUPERVISOR`, `COLLECTOR`. Ver `../../../cobroup.md/DECISION_AUTENTICACION_JWT.md`.

## Contratos consumidores

| Consumidor | Servicio | Capacidad | Propietario lógico |
|---|---|---|---|
| `MF.cobroup-frontend` | Este servicio | Lecturas y comandos operativos futuros | Backend operativo |
| `MF.cobroup-frontend-admin` | Backend admin | Configuración y administración | Backend admin |
| Backend admin | Este servicio | Consultas operativas para mapas/reportes mediante API/proyección | Backend operativo |
| Este servicio | Backend admin | Lectura versionada de configuración y decisión de excepciones | Backend admin |

Los errores usarán un sobre versionado con código estable, mensaje seguro, `traceId` y detalles no sensibles. Fechas auditables serán UTC; moneda y zona horaria procederán del tenant. Los comandos financieros futuros requerirán idempotencia.

## Contrato de rutas v1

`GET /api/operations/v1/routes` reenvía el bearer token al directorio administrativo y devuelve `id`, `name`, `description`, `accessAs`, `maxActiveCustomers` y `maxActiveLoans`. No acepta tenant ni usuario por parámetro. Los límites `null` significan sin límite configurado. Una dependencia administrativa no disponible produce `503`.

## Contrato de clientes v1

`GET|POST /api/operations/v1/customers` y `GET|PUT /api/operations/v1/customers/{id}` pertenecen exclusivamente al backend operativo. Tenant y rutas se derivan del token y del directorio administrativo. `Customer`, teléfonos y ubicaciones se almacenan en el esquema `operations`; el cupo de clientes activos se comprueba bajo bloqueo transaccional por ruta.

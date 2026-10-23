# ADR-0005 — Validación JWT con firma simétrica HS256 por ambiente

Estado: Aceptada el 2026-10-22.
Supersede: ADR-0002 en lo referente a algoritmo (`RS256`), JWKS y distribución asimétrica.

## Contexto

Conforme a la decisión normativa vinculante en `cobroup.md/DECISION_AUTENTICACION_JWT.md` y `MS.Cobroup.BackendAdmin/docs/decisions/0007-symmetric-hs256-per-environment.md`, se cerró la migración hacia firma simétrica con algoritmo estricto `HS256`.

`MS.Cobroup.Backend` actúa exclusivamente como **consumidor/validador operativo** de los access tokens y **nunca emite tokens**.

## Decisión

1. **Algoritmo Estricto:** Se valida exclusivamente `SecurityAlgorithms.HmacSha256` (`alg=HS256`). Se rechaza de forma determinista cualquier otro algoritmo (`none`, `RS256`, `ES256`, `HS384`, `HS512`).
2. **Material Criptográfico:** Se recibe la clave simétrica mediante la variable de entorno `Jwt__SigningKeyBase64`, originada en el GitHub Environment Secret correspondiente (`DEV_JWT_SIGNING_KEY_BASE64`, `QA_JWT_SIGNING_KEY_BASE64`, `PROD_JWT_SIGNING_KEY_BASE64`).
3. **Fail-Fast en Startup:** La aplicación falla inmediatamente al iniciar si la clave está ausente, vacía, contiene placeholders, no es Base64 válido o decodifica a menos de 32 bytes (256 bits). No se admiten valores por defecto ni generación efímera.
4. **Validación Local:** Se elimina toda dependencia de JWKS, clientes HTTP internos de claves y health checks asociados a endpoints JWKS. La validación se realiza en memoria mediante `ISymmetricTokenValidationService`.
5. **Rotación:** Soporta clave activa (`Jwt:ActiveKeyId`) y conjunto de claves anteriores para validación (`Jwt:PreviousValidationKeys:k...`) durante ventanas de rotación programadas.
6. **Frontera de Emisión:** `MS.Cobroup.Backend` no contiene servicios, controladores ni endpoints de generación de tokens JWT. Permanece como validador puro de `typ=access` con audiencia `cobroup-api`.

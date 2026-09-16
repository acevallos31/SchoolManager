# Handoff 043A — normalización segura de errores de negocio

## Objetivo

Cerrar la deuda de mensajes técnicos de PostgreSQL que todavía podían llegar al
frontend desde `ApiControllerBase` y desde el contrato especial de
`ConfiguracionController`, sin mover invariantes fuera de PostgreSQL/RPC ni
cambiar los status HTTP existentes.

## Rama

`fix/043a-errores-negocio-seguros`

## Base

`main` en `20bea4f9dd346836205620dce2add6f60d17b372` (post PR #97).

## Cambios

### `ApiControllerBase`

- conserva mensajes específicos para constraints conocidas;
- normaliza fallbacks por SQLSTATE para `42501`, `P0002`, `23505`, `23514`,
  `22023`, `23503` y `SM001`→`SM004`;
- una constraint desconocida ya no devuelve `PostgresException.MessageText`;
- un SQLSTATE desconocido usa un mensaje genérico seguro;
- `P0001` conserva `MessageText` porque corresponde a `RAISE EXCEPTION`
  controlado por las RPC de negocio del proyecto;
- no cambia el mapeo HTTP: 403/404/409/400 se conserva.

### `ConfiguracionController`

El contrato sigue devolviendo `{ error, code }` para que el frontend distinga
`SM00x`, pero `error` pasa por `MensajeError(ex)` en lugar de exponer
`ex.MessageText` directamente.

### Pruebas

Nuevo `ApiControllerBaseErrorTests` cubre:

- constraint conocida → mensaje de negocio específico;
- constraint desconocida → conflicto genérico sin nombre interno;
- SQLSTATE conocidos → status + mensaje seguro;
- `P0001` → conserva mensaje intencional de RPC;
- SQLSTATE desconocido → fallback genérico sin texto técnico.

## Restricciones respetadas

- sin cambios de esquema ni migraciones;
- sin cambios de RLS/RPC;
- sin cambios de contratos de status HTTP;
- sin secretos ni infraestructura;
- sin escrituras en producción.

## Validación requerida

CI debe ejecutar al menos:

- `git diff --check`;
- build backend Release;
- API integration tests;
- DB integration tests (regresión global del pipeline);
- frontend/build y Sonar según el workflow existente.

No mergear hasta CI + Quality Gate verdes.

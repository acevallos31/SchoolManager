# HANDOFF — Producción alineada hasta migración 025

## Fecha
2026-09-09 (UTC-6).

## Rama de documentación
`docs/cierre-prod-019-025`

## Estado general
Producción quedó alineada con el esquema esperado por `main` hasta la migración 025.
El incidente de permisos/carga de `/configuracion/estructura-academica` quedó resuelto.

## Qué ocurrió
El frontend/backend ya consumían contratos y permisos introducidos después de la migración 018, pero la base de datos de producción seguía detenida en 018.

Esto provocaba en Estructura Académica:
- `No tienes permiso para realizar esta operación`;
- listado de grados vacío;
- comportamiento inconsistente entre código desplegado y esquema real.

## Auditoría previa
Se confirmó en producción que:
- `schema_migrations` llegaba únicamente hasta 018;
- 019→025 no estaban registradas;
- tampoco existían físicamente sus objetos principales;
- el gate de datos de 020 no tenía conflictos:
  - 1 institución activa;
  - 0 grados huérfanos;
  - 0 jornadas huérfanas;
  - 0 grados compartidos;
  - 0 jornadas compartidas;
  - 0 duplicados por institución.

## Backup
Antes de aplicar cambios se generó backup restorable del schema `public` con PostgreSQL 17 `pg_dump`.
No restaurar sobre producción salvo incidente real y procedimiento explícito.

## PR #60 — validaciones de migraciones
Mergeado en `main`:
- merge SHA: `0ede6ff9a5d9f9ff69da3f1e0a693ce89c304d59`;
- añade validaciones faltantes de 019 y 022;
- fortalece `MigrationTests` para exigir relación 1:1 migración/rollback/validation;
- CI run #287: verde;
- Sonar Quality Gate: verde;
- no se bajaron thresholds ni se excluyeron validaciones.

## Migraciones aplicadas a producción
Se ejecutaron una por una con PostgreSQL 17 `psql`, `ON_ERROR_STOP=1`, deteniéndose después de cada una para validar:

1. `019_cargos_mensualidades_obligaciones.sql` — COMMIT — validación: 0 hallazgos.
2. `020_grados_jornadas_multiinstitucion.sql` — COMMIT — validación: 0 hallazgos.
3. `021_pagos_cobranza.sql` — COMMIT — validación: 0 hallazgos.
4. `022_portal_responsable_lectura.sql` — COMMIT — validación: 0 hallazgos.
5. `023_rbac_permisos_aplicacion_ciclos.sql` — COMMIT — validación: 0 hallazgos.
6. `024_rbac_permisos_aplicacion_estructura.sql` — COMMIT — validación: 0 hallazgos.
7. `025_fix_unicidad_grados_jornadas_institucion.sql` — COMMIT — validación: 0 hallazgos.

## Resultado funcional
Después de refrescar sesión/login:
- `/configuracion/estructura-academica` carga correctamente;
- desapareció el mensaje de falta de permiso;
- los datos académicos vuelven a mostrarse;
- no se observaron errores visibles en la prueba manual.

## Estado de producción
```text
001-018  ya aplicadas previamente
019      aplicada + validada
020      aplicada + validada
021      aplicada + validada
022      aplicada + validada
023      aplicada + validada
024      aplicada + validada
025      aplicada + validada
```

## Puntos importantes
- 025 corrige el residual histórico de unicidad global de grados/jornadas que 020 no cubría para todos los nombres de constraints legacy.
- La unicidad final es por institución con índices:
  - `ux_grados_institucion_nombre`;
  - `ux_jornadas_institucion_nombre`;
  ambos `UNIQUE` sobre `(institucion_id, lower(btrim(nombre)))`.
- 023/024 agregan permisos de aplicación para ciclos y estructura.
- La capa interna DB conserva permisos `configuracion.*`; no mezclar sin una decisión explícita de arquitectura.

## Arquitectura vigente
- Angular -> API .NET -> PostgreSQL/Supabase/RPC.
- Supabase directo en frontend únicamente para Auth (`auth.ts`).
- Deuda #10 de Supabase directo de negocio: resuelta en Bloque 030 / PR #53.
- `PermissionGuard` es el guard vigente.
- `AdminGuard`/`PadreGuard` no deben reintroducirse.
- `/portal-padre` sigue fuera del AppShell administrativo y es read-only.

## Próximos pasos
1. Mejorar navegación de Estructura Académica para que no quede escondida únicamente dentro de Configuración.
2. Validar también `/configuracion/ciclos` en producción.
3. Smoke test no destructivo de Cargos, Pagos y Portal Responsable.
4. Mantener pendiente E2E autenticado hasta disponer de staging seguro.
5. Más adelante: selector global multiinstitución y revisión de la divergencia de namespaces de permisos app/DB.

## Reglas operativas
- Siempre rama; no escribir directo a `main`.
- No force push.
- No secretos en repo.
- No E2E destructivo en producción.
- No aplicar migraciones fuera de orden.
- Validar cada migración antes de continuar.
- `AGENTS.md` sigue siendo la referencia operativa del repositorio.

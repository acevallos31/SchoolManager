# Bloque 022 — Portal Responsable (prototipo funcional de SOLO LECTURA)

## Estado: MERGEADO a main — PR #45 (`bed6a85`, 2026-09-06)

Rama: `feature/portal-responsable-fase-022` (base `main` post-merge de PR #44 = `f61c931`).
PR contra main: **mergeado** (`bed6a85`). Revisión humana aplicada en `cfc877e`.
Integrado en `main`; este handoff es el registro histórico del bloque.

## Objetivo cumplido
Un usuario padre/responsable autenticado puede: iniciar sesión → entrar a `/portal-padre` →
ver SOLO sus hijos reales (vínculo activo + `acceso_financiero=true`) → seleccionar uno →
ver resumen financiero real, cargos y pagos reales. Nada de escrituras, tarjeta simulada ni
Supabase directo desde el portal.

## Commits (5, oldest→newest)
1. `1c227fb` db(022): superficie de lectura del portal responsable (mis alumnos + finanzas del hijo)
2. `88ccc9a` api(022): endpoints de lectura del portal responsable
3. `94a0486` test(api): cobertura del portal responsable
4. `1067d3e` db(022): rollback de la migracion 022 y actualizacion de invariantes de migraciones
5. `76c0e40` fe(022): portal responsable real contra API .NET

## Capa DB — `database/migrations/022_portal_responsable_lectura.sql`
Aditiva SOLO LECTURA. No toca 001-021 (checksum 021 intacto). Helpers de identidad de 009.
Funciones nuevas (SECURITY DEFINER, `search_path=pg_catalog,public`, guard por identidad del
responsable financiero — NO por permiso `academico.*`):
- `public.usuario_es_responsable_financiero_del_alumno(p_alumno_id uuid) returns boolean`
  → true si el usuario actual (usuarios.persona_id) es responsable con vínculo
  `alumno_responsable` estado `activo` + `acceso_financiero=true` EN LA MISMA institución del alumno.
- `public.rpc_mis_alumnos_responsable() returns table(alumno_id, institucion_id, nombres, apellidos, parentesco, es_principal)`
  → hijos del responsable financiero autenticado (solo acceso_financiero=true).
- `public.rpc_resumen_financiero_responsable(p_alumno_id uuid, p_institucion_id uuid default null)
  returns table(...)` — proyección idéntica a `rpc_resumen_financiero_alumno` de 021.
- `public.rpc_cargos_responsable(...)` — espeja `rpc_listar_cargos_alumno` (021).
- `public.rpc_pagos_responsable(...)` — espeja `rpc_listar_pagos_alumno` (021).
- `public.rpc_pago_aplicaciones_responsable(p_pago_id uuid, p_institucion_id uuid default null)`.
Resolución de institución: si p_institucion_id es null se deduce del alumno. Sin responsable
arbitrario del cliente: la identidad sale del claim `sub`/`auth.uid()`, nunca de parámetros.
Aislamiento multi-institución sin fuga cross-tenant ni cross-alumno. Re-grants al pie replicando 021/009.
Rollback: `database/migrations/rollback/022_portal_responsable_lectura.rollback.sql` (DROP funciones
+ DELETE schema_migrations version='022').

## Capa API — `PortalResponsableController` (+ DTO `MisAlumnoDto`)
Reutiliza `CargoDto`, `ResumenFinancieroDto`, `PagoDto`, `AplicacionPagoDto` de 021 (misma proyección;
lee por índice ordinal). Autorización: solo autenticación (la DB resuelve identidad y niega a
no-responsables; sin institución del cliente).
- `GET /api/responsable/mis-alumnos` → hijos del responsable autenticado.
- `GET /api/responsable/alumnos/{id}/resumen`
- `GET /api/responsable/alumnos/{id}/cargos`
- `GET /api/responsable/alumnos/{id}/pagos`
- `GET /api/responsable/pagos/{pagoId}/aplicaciones`
Sin sesión → 401; no-responsable / alumno ajeno → 403 o lista vacía según el caso.

## Capa FE — `pages/portal-padre/*` reescrito + `core/services/portal-responsable.service.ts`
Flujo real contra la API .NET (sin Supabase). Pantalla: encabezado con título del portal, tarjetas/
selector de hijos, resumen financiero, pestañas Resumen/Cargos/Pagos, formato HNL, badge de vencido,
badges por estado (pendiente/parcial/pagado/anulado), estados loading/empty/error, logout.
SIN botón de pagar / tarjeta simulada. Responsive móvil (CSS legacy base reutilizado).

## Validación ejecutada (verde)
- DB: `tests/SchoolManager.Database.IntegrationTests` → **151/151** (145 previos + 6 nuevos PortalResponsable).
- API: `tests/SchoolManager.API.IntegrationTests` → **84/84** (79 previos + 5 nuevos PortalResponsableController).
- FE: `npx ng test --watch=false` → **172/172** (162 previos + 10 nuevos portal-padre).
- Backend Release build: **Build succeeded, 0 errors**.
- FE production build: **bundle generation complete**.
- `git diff --check origin/main..HEAD` → OK.

## Cobertura nueva (tests)
- DB (PortalResponsableLecturaTests): responsable ve SOLO sus hijos; acceso_financiero=false lo
  excluye; alumno ajeno de otra institución aislado (vacío/raise P0002); requiere sesión.
- API (PortalResponsableControllerTests): mis-alumnos (vinculado visible, ajeno no), resumen/cargos/
  pagos del hijo, sin sesión → 401.
- FE (portal-padre.spec.ts): carga hijos + autoselección, selección, resumen, cargos, pagos +
  aplicaciones, estado vacío, error hijos, error alumno, sin sesión → login, HNL, logout. Verifica
  ausencia de botón "Pagar"/tarjeta.

## Restricciones respetadas
Sin producción/Supabase remoto. Sin pagos online reales. Sin tarjeta simulada. Sin facturación
fiscal. Sin conciliación. Sin 023. 001-021 intactos.

## Limitaciones / supuestos del prototipo
- `rpc_mis_alumnos_responsable` no expone grado/sección/ciclo de la matrícula vigente (decisión:
  mantener la proyección correcta y verifiable; no se arriesgó el nombre de columnas de grados/ciclos).
- Encabezado muestra título "Portal del Responsable": el nombre del usuario no está expuesto por
  `UsuarioActual` ni por un endpoint sin un RPC adicional (el legacy también usaba fallback "Usuario").
- El helper `usuario_es_responsable_financiero_del_alumno` exige vínculo `acceso_financiero=true`
  (decisión de dominio 022: portal exclusivamente financiero). Si se objeta, se ajusta a ambos estados.

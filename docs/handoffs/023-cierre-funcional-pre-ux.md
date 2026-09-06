# Bloque 023 — Cierre Funcional Pre-UX (Fase 0: auditoría + deuda real)

## Estado: LISTO PARA REVISIÓN — PR contra main (sin mergear por regla)

Rama: `chore/cierre-funcional-pre-ux-023`.
Base: `origin/main` = `bed6a85` (merge PR #45 de 022).
PR contra main: **abierto — NO mergear** (espera revisión humana).
No 024. No Vertic. No rediseño visual. No Figma. No producción/Supabase remoto.

## Objetivo cumplido
Dejar SchoolManager **funcionalmente consistente** antes del bloque visual/UX:
auditar frontend/backend/docs (Fase 0), eliminar flujos falsos/legacy y deuda
muerta, y dejar la documentación reflejando el estado real de main (021/022
mergeados). Sin nueva funcionalidad salvo reparación; sin refactor por gusto de
flujos que funcionan.

## Qué se hizo
1. **Fase 0 — Auditoría** (subagentes delegados FE/backend/docs + verificación
   manual de cada hallazgo accionable). Conclusión: los **flujos principales
   funcionan** (login Supabase Auth, alumnos, matrícula, responsables,
   configuración académica, conceptos/planes, cargos, pagos, portal responsable).
   No hay pantallas dummy ni botones muertos conocidos. portal-padre ya consume
   API .NET (022); la página legacy `mensualidades` fue retirada antes.
2. **Eliminación de código muerto / flujos falsos**:
   - Guards FE `AdminGuard`/`PadreGuard` (`core/guards/admin.guard.ts`,
     `padre.guard.ts`): 0 referencias (solo auto-ref; las rutas usan
     `PermissionGuard`). Eliminados.
   - `AlumnosController.cs` (backend): **stub** que respondía HTTP 200 con
     mensajes fijos sin consultar Postgres; nada lo consumía (el FE usa
     Supabase directo vía `alumno.service`). Eliminado junto a su DTO
     (`DTOs/AlumnoDto.cs`) y Models huérfanos (`Models/Alumno.cs`,
     `Models/Matricula.cs`) — todos con 0 referencias de controller/test.
3. **Comentarios obsoletos corregidos** (reflejaban 021 como "pendiente" cuando
   ya está en main): `app.routes.ts` (portal-padre), `CargosController.cs:13`,
   `DTOs/CargoDto.cs`, `DTOs/PagoDto.cs`.
4. **Limpieza documental** (ver commits).

## Commits (rama, oldest→newest)
1. `7abe6dc` refactor: elimina código muerto y comenta estado real de 021/022
   (guards FE + AlumnosController/DTOs/Models stub + comentarios obsoletos).
2. `39c048e` docs: refleja estado real post-021/022 y nueva deuda #10
   (AI_CONTEXT reescrito; technical-debt #1/#6/#7/#8 actualizados + #10 nueva;
   handoffs 021/022 marcados mergeados).

## Qué NO se hizo (y por qué)
- **Migrar a la API .NET** las páginas que consultan Supabase directo
  (`/alumnos`, `/matriculas`, `/configuracion/ciclos`,
  `/configuracion/estructura-academica` vía `alumno.service`,
  `ciclo-escolar.service`, `configuracion.service`, `estructura-academica.service`):
  funcionan contra Supabase real y para ciclos/estructura-académica/configuración
  **no existe controller .NET** que los reemplace; migrarlos = **nueva
  funcionalidad** (prohibida salvo reparar flujo roto). Registrada como deuda
  #10 (abierta, deliberada) en `technical-debt.md`; se aborda en un bloque
  dedicado post-UX.
- **Login**: es por diseño Supabase Auth + `GET /auth/me` en .NET; no se añadió
  `POST /api/auth/login`.
- Rediseño visual/Vertic/024/Figma/producción.

## Limpieza documental (`docs/`)
- **`AI_CONTEXT.md`** reescrito: migraciones 019-022 todas en main; grados y
  jornadas **por institución** (020); módulos 021 (pagos) y 022 (portal
  responsable) en main; login por Supabase Auth; guards muertos eliminados;
  deuda #10 (Supabase directo) documentada.
- **`technical-debt.md`**:
  - #1 → nota de guards eliminados en 023.
  - #6 → **RESUELTO** por 021 (PR #44).
  - #7 → mecanismo env corregido (no `secrets.*` en `if`).
  - #8 → **RESUELTO** (PR #41 en main; portal-padre resuelto por 022).
  - **#10 nueva (ABIERTA, deliberada)**: páginas de negocio que consultan
    Supabase directo; no es flujo roto; bloque dedicado de migración post-UX.
- **Handoffs 021 y 022** → marcados como **mergeados** (PR #44 `f61c931` /
  PR #45 `bed6a85`).

## Validación ejecutada (verde — ver resultado real en el cuerpo del PR)
- DB: `tests/SchoolManager.Database.IntegrationTests` completo.
- API: `tests/SchoolManager.API.IntegrationTests` completo.
- FE: `npx ng test --watch=false` completo.
- Backend Release build y FE production build: OK.
- Gate de cobertura (`scripts/check-coverage-gate.py`) y `git diff --check`: OK.

## Restricciones respetadas
Sin producción/Supabase remoto. Sin Vertic/024/rediseño. Sin nuevas
funcionalidades. Commits pequeños por capas. Push a la rama. PR contra main sin
mergear.

## Estado de deuda realmente viva tras 023 (ver `docs/technical-debt.md`)
- **#7** SonarCloud import (acción manual del mantenedor: SONAR_TOKEN + paso manual).
- **#9** New Code de SonarCloud (depende de #7); gate de regresión local activo.
- **#10** Supabase directo en páginas de negocio (deliberado; bloque dedicado).
- Riesgo residual de #1 (rutas genéricas de configuración solo por autenticación).

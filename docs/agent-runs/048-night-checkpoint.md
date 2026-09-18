# 048 — Checkpoint de la noche (auth debt close)

Rama: `chore/048-night-auth-debt-close`
Base: rebasada sobre el commit remoto `793b2d5e6cfe32a6e347169790130ca73859b52b`
Prompt operativo: `docs/agent-prompts/048-launcher.md` (existe y fue verificado)
Última actualización: 2026-09-18 (Phase 1 CERRADA)

## Phase 1 — CERRADA (frontend/session-state + semántica de identidad)

### Causa raíz confirmada (case Demo) — frontend/session-state
`AuthService.asegurarUsuarioInicial()` **memoiza `inicializacionPromise`**: un
`/api/auth/me` 403 transitorio (`IDENTIDAD_NO_VINCULADA`) quedaba cacheado para
siempre; llamadas posteriores (auth-callback tras OAuth, guards, re-bootstraps)
re-servían el fallo antiguo sin re-consultar `/me`, aunque el backend ya
respondiera 200 para un usuario válido. Resultado: el mensaje previo persistía y
la sesión quedaba bloqueada/redirigida al login.

**Evidencia de producción (Render):** tras reproducir el login Demo, dos
`GET /api/auth/me` con `StatusCode=200`, `Authenticated=true` y
`UserId=a6a3d715-2418-4fb5-b95c-072e0ce216cf` = `auth.users.id` =
`public.usuarios.auth_user_id`. Descartadas para Demo: RLS, identidad distinta,
DB equivocada.

### Fix mínimo — commit `db8ca6a` (2 partes)
1. `restaurarSesion()` catch: `this.inicializacionPromise = null` → una
   restauración fallida no queda cacheada; el siguiente bootstrap re-consulta
   `/me` y despeja el mensaje previo.
2. `login()`: al establecer sesión + usuario válidos, `mensajeSesionInvalida = null`
   descarta cualquier mensaje pendiente.

### Tests y suites (evidencia real)
- TDD: 2 tests nuevos en `auth.spec.ts` (mensaje previo `IDENTIDAD_NO_VINCULADA`
  → `/me` 200 posterior no debe re-mostrarse/bloquear).
- RED: **2 failed / 416 passed**; GREEN: **418/418 passed** (40 archivos).
- Conteo final: **API 252/252** (incluye 3 tests RLS de UsuarioActualService),
  **DB 220/220**, **Frontend 418/418**. `git diff --check` limpio.
- Push: `e7710a6..db8ca6a` en `chore/048-night-auth-debt-close`.

## Historial: bloque 1 (semántica de identidad) — CERRADO

### Cambios aplicados (commit de esta noche)
Separación semántica de los tres estados que antes se conflacionaban en un solo
`IDENTIDAD_NO_VINCULADA`, de extremo a extremo:

- `backend/SchoolManager.API/Identity/IdentidadNoVinculadaException.cs`
  nueva `DatosUsuarioIncompletosException` (vinculado pero sin persona utilizable).
- `backend/SchoolManager.API/Identity/UsuarioActualService.cs`
  `join` → `left join public.personas`; si `persona_id`/nombres son nulos lanza
  `DatosUsuarioIncompletosException` en lugar de devolver cero filas y reportarse
  como identidad no vinculada.
- `backend/SchoolManager.API/Controllers/AuthController.cs`
  403 con `codigo = PERFIL_INCOMPLETO` (distinto de `IDENTIDAD_NO_VINCULADA`).
- `backend/SchoolManager.API/Authorization/PermisoAuthorizationHandler.cs`
  captura `IdentidadNoVinculadaException` / `UsuarioInactivoException` /
  `DatosUsuarioIncompletosException`: una identidad no resoluble ahora falla la
  autorización limpiamente en vez de propagar la excepción (500).
- `frontend/.../core/services/auth.ts`
  `mapearErrorPerfil` conserva los códigos del backend (`IDENTIDAD_NO_VINCULADA`,
  `USUARIO_INACTIVO`, `PERFIL_INCOMPLETO`) y un 403 **sin** código pasa a
  `PERFIL_NO_HABILITADO`. Antes los tres se colapsaban a `USER_PROFILE_NOT_FOUND`.
- `frontend/.../pages/login/login.ts` mensajes distintos por código.

### Pruebas ejecutadas (evidencia real, no estimada)
- `dotnet test tests/SchoolManager.API.IntegrationTests` → **249/249 passed** (incluye
  2 pruebas nuevas: `Usuario_vinculado_sin_perfil_de_persona_no_reporta_identidad_no_vinculada`
  y `Usuario_vinculado_sin_persona_no_se_reporta_como_identidad_no_vinculada`).
- `npx ng test --watch=false` → **40 archivos / 416 pruebas passed** (incluye las
  aserciones nuevas de códigos diferenciales en `auth.spec.ts`).
- Docker operativo; imagen `postgres:16-alpine` en caché (los tests de integración
  corren de verdad, no están saltados).

## Causa raíz del caso Demo: descartes con evidencia

| Hipótesis | Veredicto | Evidencia |
|---|---|---|
| Firma/issuer/audience del JWT, o claim `sub` mal parseado | **Descartada** | El 403 solo se emite tras `Guid.TryParse(sub)` exitoso; un sub inválido da `SESION_INVALIDA`/401. |
| Proyecto Supabase distinto entre frontend y backend | **Descartada** (config commiteada) | `environment.ts` y `appsettings` apuntan al mismo project ref. |
| `persona_id` nulo | **No representable en el baseline** | `database/baseline/001_schoolmanager_fase1a.sql:86` → `persona_id uuid not null references public.personas(id)`. Solo posible en instalaciones heredadas (migración 003 lo declara nullable). Endurecido igualmente. |
| El backend desplegado lee otra base que la que se revisó a mano | **VIVA — hipótesis principal** | Ver siguiente sección. |
| RLS filtrando filas para el rol de conexión del API | **Refutada como causa en prod** | Demo por test (commit `8a36b79`): con `authenticated` sin `request.jwt.claim.sub` se reproduce `IDENTIDAD_NO_VINCULADA`; con el sub publicado vuelve a verse. **Pero** no existe `FORCE ROW LEVEL SECURITY` en `database/`, así que el owner (conexión estándar Supabase) ignora RLS → la fila está visible. Además `authenticated` no tiene SELECT sobre `instituciones` → un rol no-owner rompería con `42501` (500) para todos, no 403 solo en Demo. |

## Siguiente paso (arranque de la próxima sesión)

Phase 1 cerrada. Los pasos de la sección anterior (leer logs de Render de
`/api/auth/me`, serializar/refutar RLS) quedaron **RESUELTOS**: la evidencia de
Render (`/me`=200, Authenticated=true, UserId=a6a3d715…) confirma que el backend
resuelve la identidad; la causa raíz es el *session-state del frontend* (memoización
de `inicializacionPromise`), corregida en `db8ca6a`.

Próximo arranque: **Phase 2 — flujo administrativo de aprobación de identidad
pendiente** en Configuración > Seguridad y acceso (distinguir activo/inactivo de
vinculado/pendiente; revisar identidad externa pendiente; aprobar/rechazar explícito
reutilizando `public.vincular_identidad_usuario(usuario_id, auth_user_id)`; auditar;
autorización backend; Angular solo orquesta; migración versionada SIN aplicar a prod).

## Restricciones respetadas
Sin merge, sin cambios en producción, sin carga contra producción. Todo el trabajo fue
local sobre `chore/048-night-auth-debt-close`.

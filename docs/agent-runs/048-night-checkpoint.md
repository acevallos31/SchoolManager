# 048 — Checkpoint de la noche (auth debt close)

Rama: `chore/048-night-auth-debt-close`
Base: rebasada sobre el commit remoto `793b2d5e6cfe32a6e347169790130ca73859b52b`
Prompt operativo: `docs/agent-prompts/048-launcher.md` (existe y fue verificado)
Última actualización: 2026-09-18

## Phase 1 — estado: bloque 1 CERRADO (semántica de identidad); causa raíz de producción PENDIENTE

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
| RLS filtrando filas para el rol de conexión del API | **VIVA — secundaria** | `009_seguridad_rls_rpc.sql` habilita RLS en `usuarios`/`personas`. Si el rol de conexión no es owner y no lleva contexto JWT, la fila se filtra → cero filas → `IDENTIDAD_NO_VINCULADA`. |

## Siguiente paso (arranque de la próxima sesión)

1. **Confirmar qué base lee el API desplegado** (sin escribir nada, sin tocar datos):
   revisar los logs de Render de `RequestObservabilityMiddleware` para el request
   fallido y comparar el `UserId` (sub) registrado con el `auth_user_id` observado a
   mano en la base; y revisar la variable `ConnectionStrings__PostgreSQL` del servicio
   Render vs. la config commiteada. Si el project ref difiere, la causa raíz está en
   la configuración del entorno, no en el código.
2. **Test local que reproduce el escenario RLS**: ejecutar la consulta de
   `UsuarioActualService` bajo un rol no-owner sujeto a RLS (sin contexto JWT) y
   comprobar que devuelve cero filas → serializa el bug como regresión.
3. `tests/SchoolManager.Database.IntegrationTests` → **220/220 passed** (ejecutado en
   este bloque; sin regresiones en RLS/RPC tras el cambio de join).
4. Continuar con las fases siguientes de `048-launcher.md`.

## Restricciones respetadas
Sin merge, sin cambios en producción, sin carga contra producción. Todo el trabajo fue
local sobre `chore/048-night-auth-debt-close`.

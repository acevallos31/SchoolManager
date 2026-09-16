# Handoff 044B — Backend staging + Supabase local efímero

## Objetivo

Cerrar la segunda capa del staging E2E sin crear infraestructura cloud ni tocar
producción: backend .NET en modo `Staging` con fail-fast y un Supabase local
reproducible/desechable.

## Rama

`feature/e2e-staging-backend-044b`

Base: `main` después del merge de 044A.

## Cambios

- `StagingSafety` valida `Jwt:Issuer`, PostgreSQL y CORS antes de levantar la API.
- Los hosts productivos conocidos están bloqueados explícitamente.
- `E2E_ALLOWED_HOSTS` permite únicamente agregar hosts de staging controlados.
- `appsettings.Staging.json` usa el issuer local y deja PostgreSQL vacío a
  propósito: la connection string debe entrar en runtime.
- `Program.cs` permite metadata HTTP solamente en `Staging` cuando el issuer
  local usa `http`; producción conserva el requisito HTTPS.
- Se agregaron tests para:
  - no alterar ambientes no-Staging;
  - rechazar Supabase productivo;
  - rechazar PostgreSQL productivo;
  - aceptar el stack local;
  - aceptar hosts de staging incluidos explícitamente en allowlist.
- `supabase/config.toml` define puertos locales 54321/54322 y PostgreSQL 17.
- `scripts/e2e/bootstrap-local-staging.py`:
  - regenera `supabase/migrations/` desde el baseline + migraciones 007+;
  - elimina volúmenes locales previos;
  - levanta Supabase local con los servicios mínimos necesarios;
  - imprime las variables de runtime;
  - destruye datos y migraciones generadas con `stop`.
- `supabase/migrations/` queda ignorado por Git: la autoridad continúa en
  `database/baseline/` y `database/migrations/`.
- CI valida sintaxis del bootstrap, `config.toml` y el contrato de
  `appsettings.Staging.json`; no crea infraestructura ni usa secretos.

## Seguridad

No se versiona ninguna contraseña, service-role key, connection string usable ni
credencial Auth. El stack local usa únicamente `localhost`/`127.0.0.1` y el
backend falla antes de arrancar si `Staging` intenta reutilizar un host de
producción.

No se ejecutó ninguna migración en Supabase cloud y no se modificó ningún dato
real.

## Uso local

```bash
python scripts/e2e/bootstrap-local-staging.py start
```

Después se usan las variables que imprime el script para arrancar la API con
`ASPNETCORE_ENVIRONMENT=Staging` y preparar el frontend con
`npm run prepare:staging`.

Al terminar:

```bash
python scripts/e2e/bootstrap-local-staging.py stop
```

## Qué NO cierra 044B

Todavía no existe seed determinista de usuarios/datos E2E ni se ejecuta
`auth.spec.ts` de extremo a extremo. Ese es el siguiente checkpoint.

## Siguiente checkpoint

**044C — seed E2E + autenticación local real**.

Crear institución/usuarios de prueba de forma idempotente, vincularlos con Auth
local y ejecutar login → dashboard → rutas protegidas → logout sin tocar
producción.

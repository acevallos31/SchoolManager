# Observabilidad — estado actual (auditoría read-only)

Estado de healthcheck, logging, errores y monitoreo del backend en
`main` (2026-09-05). Documenta qué existe y qué falta. **No** introduce
un sistema grande de observabilidad en esta rama: se deja como deuda
(ver `docs/technical-debt.md` #5). Principios ISW2 #5 y #11.

## Qué existe

### Healthcheck
- `GET /health` en `Program.cs` devuelve `200` con `{ status: "ok",
  service, timestamp }`. Es un chequeo de *liveness* básico del proceso.
- **No** comprueba dependencias (PostgreSQL / Supabase Auth) — no es un
  healthcheck de *readiness*. Un fallo de conexión a DB no lo hará fallar.

### Logging backend
- Solo el logging por consola por defecto de ASP.NET Core
  (`appsettings.json`: `Default: Information`, `Microsoft.AspNetCore:
  Warning`). Sin sink estructurado (serilog/OpenTelemetry), sin niveles
  configurables por entorno productivo más allá del default.
- `appsettings.json` no contiene secretos (los `Jwt:Issuer/Audience` y
  CORS son valores no sensibles; la cadena de conexión va por variable de
  entorno `ConnectionStrings__PostgreSQL`).

### Errores
- Manejo de errores por middleware/`ProblemDetails` por defecto de
  ASP.NET Core en controllers; excepciones no capturadas caen al log de
  consola. No hay un middleware global que registre errores con contexto
  (usuario, ruta, trace id) de forma estructurada.

### Monitoreo
- Sin métricas de aplicación, sin traces distribuidos, sin alertas. La
  infraestructura externa (Render/Vercel) ofrece dashboards básicos, pero
  no hay telemetría propia del API.

## Qué falta (deuda, no se implementa en esta rama)

| Carencia | Riesgo | Añadir cuando |
| --- | --- | --- |
| Healthcheck de readiness que verifique DB/Auth | el `/health` responde "ok" aunque el servicio no sirva requests correctamente | antes/ durante la fase 020 (flujo de dinero en prod) |
| Logging estructurado (serilog/OpenTelemetry) + niveles por entorno | diagnóstico lento de errores en prod; logs difíciles de filtrar | con un primer panel/monitor real |
| Registro de errores con contexto (request id, usuario, path) | no se puede correlacionar un fallo con una sesión | idem |
| Métricas + alertas | degradaciones pasan desapercibidas hasta el usuario | post-020, cuando exista superficie con dinero |

## Recomendación mínima (futura, fuera de esta rama)

1. Añadir `AddHealthChecks()` con una comprobación ligera a Postgres
   (conexión+`select 1`) y exponer `/health/ready`, manteniendo `/health`
   como liveness. Debe ir acompañado de un test de integración.
2. Adoptar logging estructurado mínimo (p. ej. serilog a consola en
   JSON) sin añadir infraestructura; es bajo riesgo y alto retorno.

Ambas quedan registradas como deuda técnica #5 y **no** se implementan
aquí (requieren su propio PR revisado con tests, principios #5 y #6).

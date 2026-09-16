# Handoff — 045A Observabilidad backend

## Objetivo

Cerrar la deuda técnica #5 de observabilidad sin introducir infraestructura de pago ni modificar autenticación, OAuth, RBAC o base de datos.

## Implementación

- `Program.cs` usa `JsonConsole` nativo de ASP.NET Core con scopes y seguimiento de `TraceId`/`SpanId`/`ParentId`.
- `RequestObservabilityMiddleware` añade `X-Request-ID`, scope estructurado y tiempo total por request.
- El scope contiene únicamente `RequestId`, `TraceId`, patrón de ruta, estado autenticado y claim `sub` cuando existe.
- Las excepciones no controladas producen `500 application/problem+json` con mensaje genérico y correlación, sin filtrar el detalle interno.
- Para evitar fugas accidentales, el middleware no serializa el mensaje arbitrario de la excepción y no captura query strings, headers, body, cookies, tokens, correos ni nombres.
- `ApiObservabilityMetrics` instrumenta requests, respuestas 5xx y duración con `System.Diagnostics.Metrics` (`MeterName = SchoolManager.API`).
- No existe endpoint público `/metrics`; un exporter futuro puede suscribirse al meter sin cambiar la lógica del API.

## Estrategia de alertas

`docs/observabilidad.md` define los umbrales operativos:

- readiness 503 en tres comprobaciones consecutivas;
- tasa 5xx > 5 % durante 5 minutos con al menos 20 requests;
- p95 > 2 s durante 10 minutos;
- fallo de liveness como alerta inmediata.

El diseño sigue siendo agnóstico al proveedor. Render puede consumir logs JSON y health checks sin agregar un servicio SaaS obligatorio.

## Pruebas

`RequestObservabilityMiddlewareTests` cubre:

- propagación de `X-Request-ID`;
- `ProblemDetails` 500;
- ausencia del detalle interno de la excepción en la respuesta.

La suite existente de `HealthReadinessTests` conserva el contrato de `/health` y `/health/ready`.

## Alcance y seguridad

No se modifican:

- Google OAuth;
- Microsoft OAuth;
- `AuthService` ni `/auth/callback`;
- validación JWT;
- Supabase Auth;
- RLS/RPC;
- migraciones o datos productivos.

## Cierre esperado

Antes del merge del PR #106 deben quedar verdes:

1. CI estándar completo;
2. SonarCloud Quality Gate;
3. preview Vercel;
4. pruebas backend incluyendo observabilidad.

Después del merge, la única deuda técnica abierta registrada debe ser #14 / issue #85: prueba de carga controlada del backend.

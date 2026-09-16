# Observabilidad — SchoolManager.API

Este documento define el contrato de observabilidad del backend después del Bloque 045A. El objetivo es poder diagnosticar fallos y degradaciones sin exponer datos sensibles ni requerir infraestructura de pago adicional.

## Health checks

### `GET /health`

Liveness del proceso. Devuelve `200 OK` mientras la API esté viva y no comprueba dependencias.

```json
{ "status": "ok", "service": "SchoolManager.API", "timestamp": "..." }
```

### `GET /health/ready`

Readiness real. Ejecuta `SELECT 1` contra PostgreSQL mediante el `NpgsqlDataSource` registrado.

- `200 OK`: PostgreSQL responde.
- `503 Service Unavailable`: la dependencia crítica no está disponible.

La respuesta nunca incluye host, puerto, usuario, contraseña, connection string ni detalle interno de la excepción.

## Logging estructurado — 045A

`Program.cs` configura el provider nativo `JsonConsole` de ASP.NET Core. Los logs salen por stdout en JSON, por lo que Render puede conservarlos y filtrarlos sin introducir Serilog, un collector o un servicio SaaS adicional.

Cada request pasa por `RequestObservabilityMiddleware` y recibe contexto estructurado:

- `RequestId`: `HttpContext.TraceIdentifier`;
- `TraceId`: identificador W3C de `Activity` cuando existe;
- `Route`: patrón de ruta ASP.NET (`/api/alumnos/{id}`), no la URL cruda;
- `Authenticated`: indica si existe identidad autenticada;
- `UserId`: únicamente el claim `sub` cuando está disponible;
- método HTTP, status code y duración total.

La respuesta incluye `X-Request-ID`. Ese valor es el dato que soporte debe pedir al usuario cuando haya que buscar una solicitud concreta en los logs.

### Datos que no se registran

El middleware no captura ni registra:

- query strings;
- headers;
- cuerpo de request/response;
- JWT/access tokens;
- cookies;
- contraseñas;
- correos o nombres de personas.

Para rutas no reconocidas se usa `<unmatched>` en lugar del path enviado por el cliente, evitando cardinalidad ilimitada y contenido arbitrario en logs.

## Excepciones no controladas

Una excepción inesperada se registra con:

- `RequestId` y `TraceId`;
- tipo de excepción y tipo de excepción interna;
- método y patrón de ruta;
- stack trace.

No se serializa el mensaje arbitrario de la excepción en el log estructurado del middleware, porque podría contener valores operativos sensibles.

Si la respuesta todavía no comenzó, el cliente recibe `500 application/problem+json` con un mensaje genérico y los identificadores de correlación:

```json
{
  "type": "about:blank",
  "title": "Ocurrió un error interno al procesar la solicitud.",
  "status": 500,
  "requestId": "...",
  "traceId": "..."
}
```

No se devuelve stack trace, mensaje de excepción, connection string ni detalle de base de datos.

## Métricas internas

`ApiObservabilityMetrics` usa `System.Diagnostics.Metrics` con meter `SchoolManager.API`. No abre un endpoint público y no requiere exporter para ejecutar la aplicación.

Instrumentos actuales:

| Métrica | Tipo | Uso |
| --- | --- | --- |
| `schoolmanager.api.requests` | Counter | total de requests procesados |
| `schoolmanager.api.server_errors` | Counter | respuestas HTTP 5xx |
| `schoolmanager.api.request.duration` | Histogram | duración de requests en ms |

Tags permitidos: método HTTP, patrón de ruta y status code. No se incluyen IDs de usuario, correos, query strings ni otros valores de alta cardinalidad.

Un exporter OpenTelemetry/OTLP o Prometheus puede añadirse en el futuro sin cambiar la instrumentación de negocio. No es requisito para el cierre de 045A.

## Estrategia de alertas

La estrategia operativa queda definida para que pueda implementarse con el monitor disponible en el entorno, sin acoplar el código a un proveedor específico:

1. **Disponibilidad crítica:** alertar si `/health/ready` devuelve `503` en tres comprobaciones consecutivas.
2. **Errores 5xx:** alertar si los 5xx superan 5 % durante 5 minutos con un mínimo de 20 requests.
3. **Latencia:** advertir si el p95 de `schoolmanager.api.request.duration` supera 2 s durante 10 minutos.
4. **Liveness:** alertar inmediatamente cuando `/health` deje de responder.

Mientras no exista un collector de métricas dedicado, Render conserva los logs JSON y los health checks siguen siendo la fuente mínima de disponibilidad. Incorporar un exporter o una plataforma externa será una decisión operativa futura, no una dependencia obligatoria del backend.

## Flujo de diagnóstico

Cuando un usuario reporte un error:

1. pedir el valor `X-Request-ID` si está disponible;
2. buscar ese `RequestId` en los logs del backend;
3. usar el `TraceId` asociado para correlacionar eventos de la misma solicitud;
4. revisar `Route`, `StatusCode`, duración y tipo de excepción;
5. nunca solicitar ni registrar JWT, password o cookies para diagnosticar el caso.

## Pruebas

- `HealthReadinessTests.cs`: liveness/readiness y ausencia de secretos.
- `RequestObservabilityMiddlewareTests.cs`: encabezado `X-Request-ID` y `ProblemDetails` 500 sin filtrar detalle interno.
- CI compila el backend y ejecuta la suite de integración completa antes de permitir merge.

## Alcance

045A no modifica autenticación local, Google OAuth, Microsoft OAuth, JWT, RLS/RPC, migraciones ni datos de producción. Solo incorpora observabilidad transversal al pipeline HTTP del backend.

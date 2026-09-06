# Observabilidad — health checks y estado actual

Estado de healthcheck, logging, errores y monitoreo del backend
(`SchoolManager.API`). Este documento describe el **contrato** de los
endpoints de estado (implementado en PR A, deuda #5) y la **auditoría** de
lo que existe y lo que aún falta en observabilidad. Principios ISW2 #5 y
#11.

---

## Contrato de health checks (liveness y readiness)

Ambos endpoints son **anónimos** (no requieren autenticación) para que un
orquestador, balanceador de carga o servicio de monitoreo pueda sondearlos
sin credenciales. **No exponen secretos ni connection strings** en ninguna
respuesta.

### `GET /health` — liveness

Indica que el **proceso** de la API está vivo y respondiendo HTTP.

- **200 OK** siempre que el proceso esté en pie. No comprueba dependencias.
- Cuerpo:
  ```json
  { "status": "ok", "service": "SchoolManager.API", "timestamp": "..." }
  ```
- Uso: sondear periódicamente para detectar un proceso colgado o caído.
  No debe usarse para decidir enrutado de tráfico porque no valida la base.

### `GET /health/ready` — readiness

Indica si la API está **lista para recibir tráfico**, es decir, si su
dependencia crítica (PostgreSQL) está disponible.

- Comprueba conectividad real con PostgreSQL ejecutando `SELECT 1` a través
  del `NpgsqlDataSource` singleton registrado en DI (timeout 3 s).
- **200 OK** cuando la base responde:
  ```json
  { "status": "ready", "service": "SchoolManager.API", "database": "ok",
    "timestamp": "..." }
  ```
- **503 Service Unavailable** cuando la base no responde o falla la conexión
  (se captura `NpgsqlException`):
  ```json
  { "status": "not_ready", "service": "SchoolManager.API",
    "database": "unavailable", "timestamp": "..." }
  ```
- Uso: el orquestador debe **retirar la instancia del pool / no enviarle
  tráfico** mientras devuelva 503, y reintentar cuando vuelva a 200.

### Reglas de la respuesta

1. Códigos: **200** = listo; **503** = dependencia crítica no disponible.
2. Nunca incluir connection strings, host, puerto, usuario, contraseña ni
   detalle de la excepción interna.
3. Mantener `GET /health` como liveness (no mezclar responsabilidades); el
   readiness vive en una ruta separada.

### Implementación de referencia

`backend/SchoolManager.API/Program.cs` — endpoints de minimal API:
`app.MapGet("/health", ...)` (liveness) y `app.MapGet("/health/ready", ...)`
(readiness; `SELECT 1` vía `NpgsqlDataSource`).

### Cobertura de tests

`tests/SchoolManager.API.IntegrationTests/HealthReadinessTests.cs`:

- liveness devuelve 200 con `status=ok`;
- liveness sigue en 200 aunque la DB esté caída (no depende de la base);
- readiness devuelve 200 cuando PostgreSQL responde;
- readiness devuelve 503 cuando PostgreSQL está caído (datasource a puerto
  cerrado);
- ninguna respuesta expone connection string ni secretos.

---

## Qué existe (auditoría)

### Healthcheck
- `GET /health` (liveness del proceso, ver contrato arriba).
- `GET /health/ready` (readiness con chequeo real de PostgreSQL, ver
  contrato arriba) — añadido en PR A.

### Logging backend
- Solo el logging por consola por defecto de ASP.NET Core
  (`appsettings.json`: `Default: Information`, `Microsoft.AspNetCore:
  Warning`). Sin sink estructurado (serilog/OpenTelemetry), sin niveles
  configurables por entorno productivo más allá del default.
- `appsettings.json` no contiene secretos (los `Jwt:Issuer/Audience` y CORS
  son valores no sensibles; la cadena de conexión va por variable de entorno
  `ConnectionStrings__PostgreSQL`).

### Errores
- Manejo de errores por middleware/`ProblemDetails` por defecto de ASP.NET
  Core en controllers; excepciones no capturadas caen al log de consola. No
  hay un middleware global que registre errores con contexto (usuario, ruta,
  trace id) de forma estructurada.

### Monitoreo
- Sin métricas de aplicación, sin traces distribuidos, sin alertas. La
  infraestructura externa (Render/Vercel) ofrece dashboards básicos, pero no
  hay telemetría propia del API.

## Qué falta aún (deuda futura)

| Carencia | Riesgo | Añadir cuando |
| --- | --- | --- |
| Logging estructurado (serilog/OpenTelemetry) + niveles por entorno | diagnóstico lento de errores en prod; logs difíciles de filtrar | con un primer panel/monitor real |
| Registro de errores con contexto (request id, usuario, path) | no se puede correlacionar un fallo con una sesión | idem |
| Métricas + alertas | degradaciones pasan desapercibidas hasta el usuario | post-020, cuando exista superficie con dinero |

## Recomendación mínima restante (fuera de este PR)

1. Adoptar logging estructurado mínimo (p. ej. serilog a consola en JSON)
   sin añadir infraestructura; es bajo riesgo y alto retorno.
2. Métricas de aplicación y alertas cuando exista superficie con flujo de
   dinero en producción.

Quedan registradas como deuda técnica #5 (parte no cubierta por el
readiness) y no se implementan en PR A.

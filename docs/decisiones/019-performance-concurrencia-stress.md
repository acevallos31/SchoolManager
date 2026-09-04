# Decisiones técnicas — Bloque PERF-01 (019-performance-concurrencia-stress)

Rama `perf/escalabilidad-concurrencia-stress`. Decisiones registradas con su
evidencia para cada fase que exige una decisión explícita.

---

## PERF-03 — Índices: no se agrega ningún índice nuevo

**Evidencia.** Benchmark reproducible en Postgres 16 local (`perf/benchmark`,
2000 alumnos / 5600 matrículas / 40 secciones) con `EXPLAIN (ANALYZE, BUFFERS)`:

| Consulta | Plan | Execution (ms) |
|----------|------|----------------|
| Q1 listar alumnos (join personas) | Hash Join sobre Seq Scan | ~1.4 ms |
| Q2 listar matrículas (join completo) | cadena de Hash Join | ~8.7 ms |
| Q3 filtro estado+ciclo+institución | **Index Only Scan** `ix_matriculas_institucion_ciclo_estado` | ~1.75 ms |
| Q4 filtro por alumno | **Bitmap Index** `uq_matriculas_alumno_ciclo` | ~0.2 ms |
| Q5 catálogo grados | Seq Scan | ~0.08 ms |

**Conclusión.** Las consultas filtradas (el patrón real de producción) ya usan
los índices existentes `ix_matriculas_institucion_ciclo_estado` y
`uq_matriculas_alumno_ciclo`. Ningún índice nuevo mejora una consulta real con
el volumen objetivo; se descarta crear índices especulativos (regla PERF-03).
**Q2 es el punto de dolor** (lista completa con 5 joins) y se resuelve con
paginación server-side (PERF-02), no con índices. Ver `perf/benchmark/output/`.

---

## PERF-04 — Caché: no se implementa caché en este bloque

**Decisión:** descartar caché por ahora, sin Redis.

**Razón (regla PERF-04):** no añadir Redis si no hay beneficio medido; documentar
decisión. El cuello de botella medido NO está en los catálogos de baja mutación
(Q5 = 0.08 ms): grados/jornadas/ciclos/períodos son tablas diminutas que no
necesitan caché. El problema real es **descargar listados completos** (Q2), ya
corregido con paginación en el backend.

Además:
- El frontend de alumnos lee por Supabase; no hay un gateway .NET donde
  cachear de forma transparente sin cambiar la arquitectura de lectura.
- Introducir caché ahora añadiría complejidad (TTL + invalidación + coherencia
  multi-instancia) sin beneficio medido sobre una consulta lenta.

**Futuro.** Si se necesita, el candidato correcto es una abstracción de caché
**distribuida** (compatible con múltiples instancias) para **catálogos de baja
mutación únicamente** (grados, jornadas, instituciones, ciclos, períodos), con
TTL explícito e invalidación en escritura. **Nunca** cachear como autoridad:
cupo disponible, matrícula existente, estado vigente, saldo, pago, cargo,
mensualidad pendiente (regla obligatoria de integridad).

---

## PERF-05 — Idempotencia: defensa por integridad en la DB + decisión documentada de API key

**Decisión:** conservar la restricción única `uq_matriculas_alumno_ciclo`
(alumno + ciclo) como **defensa de integridad atómica** bajo concurrencia
(validado por los tests de PERF-06). **No se agrega todavía una
`Idempotency-Key` explícita en la API**; se documenta el diseño para un PR
futuro.

**Razón:**
- La restricción única garantiza que un doble clic / reintento / request
  duplicado del mismo (alumno, ciclo) **nunca crea duplicados**: el segundo
  request obtiene 409 Conflict controlado. Verificado por los tests
  `Cupo_uno_con_muchas...` y `Mismo_alumno_ciclo_concurrente`.
- La defensa vive en la **DB / transacción**, no en memoria local del app
  server (cumple la regla PERF-05).
- El caso que una `Idempotency-Key` resolvería mejor es el **reintento de red
  sobre la misma intención** (p. ej. el cliente no sabe si su POST llegó). Hoy
  el segundo intento devuelve 409 (recurso ya existe), que es seguro aunque no
  distingue "reintento vs duplicado intencional".

**Diseño futuro (bloque 021+)** para una `Idempotency-Key` correcta:
1. Migración nueva (con validation/rollback según convención):
   `idempotencia_operaciones(key_hash, request_hash, resultado, creado_en,
   vence_en)` con PK sobre `key_hash`.
2. En la misma transacción que crea la matrícula, insertar la key con
   `ON CONFLICT DO NOTHING`; si ya existe, devolver el resultado persistido.
   Nunca usar memoria local como única garantía.
No se escribe en Supabase remoto.

---

## PERF-08 — Connection pooling (NpgsqlDataSource)

**Revisión del estado actual (`Program.cs`):**
- `NpgsqlDataSource.Create(connectionString)` se registra como **singleton**
  → un único DataSource compartido (patrón correcto).
- El pooling de Npgsql es **on por defecto** (`Pooling=true`, `MaxPoolSize`
  por defecto 100).
- Cada controller abre una conexión `using` (`AbrirComoUsuarioAsync`) y la
  libera al salir del método → **no** hay patrón "N usuarios = N conexiones
  permanentes"; las conexiones vuelven al pool en cada request.

**Parámetros recomendados para producción (no se cambia nada aquí; requiere
evidencia con carga y autorización):**

```
Pooling=true;
Maximum Pool Size=50..100;
Minimum Pool Size=0;
Connection Idle Lifetime=300;
Connection Pruning=1;
Connection Timeout=15;
Command Timeout=30;
```

**Recomendación:** mantener el singleton de `NpgsqlDataSource` y el pooling on
por defecto; monitorear `pg_stat_activity` durante los escalones k6
(`perf/k6/monitor_db.sh`) para confirmar que las conexiones **no** crecen con
los usuarios. No cambiar producción sin evidencia y autorización.

> Nota: el transporte de alumnos del frontend es via Supabase (PostgREST), que
> debe monitorearse por conexiones del lado cliente; la API .NET (matrículas)
> ya queda acotada al pool singleton correcto.
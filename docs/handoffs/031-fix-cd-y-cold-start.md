# 031 — Cierre post-030: change detection y latencia inicial de autenticación

Fecha: 2026-09-08

## Estado

Este handoff registra dos incidencias detectadas después del Bloque 030 y su estado final de cierre/diagnóstico.

## 1. UI atascada en «Cargando…» — RESUELTO

### Síntoma observado en producción

- `GET /api/alumnos` respondía `200 OK` con datos correctos.
- El estado interno terminaba la carga, pero la vista permanecía mostrando `Cargando alumnos...`.
- Un clic en un control de la pantalla, por ejemplo `+ Nuevo Alumno`, hacía aparecer de inmediato los datos que ya estaban en memoria.
- El mismo patrón se observó en Matrículas.

### Causa

Las páginas mutaban propiedades ordinarias después de operaciones `async/await` sin una notificación reactiva fiable al scheduler de Angular. El evento de usuario provocaba una nueva pasada de change detection y «descongelaba» la vista.

El intento anterior de restaurar CD por zona con `provideZoneChangeDetection()` (PR #55) no resolvió de forma suficiente el comportamiento real de producción.

### Solución final

PR #58 migró el estado `cargando` de Alumnos y Matrículas de `boolean` a `signal`:

- `cargando = signal(false)`;
- `cargando.set(true/false)`;
- templates con `@if (cargando())`.

La señal notifica de forma nativa al scheduler y no depende de un clic ni de `detectChanges()` de producción.

Se conservaron temporalmente `provideZoneChangeDetection()` y `zone.js`; retirar o migrar globalmente el resto de la aplicación a zoneless queda fuera de este fix pequeño.

### Evidencia

- Alumnos: 22/22 tests.
- Matrículas: 16/16 tests.
- Suite frontend completa: 294/294.
- Build production: OK.
- `git diff --check`: limpio.
- CI/SonarCloud: verde.
- Validación manual del preview: `/alumnos` dejó de quedarse en `Cargando...`; los datos aparecen sin interacción adicional. También se verificó creación de alumno correctamente desde la UI.
- PR #58 mergeado a `main` como `ec077ce700c291187611c668c263682409729244`.

## 2. Login inicial lento — DIAGNOSTICADO, no bloqueante

Se instrumentó temporalmente el flujo de autenticación en el PR #57 para separar los tiempos de Supabase Auth y `GET /auth/me`.

Medición manual contra el mismo backend Render:

| Etapa | Primer login | Segundo login inmediato |
| --- | ---: | ---: |
| `supabase.signInWithPassword` | 1081.8 ms | 488.4 ms |
| `GET /auth/me` | 26937.7 ms | 347.6 ms |

Conclusión: el cuello de botella del primer login es compatible con un cold start del backend en Render. Con la instancia caliente, `/auth/me` responde en ~0.35 s, por lo que no hay evidencia actual de que la consulta de perfil sea intrínsecamente lenta.

Decisión actual: no modificar arquitectura ni `/auth/me` todavía. Estrategias futuras posibles: aceptar el cold start o evaluar un mecanismo de keep-alive/plan que mantenga el servicio caliente, respetando límites y condiciones del proveedor.

La instrumentación del PR #57 es temporal y no debe considerarse funcionalidad permanente.

## 3. Próximo ajuste funcional: código interno del alumno

Decisión preliminar para el siguiente PR, fuera del alcance del fix #58:

- El código interno dejará de ser ingresado manualmente por el usuario.
- Formato inicial acordado: `AAAA-NNNNNN`.
- `AAAA` = año de alta/ingreso del alumno.
- `NNNNNN` = consecutivo de seis dígitos.
- La generación debe ser segura ante concurrencia y ocurrir en backend/DB, no en Angular.
- La política deberá poder configurarse desde el apartado Configuración en una evolución posterior.
- El código debe permanecer estable una vez asignado y seguir siendo utilizable en búsquedas.

Ejemplo: `2026-000001`.

## Pendientes relacionados

- Validar `/matriculas` manualmente en preview/producción con el mismo criterio de carga sin clic, si todavía no se ha hecho en esa ruta.
- Definir el PR específico para generación/configuración del código interno.
- Decidir más adelante la estrategia operativa para el cold start de Render.
- No mezclar estos pendientes con E2E staging, RBAC u otras deudas post-030.

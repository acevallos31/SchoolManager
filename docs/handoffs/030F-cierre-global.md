# 030F — Cierre global de deuda #10

Agente: Codex. Fecha: 2026-09-07/08 (America/Tegucigalpa).
Rama destino: `feature/arquitectura-api-030`. PR #53, abierto contra `main`, sin merge.
Base verificada en GitHub: `b096ec5eb27934679453a0cfed2a43c8552192a0`.
Trabajo aislado: `.worktrees/030f`, rama local `feature/cierre-deuda-030f`.
Los archivos preexistentes sin seguimiento `supabase/.temp/` no se leen ni modifican.

## Estado

Bloque 030 cerrado; **deuda #10 RESUELTA en PR #53**, sin merge.
HEAD funcional verificado: `3feb5175ce35a368a887aa55790f515e079ca64c`.
El commit documental que contiene este cierre no modifica código funcional.

## Auditoría de los cinco accesos de Configuración

| Acceso previo | Clasificación | Decisión 030F / endpoint |
|---|---|---|
| `rpc_obtener_contexto_implementacion` | Contexto/infraestructura legítima: modo e institución para usuarios autenticados | También migrado: `GET /api/configuracion/contexto` |
| `rpc_actualizar_multiples_instituciones` | Deuda de negocio: modifica configuración global | `PUT /api/configuracion/modo` |
| `rpc_obtener_configuracion_institucion` | Deuda de negocio: datos del centro y reglas de identificación | `GET /api/configuracion/institucion?institucionId=` |
| `rpc_crear_institucion` | Deuda de negocio: alta del centro y configuración | `POST /api/configuracion/instituciones` |
| `rpc_actualizar_institucion` | Deuda de negocio: edición del centro y configuración | `PUT /api/configuracion/instituciones/{id}` |

Ninguna de esas cinco llamadas era un falso positivo. Se migraron las cinco;
no queda una excepción directa para Configuración.

## Contrato y seguridad

- `ConfiguracionController` delega en RPC 012/013; no duplica normalización,
  invariantes, resolución institucional ni reglas SQL en C#.
- Se registran policies para permisos **ya existentes en 012**:
  `configuracion.sistema.editar`, `configuracion.instituciones.ver/editar`.
  No se modifica catálogo, RLS, RPC ni scripts de migración/rollback.
- Contexto: autenticación .NET; la RPC exige además usuario interno válido.
- Lectura de configuración: policy .NET `ver` **o** `editar`, igual al contrato
  de 013. Escrituras: policy `editar` correspondiente. La DB vuelve a comprobar
  el permiso en el ámbito institucional; no se confunden ambas capas.
- Claim `sub` local a la transacción; commit tras RPC; rollback al fallar.
- Respuestas JSON de RPC conservadas. Errores del nuevo contrato incluyen
  `{error, code}` para preservar los `SM00x` que distinguen los callers.
- `ConfiguracionService` conserva firmas, interfaces, validación de respuestas
  y normalización previa; transporte `HttpClient` + interceptor JWT existente.
- `auth.ts` permanece intacto.

## Búsqueda global

Patrones: `SUPABASE_CLIENT`, `.from(`, `.rpc(`, `createClient` en todo el frontend.

- Servicios/páginas productivas de negocio: **0 accesos directos** tras 030F.
- `auth.ts`: 4 líneas coincidentes (import, token DI, fábrica, inyección);
  una llamada `createClient`, dedicada a Supabase Auth, infraestructura aprobada.
- `auth.spec.ts`: 2 líneas de mocks/DI de autenticación, no accesos productivos.
- `responsables.ts` y `configuracion.spec.ts`: una línea `Array.from` cada uno,
  JavaScript nativo, falsos positivos.
- `.rpc(` global: **0**; `.from(` de Supabase: **0**.
- Control preventivo: `python scripts/check-frontend-api-boundary.py`, incorporado
  a CI. Es una búsqueda léxica de fuentes TypeScript, no una prueba dinámica;
  excluye tests y `auth.ts`, reconoce `Array.from` nativo.

## Verificación y seguimiento

- Frontend completo: 25 archivos / 289 tests, LCOV generado.
- Backend build Release: 0 warnings / 0 errores; frontend build producción correcto.
- Suites completas locales: API **156/156**, DB **156/156**, frontend **289/289**.
- Cobertura local final: backend **87,95%**, LCOV frontend **70,32%**; gate de regresión
  frente al baseline versionado correcto.
- El control preventivo se comprobó con cinco patrones prohibidos y `Array.from`
  nativo: rechaza los primeros y acepta el último.
- Estado remoto inicial: run `34183124351`, validación verde, SonarScanner .NET
  ejecutado (C# + TypeScript + Cobertura/LCOV), **Quality Gate rojo**; Vercel verde.
- Se añade reporte CI de condiciones del gate y hallazgos del PR para diagnosticar
  el rojo sin exponer credenciales ni reducir reglas o exclusiones del análisis.
- Primer push 030F: `43571db` (backend/tests), `5f8198f` (frontend/tests),
  `79aa2da` (CI/handoff). Run `34190275592`: validación completa y Vercel verdes;
  QG rojo: cobertura New Code 72,6% y duplicación 12,6%.
- Correcciones posteriores: fechas obligatorias en DTO de ciclos, pruebas de
  edición de períodos, respuestas vacías y errores HTTP de los servicios migrados.
  Se centraliza el ámbito transaccional de 25 operaciones académicas sin duplicar
  reglas SQL/RPC. El validador 024 unifica su inventario; cinco tests DB comprueban
  sus diagnósticos (catálogo, grants y registro de migración). Scripts principales
  023/024 y rollbacks intactos; ninguna regla/exclusión/QG debilitada.
- Checkpoint `f2e07eb`, run `34192058779`: CI y Vercel verdes; QG todavía rojo
  por duplicación 4,4%, cobertura New Code ya correcta (86,9%).
- `3feb517` comparte la ejecución parametrizada de cinco cambios de estado
  (grados/jornadas y reactivación de sección). Suite API completa reejecutada:
  **156/156**; SQL, policies y parámetros conservados.
- Verificación remota del HEAD funcional: [run 34192575139](https://github.com/acevallos31/SchoolManager/actions/runs/34192575139),
  validación y Sonar **success**; Vercel **success**. Despliegue producción omitido.
  SonarScanner for .NET 11.3.0: begin → build con analyzers C# → end;
  análisis TypeScript (94 fuentes), Cobertura backend y LCOV frontend importados.
  [Quality Gate PR #53](https://sonarcloud.io/dashboard?id=SchoolManager&pullRequest=53):
  **OK**, New Code **86,5%** cobertura / **2,7%** duplicación;
  fiabilidad, seguridad y mantenibilidad **A**, hotspots revisados **100%**.
  Reporte de incidencias abiertas del PR vacío.
- `git diff --check origin/main...HEAD` y búsqueda global frontend correctos.
  `auth.ts` sin diff contra `b096ec5`.

## Commits 030F

- `43571db`: Configuración backend, policies existentes y tests con DB real.
- `5f8198f`: cinco llamadas de Configuración migradas a HTTP .NET.
- `79aa2da`: control CI de frontera API y diagnóstico Sonar.
- `92e0f89`: fechas obligatorias y cobertura de edición de períodos.
- `b04882b`: errores y respuestas vacías de ciclos/estructura frontend.
- `6978976`: errores y respuestas restantes de alumnos frontend.
- `a57574c`: ámbito transaccional común de las operaciones académicas.
- `f2e07eb`: validador 024 sin duplicación y cinco tests de diagnóstico.
- `3feb517`: ejecución común de cambios de estado del catálogo académico.
- Commit documental posterior: actualiza este handoff, AI_CONTEXT y deuda #10.

## Reproducción local

```text
dotnet build backend/SchoolManager.API/SchoolManager.API.csproj -c Release
dotnet test tests/SchoolManager.API.IntegrationTests/SchoolManager.API.IntegrationTests.csproj -c Release --collect:"XPlat Code Coverage" --settings tests/SchoolManager.API.IntegrationTests/coverlet.runsettings
dotnet test tests/SchoolManager.Database.IntegrationTests/SchoolManager.Database.IntegrationTests.csproj -c Release
# Desde frontend/schoolmanager-frontend, Node 22.22.3 / npm 11.13.0:
npm test -- --watch=false
npm run build
# Desde raíz:
python scripts/check-frontend-api-boundary.py
git grep -n -E 'SUPABASE_CLIENT|\.from\(|\.rpc\(|createClient' -- frontend
git diff --check
```

Docker obligatorio para las dos suites de integración. En esta estación se
usaron Node/npm declarados mediante `npm exec --package=node@22.22.3 --package=npm@11.13.0`.
CI vuelve a ejecutar las suites y builds sobre cada HEAD publicado.

## Riesgos residuales

- Las migraciones aditivas 023/024 de 030D/E se aplican manualmente después de
  revisión y CI. Esta sesión no aplica migraciones en Supabase ni producción.
- E2E autenticado contra staging sigue pendiente del entorno documentado en 029.
- El selector global multiinstitución y restricciones de contexto de RPC previas
  permanecen fuera del alcance; no se altera el dominio para eludirlas.

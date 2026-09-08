# 030F — Cierre global de deuda #10

Agente: Codex. Fecha: 2026-09-07/08 (America/Tegucigalpa).
Rama destino: `feature/arquitectura-api-030`. PR #53, abierto contra `main`, sin merge.
Base verificada en GitHub: `b096ec5eb27934679453a0cfed2a43c8552192a0`.
Trabajo aislado: `.worktrees/030f`, rama local `feature/cierre-deuda-030f`.
Los archivos preexistentes sin seguimiento `supabase/.temp/` no se leen ni modifican.

## Estado

Migración implementada; cierre pendiente de suites completas y Quality Gate del HEAD final.
No declarar deuda #10 resuelta hasta completar esas verificaciones.

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
  No se modifica catálogo, RLS, RPC ni migraciones.
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

- Frontend completo: 25 archivos / 229 tests, LCOV generado.
- Backend build inicial: 0 warnings / 0 errores.
- Suites API/DB completas y builds finales: en ejecución.
- Estado remoto inicial: run `34183124351`, validación verde, SonarScanner .NET
  ejecutado (C# + TypeScript + Cobertura/LCOV), **Quality Gate rojo**; Vercel verde.
- Se añade reporte CI de condiciones del gate y hallazgos del PR para diagnosticar
  el rojo sin exponer credenciales ni reducir reglas o exclusiones del análisis.

## Riesgos residuales

- Las migraciones aditivas 023/024 de 030D/E se aplican manualmente después de
  revisión y CI. Esta sesión no aplica migraciones en Supabase ni producción.
- E2E autenticado contra staging sigue pendiente del entorno documentado en 029.
- El selector global multiinstitución y restricciones de contexto de RPC previas
  permanecen fuera del alcance; no se altera el dominio para eludirlas.

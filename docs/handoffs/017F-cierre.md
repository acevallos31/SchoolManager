# HANDOFF — 017F Integración / Supabase / Preview

## Agente, fecha y rama

- Agente: Hermes Agent (cron 2026-09-04).
- Fecha: 2026-09-04 11:57 -06:00 (UTC-6).
- Rama: `feature/matriculas-fase-1c-cierre` desde `main` (`2c5f866`).

## Objetivo

Verificar la coherencia del contrato de Matrículas entre frontend, API .NET y PostgreSQL/Supabase, sin aplicar migraciones, sin tocar producción y sin escribir datos reales.

## Contrato frontend ↔ API .NET (coherente)

- `matriculas.service.ts` consume `/api/matriculas` (GET listar / POST crear / PUT cambiar estado), con mapeo de errores 400/403/404/409.
- Estados de matrícula tipados: `pendiente | activa | finalizada | retirada | anulada | trasladada`. Sin `cancelada` ni `pagada`.
- Transiciones UI alineadas al backend: `pendiente -> activa|anulada`; `activa -> finalizada|retirada|anulada|trasladada`; terminales sin nuevas transiciones.
- Motivo obligatorio para retirada/anulada/trasladada.

## Validaciones ejecutadas

- `dotnet build backend/SchoolManager.API/SchoolManager.API.csproj -c Release`: OK, sin errores ni warnings nuevos.
- `dotnet test SchoolManager.API.IntegrationTests -c Release`: 28 tests OK.
- `dotnet test SchoolManager.Database.IntegrationTests -c Release` (Testcontainers/Docker): 80 tests OK.
- `npx ng test --watch=false`: 17 archivos / 71 tests OK.
- `npm run build`: OK (solo warnings preexistentes canvg/jspdf/commonjs, no nuevos).
- `git diff --check`: OK.

## Auditoría Supabase read-only

Esta sesión no dispuso de credenciales de auditor de Supabase ni de una conexión remota segura a la base de datos. Por tanto, la verificación del lado remoto (tablas, constraints, estados reales, `rpc_matricular_alumno`, `rpc_cambiar_estado_matricula`, permisos/RLS, aislamiento por institución, cupo, matrícula única alumno+ciclo, historial de estados) queda **pendiente** y este 017F se marca **PARCIAL** en esa parte. No se intentó acceder a Supabase con credenciales no disponibles al vuelo (prohibido por AGENTS.md: no inventar credenciales, no leer secretos si no son necesarios).

No se detectó divergencia concreta desde el contrato de código; no se creó ninguna migración nueva.

## E2E / contrato real

No existe un entorno preview/staging desechable con acceso seguro ni datos de prueba disponibles en esta sesión. No se inventaron entornos ni credenciales. La validación E2E de escritura real (Testcontainers con flujo completo o preview desechable) queda **PENDIENTE** como siguiente paso, preferiblemente con datos de prueba controlados.

## Arquitectura/calidad

No se introdujeron capas nuevas, CQRS, MediatR, microservicios, Generic Repository ni UnitOfWork. ACID/DB siguen siendo autoridad de matrícula; no se trasladó lógica transaccional al frontend.

## Pendiente 017F

- Auditoría remota read-only de Supabase (tablas/constraints/RPC/RLS/aislamiento/cupo/unicidad/historial) cuando haya credenciales de auditor.
- Verificación CI/Sonar/Vercel del HEAD del PR de cierre.
- E2E real de escritura con datos de prueba controlados.
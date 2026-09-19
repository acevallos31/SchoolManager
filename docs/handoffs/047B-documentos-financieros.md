# Handoff 047B — Documentos financieros (estado de cuenta del alumno)

## Estado
**COMPLETADO y publicado** (backend compila 0/0, frontend build OK, 439 tests front pasan, git diff --check limpio).
Rama: `feature/047b-documentos-financieros` · Worktree: `/tmp/sm-047b`.

## Alcance (solo 047B)
- Estado de cuenta del alumno (resumen financiero + cargos + histórico de pagos).
- Impresión y descarga PDF desde la página de Cargos.
- Integración mínima en la página de Cargos (botones Imprimir / Descargar PDF).
- **Fuera de alcance** (no tocado): Demo 049, OAuth, RBAC, migraciones Supabase, lógica de pagos, invariantes de 021.

## Arquitectura
El estado de cuenta se compone **en una única transacción ACID** en la capa de aplicación del backend, espejando el patrón de 047A (`DocumentoReciboService`). El frontend solo presenta/imprime/descarga el DTO autoritativo; **no recalcula saldos, cargos, aplicaciones ni totales**.

### Backend (agrega a `/api/estado-cuenta`)
- `DTOs/EstadoCuentaDto.cs` — DTO compuesto: institución + alumno + resumen + cargos + pagos.
- `Services/IEstadoCuentaService.cs` — contrato de aplicación.
- `Services/EstadoCuentaService.cs` — composición autoritativa (Npgsql): abre conexión como usuario del claim (`set_config('request.jwt.claim.sub')`), dentro de una transacción lee:
  - `rpc_resumen_financiero_alumno` → `ResumenFinancieroDto` (8 columnas)
  - `rpc_listar_cargos_alumno` → `CargoDto` (17 columnas)
  - `rpc_listar_pagos_alumno` → `PagoDto` (15 columnas)
  - identidad de alumno e institución.
  - Mapeos de columnas **copiados exactamente** de `CargosController.cs` / `PagosController.cs` (021) — no se alteran invariantes; el servicio es solo lectura.
- `Controllers/EstadoCuentaController.cs` — `GET /api/estado-cuenta/alumno/{alumnoId:guid}`.
- `Program.cs` — registro DI `AddScoped<IEstadoCuentaService, EstadoCuentaService>()`.

### Permisos
- `GET estado de cuenta` → `[Authorize(Policy = Permisos.Cargos.Ver)]` (coherente con la pantalla de Cargos).

### Frontend (solo presentación)
- `core/services/estado-cuenta.service.ts` — servicio Angular que consume el endpoint; tipos `EstadoCuenta`, `EstadoCuentaInstitucion`, `EstadoCuentaAlumno`, error tipado.
- `core/documents/estado-cuenta.documento.ts` — `EstadoCuentaDocumento extends DocumentoImprimible`; dibuja PDF vía `LienzoPdf` y expone HTML autocontenido (escapado). Espejo de `recibo-pago.documento.ts`.
- `core/documents/estado-cuenta.documento.spec.ts` — spec con Stub de `LienzoPdf` (DIP).
- `pages/cargos/cargos.ts` / `cargos.html` — propiedades `generandoDocumento`, métodos `imprimirEstadoCuenta()` / `descargarEstadoCuentaPdf()` reusando `ImpresionService`, botones en el header (solo visibles con alumno seleccionado).

## Pruebas ejecutadas
- Backend: `dotnet build -c Debug` → **Build succeeded, 0 warnings / 0 errors**. (No existe proyecto de tests backend; único `SchoolManager.API.csproj`.)
- Frontend build: `ng build --configuration development` → **OK** (incluye chunk `cargos`).
- Tests frontend: `ng test --watch=false` → **43 archivos / 439 tests passed**.
- Spec 047B aislado: **3/3 passed**.
- `git diff --check` → limpio.

## Commit / PR
- Commits pequeños por capa (backend, frontend, docs).
- Push de `feature/047b-documentos-financieros`; PR contra `main` **sin merge**.

## Pendientes / deuda
- (Según resultado de la publicación) revisar comentarios de revisores y fusionar cuando se autorice.
- Validación E2E en entorno con datos reales (no ejecutada; sin migraciones por restricción del usuario).
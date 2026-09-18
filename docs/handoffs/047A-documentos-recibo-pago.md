# 047A — Documentos: recibo de pago (backend autoritativo + PDF)

Rama: `feature/047a-documentos-recibo-pdf` · Fecha: 2026-09-17 · Prompt: `docs/agent-prompts/047-smart-night-8h-documentos-deuda-tecnica.md`

## Objetivo

Emitir el recibo de pago desde el backend (fuente autoritativa) y consumirlo en Angular
solo como presentación (imprimir / descargar PDF), sin lógica financiera en el cliente.
Corregir el acoplamiento directo de `DocumentoImprimible` a jsPDF aplicando DIP.

## Decisión arquitectónica (backend)

**Servicio de aplicación que compone las RPC existentes en una sola transacción**, sin
migración nueva. Se demostró que `rpc_obtener_pago` + `rpc_obtener_aplicaciones_pago` +
las lecturas de identidad (alumno e institución) son suficientes; no se requirió crear
ninguna RPC ni migración.

- `IDocumentoReciboService` / `DocumentoReciboService` (`backend/SchoolManager.API/Services/`):
  compone cabecera del pago, aplicaciones, alumno e institución dentro de una única
  transacción (`EnTransaccionComoUsuarioAsync`) — snapshot consistente (ACID). Toda la
  composición/validación es autoritativa y server-side.
- `PagosController`: endpoint **delgado** `GET /api/pagos/{pagoId}/recibo`. Permisos
  server-side (`academico.pagos.ver` vía `ApiControllerBase`), traducción de errores
  SQL estándar (42501→403, P0002→404, etc.).
- `Program.cs`: registro `AddScoped<IDocumentoReciboService, DocumentoReciboService>()`.
- `DTOs/ReciboPagoDto.cs`: DTO completo (pago, institución, alumno, detalle de
  aplicaciones). Es la única fuente de datos del recibo; Angular no recalcula nada.

No se creó migración nueva (la 044 sigue siendo la última).

## Decisión arquitectónica (frontend, DIP)

Se invirtió la dependencia de los documentos respecto a jsPDF:

- `core/documents/lienzo-pdf.ts` — **puerto** `LienzoPdf`: superficie mínima de dibujo PDF
  que necesitan los documentos. Totalmente desacoplado de jsPDF.
- `core/documents/jspdf-lienzo-adapter.ts` — **adaptador** `JsPdfLienzoAdapter`: único
  archivo de la aplicación que importa `jspdf`. Construye el motor internamente.
- `DocumentoImprimible.renderizarPdf(lienzo: LienzoPdf)` — ya no menciona jsPDF.
- `ImpresionService` solicita el adaptador concreto y lo entrega al documento; tampoco
  conoce la librería.

Verificación: `grep "from 'jspdf'"` → solo coincide `jspdf-lienzo-adapter.ts`.

## Entregables frontend

- `core/services/pagos.service.ts` — tipos `ReciboPago`, `ReciboInstitucion`,
  `ReciboAlumno`, `ReciboDetalle` y método `obtenerRecibo(pagoId)` (solo GET al endpoint).
- `core/documents/recibo-pago.documento.ts` — `ReciboPagoDocumento` (extends
  `DocumentoImprimible`): **solo presentación** — cabecera de institución, datos del pago,
  alumno, tabla de aplicaciones, total y estado (ANULADO + motivo). No calcula saldos ni
  totales; usa los valores del DTO. Genera PDF (vía `LienzoPdf`) y HTML imprimible.
- `pages/pagos/pagos.ts` + `pagos.html` — botones **Imprimir** y **Descargar PDF** por
  pago; métodos `imprimirRecibo(p)` / `descargarReciboPdf(p)` que piden el DTO y delegan
  en `ImpresionService`. Guardan el permiso de ver antes de llamar.

## Pruebas (todas verdes)

- Backend API: **247/247** (`dotnet test tests/SchoolManager.API.IntegrationTests`) —
  incluye `Recibo_incluye_cabecera_detalle_alumno_e_institucion` (recibo 200 con
  cabecera/detalle, 404 pago inexistente, 403 sin permiso).
- Backend DB: **220/220** (`dotnet test tests/SchoolManager.Database.IntegrationTests`).
- Frontend: **406/406** en 38 archivos (`npx ng test --watch=false`), con cobertura v8.
  Nuevos: `pagos.service.spec.ts`, `recibo-pago.documento.spec.ts` (usa un stub del
  puerto `LienzoPdf`, prueba DIP sin jsPDF) y casos en `pagos.spec.ts` (mock de
  `ImpresionService`).
- `npx ng build` OK.

## Pendiente / siguiente

- Deuda **#109** (auditoría de RPC `SECURITY DEFINER`) en grupos pequeños y verificables —
  no iniciada; se aborda después de que 047A quede cerrada.
- Fase documental del prompt 047 (plantillas de documentos) no iniciada.

## Restricciones respetadas

Sin merge, sin DDL/migraciones en producción, sin pruebas de carga contra producción.

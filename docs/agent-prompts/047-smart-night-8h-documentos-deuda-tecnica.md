# Hermes Smart — Jornada nocturna ~8h: Bloque 047 + deuda técnica

## Contexto

Repositorio: `acevallos31/SchoolManager`
Rama actual de trabajo: `feature/047a-documentos-recibo-pdf`
Base: `main`

El objetivo de esta jornada es aprovechar aproximadamente 8 horas de trabajo autónomo en modo Smart para avanzar el bloque 047 de documentos/impresión y reducir deuda técnica segura, respetando la arquitectura, SOLID, ACID, seguridad, CI/Sonar y las decisiones ya tomadas en el proyecto.

## Reglas no negociables

1. Toda lógica de negocio debe vivir en backend/DB, no en Angular.
2. Mantener arquitectura por capas y separación de responsabilidades.
3. Respetar SOLID, especialmente SRP y DIP.
4. Mantener garantías ACID en operaciones financieras y de escritura.
5. No confiar en datos calculados por frontend para decisiones financieras o de seguridad.
6. No tocar Google/Microsoft OAuth, `auth.users` ni los flujos de autenticación salvo que sea estrictamente necesario para compilar; si algo exige cambios ahí, DETENERSE y documentar.
7. No ejecutar migraciones en Supabase/producción.
8. No hacer cambios destructivos en producción.
9. No ejecutar pruebas de carga contra producción durante esta sesión.
10. No reducir Quality Gates, cobertura, pruebas, permisos, RLS ni controles de seguridad.
11. No usar `NOSONAR`, exclusiones nuevas ni workarounds para ocultar deuda.
12. No hacer merge a `main`.
13. Se puede abrir PR al final si el trabajo queda coherente y CI está listo para validar.
14. Commits pequeños y temáticos; evitar un mega-commit de 8 horas.
15. Si una tarea requiere una decisión funcional no documentada, documentar la duda y continuar con otra tarea segura.

## Estado conocido antes de empezar

### Identidad / acceso

- PR #116 ya fue fusionado a `main`.
- Existe edición de nombres, apellidos y correo interno de SchoolManager.
- OAuth solo autentica; `personas` es la fuente de verdad de datos personales.
- No importar teléfono/dirección desde Google/Microsoft.
- Invitaciones 046A–046G han avanzado sustancialmente; no reimplementar trabajo ya existente.

### Bloque 047 — documentos

La rama ya contiene una base inicial:

- `backend/SchoolManager.API/DTOs/ReciboPagoDto.cs`
- `frontend/schoolmanager-frontend/src/app/core/documents/documento-imprimible.ts`
- `frontend/schoolmanager-frontend/src/app/core/services/impresion.service.ts`

Antes de continuar, revisar estos tres archivos críticamente.

Decisión arquitectónica:

```text
DB / RPC
   ↓
Servicio de aplicación backend
   ↓
DTO autoritativo del documento
   ↓
Controller delgado y seguro
   ↓
Frontend
   ↓
Documento de presentación
   ↓
Motor/adaptador de impresión/PDF
```

El frontend NO debe reconstruir el recibo combinando múltiples fuentes para decidir qué es verdadero. Debe recibir un DTO autoritativo del backend.

Además, `DocumentoImprimible` no debe depender directamente de `jsPDF`; introducir una abstracción propia o separar el renderer/adaptador para cumplir DIP.

## PRIORIDAD 1 — Terminar 047A: recibo imprimible/PDF con arquitectura correcta

Objetivo funcional mínimo:

- Obtener un recibo de pago autoritativo desde backend.
- Permitir imprimirlo en papel.
- Permitir descargarlo como PDF.
- Dejar la base reutilizable para matrícula, estados de cuenta, constancias y reportes.
- NO implementar envío por email todavía salvo que todo lo anterior quede terminado, probado y con tiempo suficiente.

### Backend

1. Revisar `ReciboPagoDto` y ajustarlo solo si el contrato de negocio lo exige.
2. Crear un servicio de aplicación, por ejemplo `ReciboPagoService` o equivalente.
3. El servicio debe componer:
   - institución: id, nombre, nombre corto, dirección, teléfono, correo, logo;
   - alumno: id, nombre completo, RNE, código interno;
   - pago: número de recibo, fecha, monto, método, referencia, estado;
   - anulación cuando corresponda;
   - detalle de aplicaciones/cargos.
4. El backend debe validar alcance institucional y permiso existente de pagos (`academico.pagos.ver` o política equivalente).
5. Evitar SQL duplicado si existe RPC/consulta segura reutilizable.
6. Si la composición requiere varias lecturas, mantener consistencia de lectura dentro de una misma transacción cuando sea pertinente.
7. Crear endpoint delgado, preferiblemente:
   `GET /api/pagos/{pagoId}/recibo`
8. No generar PDF en el controller.
9. No alterar el estado del pago al consultar/imprimir el recibo.
10. Un pago anulado debe seguir siendo consultable como documento histórico y debe indicarse claramente como ANULADO.

### Frontend

1. Crear contrato TypeScript espejo del DTO de recibo.
2. `PagosService` puede pedir el recibo al endpoint, pero no componer reglas de negocio.
3. Diseñar abstracciones POO reutilizables.
4. Corregir acoplamiento actual entre `DocumentoImprimible` y `jsPDF`.
5. `ImpresionService` debe orquestar salidas, no conocer reglas financieras.
6. Implementar `ReciboPagoDocumento` o equivalente como presentación del DTO.
7. Agregar acciones en Pagos:
   - Imprimir recibo.
   - Descargar PDF.
8. Usar tamaño Letter por defecto para SchoolManager en esta fase, salvo que ya exista estándar documentado distinto.
9. El PDF y la impresión deben compartir el mismo modelo de datos y una identidad visual coherente.
10. No incluir secretos, IDs internos innecesarios ni información privada no requerida en el recibo.

### Diseño futuro que debe quedar posible

```text
IDocumento / DocumentoImprimible
  ├── ReciboPagoDocumento
  ├── MatriculaDocumento
  ├── EstadoCuentaDocumento
  └── ReporteDocumento

IRepresentadorDocumento / adaptadores
  ├── Pdf
  ├── ImpresionHtml
  └── futuro EmailAttachment
```

No sobrearquitecturar: abstraer solo lo necesario para que el segundo documento no requiera copiar el motor completo.

### Tests 047A

Agregar cobertura real para:

- servicio backend compone correctamente el DTO;
- pago inexistente;
- acceso fuera de institución;
- permiso insuficiente;
- recibo registrado;
- recibo anulado conserva datos y marca anulación;
- detalle suma/representa lo persistido, sin recalcular reglas en frontend;
- frontend solicita endpoint correcto;
- documento normaliza datos opcionales;
- impresión/PDF no mutan DTO;
- cobertura suficiente para Sonar >= Quality Gate actual.

Ejecutar las suites relevantes y, antes de cerrar, la suite completa si el tiempo lo permite.

## PRIORIDAD 2 — Auditoría de deuda técnica pendiente

### Issue #109 — SECURITY DEFINER expuesto a `authenticated`

Esta es la deuda técnica prioritaria de seguridad.

NO hacer revocaciones masivas.

Trabajo esperado:

1. Inventariar las funciones `SECURITY DEFINER` relevantes desde migraciones/baseline.
2. Identificar consumidores reales en:
   - backend .NET;
   - Angular/Supabase directo;
   - tests;
   - scripts/documentación.
3. Clasificarlas:
   - necesita acceso directo Data API;
   - solo API .NET;
   - candidata a `SECURITY INVOKER`;
   - debe mantener DEFINER con grants estrictos;
   - incierta/requiere decisión.
4. Crear documento versionado con inventario y clasificación.
5. Si hay un grupo pequeño de funciones 100% seguro que solo usa API .NET y existe cobertura suficiente, preparar una migración de hardening LOCAL/VERSIONADA y tests, pero:
   - NO aplicarla en producción;
   - NO mezclar muchos módulos en una sola migración;
   - preferir un grupo coherente (por ejemplo financiero) y pequeño.
6. Si la seguridad del cambio no es inequívoca, limitarse a la auditoría/documentación y abrir/subdividir deuda en issues.

### Issue #107 — flujo de identidad

Auditar contra el estado actual. Gran parte del issue es histórico y puede estar parcial o casi totalmente resuelto por 046A–046G y #116.

- NO reimplementar.
- Comparar criterios del issue con main actual.
- Documentar qué está cerrado y qué sigue pendiente realmente (por ejemplo aceptación/claim/aprobación si aplica).
- Proponer actualización del issue o comentario de estado; no cerrarlo si aún existen huecos reales.

### Issue #85 — prueba de carga backend

NO ejecutar pruebas contra producción durante esta sesión.

Sí se puede:

- revisar si el plan sigue vigente;
- preparar scripts/README/checklist para una futura prueba controlada;
- definir métricas y stop conditions;
- dejarlo listo para una ventana explícitamente autorizada.

No usar credenciales personales ni generar carga productiva.

## PRIORIDAD 3 — Deuda técnica localizada detectada durante 047

Mientras se trabaja en 047, se permite corregir deuda únicamente si cumple TODO:

- riesgo bajo;
- alcance pequeño;
- relación directa con código tocado;
- cobertura existente o añadida;
- no requiere migración productiva;
- no rompe contratos públicos;
- CI/Sonar pueden verificarla.

Para deuda transversal no relacionada, documentar/crear issue en vez de ampliar el scope.

## Calidad y seguridad

Antes de considerar terminada cada unidad de trabajo:

- `git diff --check`
- build backend Release
- tests backend relevantes
- tests DB relevantes si se tocó SQL
- frontend tests
- frontend production build
- revisar secretos
- revisar que no se haya introducido acceso directo privilegiado desde Angular
- revisar permisos/scoping multiinstitución
- revisar Sonar/cobertura si hay PR

No arreglar un Quality Gate reduciendo exigencias.

## Estrategia para las ~8 horas

Orientativa, no rígida:

- 0:00–0:45 — diagnóstico, arquitectura y plan detallado.
- 0:45–3:30 — implementación 047A backend + tests.
- 3:30–5:00 — frontend impresión/PDF + tests.
- 5:00–6:00 — integración, build y correcciones.
- 6:00–7:15 — auditoría issue #109 y deuda segura.
- 7:15–7:45 — auditoría #107/#85 y documentación.
- 7:45–8:00 — reporte final, commits limpios, push y PR si corresponde.

Si 047A requiere más tiempo, PRIORIZAR 047A y dejar la deuda técnica únicamente auditada/documentada. Calidad > cantidad.

## Git / PR

- Trabajar sobre `feature/047a-documentos-recibo-pdf` mientras el alcance sea 047A.
- Si se implementa hardening de #109 que sea claramente separable, usar otra rama derivada de `main` o dejarlo en commits claramente separados; no mezclar una migración de seguridad transversal dentro del PR de impresión si dificulta revisión.
- No hacer merge.
- Se permite push.
- Se permite abrir PR(s) con descripción completa y resultados de tests.

## STOP CONDITIONS

Detener la parte afectada y documentar si ocurre cualquiera:

- requiere credencial/secret no disponible;
- requiere DDL en producción;
- requiere tocar OAuth/login funcional;
- requiere decidir una regla financiera no documentada;
- tests revelan una regresión preexistente que no está relacionada;
- la solución segura exige ampliar significativamente el scope;
- se detecta riesgo de exposición de datos entre instituciones;
- se detecta que una RPC supuestamente sin consumidor sí es usada directamente por frontend.

Continuar con otra tarea segura en lugar de forzar una solución.

## Entregable final obligatorio

Crear/actualizar un handoff nocturno con:

1. resumen ejecutivo;
2. horas aproximadas / fases completadas;
3. commits y SHAs;
4. archivos principales modificados;
5. arquitectura final de 047A;
6. endpoints/DTOs/interfaces creados;
7. tests ejecutados y conteos;
8. CI/Sonar si se abrió PR;
9. deuda #109: inventario/clasificación/delta;
10. estado real de #107;
11. preparación realizada para #85;
12. riesgos y decisiones pendientes;
13. migraciones creadas pero NO aplicadas, si existieran;
14. próximos 3 pasos recomendados.

Al terminar, reportar de forma explícita:

- `MERGE: NO`
- `PRODUCTION DB CHANGES: NO`
- `PRODUCTION LOAD TEST: NO`

## Resultado deseado al despertar

Ideal:

- 047A funcional y probado con recibo imprimible + PDF;
- arquitectura reutilizable para otros documentos;
- backend autoritativo y frontend sin lógica de negocio;
- CI/Sonar verde o causa exacta documentada;
- issue #109 auditado y, solo si fue inequívocamente seguro, primer hardening preparado;
- #107 actualizado conceptualmente contra el estado real;
- #85 preparado pero no ejecutado;
- ningún merge ni cambio productivo sin autorización.

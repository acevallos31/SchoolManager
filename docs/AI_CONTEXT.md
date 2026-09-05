# SchoolManager - AI Context

## Arquitectura
- Angular frontend, .NET backend y PostgreSQL/Supabase con JWT Supabase.
- Monolito modular, UUID internos, RLS y RPC para escrituras críticas.
- Evitar CQRS, MediatR, microservicios, Generic Repository y UnitOfWork artificial.
- Permisos RBAC, no checks hardcodeados por rol y no DELETE físico de históricos.

## Contexto institucional
- Modo single resuelve la institución activa; modo multi exige contexto explícito.
- El selector global multiinstitución está pendiente.

## Migraciones
- 007 RBAC base; 008 modelo académico histórico; 009 RLS y RPC; 010 identidad; 011 creación alumno con documento; 012 configuración de implementación; 013 centro educativo; 014 ciclos/períodos; 015 períodos anticipados.
- 016 grados, jornadas y secciones: implementada en el repositorio; aplicación en producción no verificada.
- 017 responsables (gestión RPC): implementada y validada en Postgres 16 desechable; baseline consolidado anexado; en `main`.
- 018 configuración financiera (conceptos y planes de pago): implementada y en `main`.

## Estándares de ingeniería
- Los **12 principios de ingeniería** del proyecto viven en `docs/engineering-principles.md` y son reglas vinculantes citables por número (`#1`…`#12`) en PRs, handoffs y decisiones técnicas.
- La IA no sustituye la revisión: todo output generado por IA se lee y verifica antes de integrarse (#12).

## Módulo Responsables (018)
- Namespace vigente: **`academico.responsables.*`** (usado por RLS 009 y backend `Permisos.cs`); `responsables.responsables.*` (007) es legacy sin uso, conservado.
- MIG017 cierra la brecha: 9 RPC `security definer` (crear con/para persona, editar, inactivar/reactivar, vincular, editar vinculo, desactivar/reactivar vinculo) con invariantes persona+institución único, principal único por alumno, no-cruce de institución, sin DELETE físico. Superficie solo-RPC (sin policies INSERT/UPDATE directas).
- Backend: `ResponsablesController` (API `api/responsables`, paginado `PaginatedResult`) en `feature/responsables-fase-018`. **Cuidado**: los parámetros nullable a `AddWithValue` deben usar `?? DBNull.Value` (pasar `null` rompe Npgsql en runtime con 500 — los tests de API lo cazan; los de DB no pasan por el controller).
- Frontend: página `/responsables` + servicio `responsables.service.ts` (API .NET) + integración Alumno→Responsable (`responsables?alumnoId=...`, panel de vínculos).

## Modelo académico
Institución -> Ciclo -> Período matrícula -> Grado -> Jornada opcional -> Sección -> Matrícula -> Alumno.

- Los períodos pueden ser anticipados, normales o extraordinarios y sus fechas son independientes de las académicas.
- Grados y jornadas son globales por ahora; secciones son por institución/ciclo.
- Una sección con matrículas no cambia ciclo, grado ni jornada.

## Estado frontend
- Módulos navegables: `/configuracion`, `/configuracion/ciclos`, `/configuracion/estructura-academica`, `/configuracion/conceptos-financieros`, `/configuracion/planes-pago`, `/responsables`, `/matriculas` y `/alumnos`.
- Matrículas Fase 1C completa: página `/matriculas` conectada a la API .NET (listar, crear, cambiar estado) con acción "Matricular" desde Alumnos.
- Responsables y Configuración Financiera (conceptos + planes de pago) integrados y en `main`; sus 3 rutas nuevas cargan por lazy (`loadComponent`).
- Detalle de cierre en `docs/handoffs/017D-integracion-alumnos.md`, `017E-tests.md` y `017F-cierre.md`.

## Git y validación
Las reglas operativas de Git, migraciones y validación están en `AGENTS.md`.

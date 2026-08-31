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
- 016 grados, jornadas y secciones: en desarrollo, no aplicada en producción.

## Modelo académico
Institución -> Ciclo -> Período matrícula -> Grado -> Jornada opcional -> Sección -> Matrícula -> Alumno.

- Los períodos pueden ser anticipados, normales o extraordinarios y sus fechas son independientes de las académicas.
- Grados y jornadas son globales por ahora; secciones son por institución/ciclo.
- Una sección con matrículas no cambia ciclo, grado ni jornada.

## Estado frontend
- `/configuracion`, `/configuracion/ciclos` y `/configuracion/estructura-academica`.
- Próximo bloque: Matrículas Fase 1C.

## Git y validación
- Conventional Commits en español. No commit, push ni PR automático.
- Migraciones manuales en Supabase después de revisión y CI.
- Validar con `dotnet build -c Release`, `dotnet test`, `npm test`, `npm run build` y `git diff --check`.
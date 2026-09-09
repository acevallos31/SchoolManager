# SchoolManager - AI Context

## Estado general
- Proyecto web de gestión escolar en Angular + .NET + PostgreSQL/Supabase.
- Arquitectura objetivo: Angular -> API .NET -> PostgreSQL/Supabase/RPC.
- Supabase directo en frontend queda reservado a autenticación (`auth.ts`).
- Deuda #10 de accesos directos de negocio a Supabase: RESUELTA en Bloque 030 / PR #53.
- Producción quedó alineada hasta migración 026 el 2026-09-09.
- Estructura Académica funciona en producción después de aplicar 019→025 y refrescar sesión.
- La rematrícula tras anulación quedó corregida con 026 y validada manualmente en producción.

## Arquitectura
- Angular standalone frontend.
- Backend .NET 10.
- PostgreSQL/Supabase como persistencia.
- Monolito modular; evitar CQRS, MediatR, microservicios, Generic Repository y UnitOfWork artificial.
- UUID para PK/FK internos.
- RLS + RPC para invariantes y escrituras críticas.
- Permisos RBAC; no checks hardcodeados por rol.
- No DELETE físico de históricos.

### Autenticación
- El frontend autentica contra Supabase Auth en `auth.ts`.
- El backend valida el JWT Supabase.
- `GET /api/auth/me` entrega identidad/permisos.
- No existe ni debe agregarse `POST /api/auth/login` en .NET.

## Contexto institucional
- Modo single: resuelve institución activa.
- Modo multi: exige contexto institucional explícito.
- Selector global multiinstitución: pendiente.
- Grados y jornadas son por institución desde 020.
- Secciones validan contexto institucional mediante FK compuestas.

## Modelo académico
Institución -> Ciclo -> Período matrícula -> Grado -> Jornada opcional -> Sección -> Matrícula -> Alumno.

- Los períodos pueden ser anticipados, normales o extraordinarios.
- Fechas de matrícula y fechas académicas son independientes.
- Una sección con matrículas no cambia ciclo, grado ni jornada.

## Migraciones
Todas las migraciones activas 001→026 están en `main`.

- 001-008: RBAC y modelo académico base.
- 009: RLS y RPC.
- 010: identidad.
- 011: creación de alumno con documento.
- 012: configuración de implementación.
- 013: centro educativo.
- 014: ciclos/períodos.
- 015: períodos anticipados.
- 016: grados, jornadas y secciones.
- 017: responsables.
- 018: configuración financiera.
- 019: cargos/obligaciones + `matriculas.plan_pago_id`.
- 020: grados/jornadas multiinstitución.
- 021: pagos/cobranza.
- 022: portal responsable read-only.
- 023: permisos de aplicación `academico.ciclos.*`.
- 024: permisos de aplicación `academico.estructura.*`.
- 025: corrección de unicidad de grados/jornadas por institución.
- 026: permite rematrícula en el mismo ciclo únicamente cuando la matrícula anterior está `anulada`; mantiene bloqueadas las matrículas no anuladas.

### Estado real de producción
El 2026-09-09 se confirmó que producción estaba detenida en 018. Se ejecutaron manualmente, una por una, con PostgreSQL 17 `psql` y `ON_ERROR_STOP=1`:

`019 -> 020 -> 021 -> 022 -> 023 -> 024 -> 025`

Cada migración terminó en `COMMIT` y se ejecutó su validación SQL post-migración antes de continuar. Todas devolvieron cero hallazgos.

Después del merge del PR #63 se aplicó también `026_permitir_rematricula_tras_anulacion.sql` y su validación devolvió cero hallazgos.

Estado final de producción:
- 019 aplicada + validada.
- 020 aplicada + validada.
- 021 aplicada + validada.
- 022 aplicada + validada.
- 023 aplicada + validada.
- 024 aplicada + validada.
- 025 aplicada + validada.
- 026 aplicada + validada.

Antes de aplicar la cadena 019→025 se creó backup restorable del schema `public` con `pg_dump` 17.

## Validaciones de migraciones
- PR #60 (`test(db): completar validaciones faltantes de migraciones 019 y 022`) mergeado en `main` como `0ede6ff9a5d9f9ff69da3f1e0a693ce89c304d59`.
- El repositorio mantiene relación 1:1 entre migraciones activas, rollback y validación SQL.
- Se añadieron validaciones faltantes para 019 y 022.
- `MigrationTests` exige que toda migración activa tenga exactamente un rollback y una validación.
- CI run #287 quedó verde después de refactorizar duplicación detectada por Sonar.
- PR #63 amplió la cadena esperada hasta 026 y CI run #317 quedó completamente verde, incluidos tests DB y Sonar Quality Gate.
- No se relajaron reglas ni umbrales de Sonar.

### Nota 020/025
020 tenía compatibilidad incompleta con nombres históricos de constraints globales (`uq_grados_nombre` / `uq_jornadas_nombre`). 025 corrige ese residual y exige:
- ausencia de unicidad global histórica;
- `ux_grados_institucion_nombre` UNIQUE;
- `ux_jornadas_institucion_nombre` UNIQUE;
- definición `(institucion_id, lower(btrim(nombre)))`.

### Nota 026 — rematrícula tras anulación
La restricción histórica `UNIQUE(alumno_id, ciclo_id)` impedía conservar una matrícula anulada y volver a matricular al mismo alumno en el mismo ciclo.

026 sustituye esa restricción por un índice UNIQUE parcial con el mismo nombre `uq_matriculas_alumno_ciclo`:
- `estado = 'anulada'` libera alumno+ciclo para una nueva matrícula;
- `pendiente`, `activa`, `finalizada`, `retirada` y `trasladada` siguen bloqueando una segunda matrícula del mismo alumno/ciclo;
- se conserva historial; no se revive ni elimina la fila anulada;
- `ix_matriculas_alumno` mantiene eficiente la consulta del historial completo.

Validación manual en producción:
- rematrícula después de anular: permitida;
- intento con matrícula activa: bloqueado;
- intento con matrícula finalizada: bloqueado.

## Módulo Responsables
- Namespace vigente: `academico.responsables.*`.
- Superficie principal por RPC y API .NET.
- Frontend `/responsables` consume API .NET.
- Sin DELETE físico.

## Cargos
- Tabla `cargos` desde 019.
- Permisos `academico.cargos.{ver,generar,anular}`.
- `vencido` es derivado por fecha.
- Estados `parcial`/`pagado` se sincronizan con 021 mediante triggers.
- API: `CargosController`.
- Frontend `/cargos` consume API .NET.

## Pagos / Cobranza
- `pagos` + `pagos_aplicaciones` desde 021.
- Un pago puede aplicarse a varios cargos.
- Saldo siempre derivado, nunca almacenado.
- Sin sobrepago.
- Anulación atómica con trazabilidad.
- Permisos `academico.pagos.{ver,registrar,anular}`.
- Frontend `/pagos` consume API .NET.

## Portal Responsable
- Migración 022 expone lectura por identidad responsable→alumno.
- RPC principales: alumnos, cargos, resumen financiero, pagos y aplicaciones.
- `/portal-padre` es read-only y queda fuera del AppShell administrativo.
- Sin botón de pago.

## Arquitectura API / Bloque 030
Bloque 030 CERRADO y mergeado mediante PR #53.

- Alumnos, Matrículas, Ciclos/Períodos, Estructura Académica y Configuración pasan por API .NET.
- Cero accesos directos Supabase de negocio en frontend.
- Única excepción productiva: Supabase Auth en `auth.ts`.
- Reglas e invariantes permanecen en RPC/DB.
- Autorización .NET y permisos internos DB siguen siendo capas separadas.

Pruebas de cierre del bloque:
- API: 156/156.
- DB: 156/156.
- Frontend: 289/289.
- CI/Sonar/Quality Gate: verdes.

## Frontend / UX
- Foundation visual `sm-*` y AppShell global mergeados.
- Bloques 024→028 de UI/UX completados.
- Drawer móvil, accesibilidad básica, modales, focus handling y scroll lock cerrados en 028.
- `/login` y `/portal-padre` quedan fuera del AppShell.
- `PermissionGuard` es el guard de navegación vigente.
- `AdminGuard`/`PadreGuard` fueron eliminados; no reintroducirlos.
- PR #62 (`feat(nav): navegación académica y filtros financieros por alumno`) mergeado como `d08235132b4c3c0f8b84d3bd8f8560bdbd49fdf5`.
- Ciclos y Estructura Académica quedan visibles desde el AppShell según permisos.
- Cargos/Pagos permiten acceso directo con selector de alumno y conservan `?alumnoId=...`.
- Se corrigieron refrescos zoneless en Cargos, Pagos y Matrículas; validación manual confirmó que los alumnos se muestran de forma consistente en los selectores.
- Los errores de Matrículas se muestran dentro del formulario/modal activo.

Rutas principales:
- `/dashboard`
- `/alumnos`
- `/matriculas`
- `/responsables`
- `/cargos`
- `/pagos`
- `/configuracion`
- `/configuracion/ciclos`
- `/configuracion/estructura-academica`
- `/configuracion/conceptos-financieros`
- `/configuracion/planes-pago`
- `/portal-padre`

## Incidente Estructura Académica — RESUELTO
Síntoma en producción:
- `/configuracion/estructura-academica` abría, pero mostraba `No tienes permiso para realizar esta operación` y no cargaba grados.

Causa raíz:
- Código/backend ya esperaba permisos y esquema posteriores a 018.
- Producción solo tenía migraciones hasta 018.
- 023/024 agregan permisos de aplicación para ciclos/estructura.
- 020/025 completan el modelo multiinstitución y la unicidad correcta.

Resolución:
- Backup previo.
- Aplicación secuencial 019→025.
- Validación post-migración cero hallazgos en cada paso.
- Refresco de sesión/login.
- Verificación manual: Estructura Académica carga correctamente y desapareció el error de permisos.

## Calidad / Sonar
- SonarScanner for .NET analiza C# real + TypeScript.
- Cobertura backend Cobertura y frontend LCOV importadas.
- Quality Gate bloquea CI cuando falla.
- Guard anti falso-verde de `SONAR_TOKEN` vigente.
- Deudas #7 y #9 relacionadas con Sonar: resueltas.
- PR #60 y PR #63 pasaron QG sin excluir archivos ni bajar thresholds.
- Los hallazgos `High` del Overall Code de Sonar siguen pendientes de auditoría específica; el Quality Gate del código nuevo está verde.

## E2E
- Smoke local disponible.
- E2E autenticado completo sigue pendiente de staging seguro.
- No usar producción para E2E destructivo.

## Riesgos / deuda real pendiente
- Selector global multiinstitución.
- E2E autenticado en staging.
- Revisar los issues `High` históricos de Sonar y clasificar vulnerabilidad real vs deuda/falso positivo.
- Mejorar mensajes de error de negocio: actualmente algunos `23505` pueden mostrar texto crudo de PostgreSQL; deben mapearse a mensajes amigables sin debilitar la validación DB.
- Mejorar observabilidad más allá de `/health` y `/health/ready` si se necesita trazabilidad.
- Revisar divergencia de namespaces de permisos entre aplicación (`academico.estructura.*`, `academico.ciclos.*`) y capa interna DB (`configuracion.*`) para evitar confusión futura, sin romper la separación de capas.

## Próximo bloque recomendado
1. Mejorar mensajes de error de negocio (`23505` y conflictos equivalentes) manteniendo la DB como autoridad.
2. Auditar issues `High` del Overall Code en Sonar.
3. Retomar E2E autenticado en staging seguro.
4. Después, selector global multiinstitución y observabilidad adicional según necesidad.

## Git y operación
- Trabajar siempre en rama; no escribir directamente a `main`.
- No force push.
- No commitear secretos.
- No ejecutar pruebas destructivas contra producción.
- Las reglas operativas completas están en `AGENTS.md`.

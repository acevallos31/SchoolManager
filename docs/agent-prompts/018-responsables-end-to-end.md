# Hermes — Bloque 018 Responsables end-to-end

Trabaja de forma autónoma sobre `/opt/projects/SchoolManager`.

## Objetivo

Completar de extremo a extremo el Bloque 018 — Responsables/padres, con auditoría previa, backend/API, seguridad, frontend, pruebas, documentación y PR listo para revisión.

Este bloque está diseñado como trabajo profundo de varias horas. No inventes tareas para consumir tiempo: prioriza revisión real, integración, cobertura y validación.

NO iniciar Bloque 019 Finanzas.
NO hacer merge a `main`.
NO aplicar cambios a producción ni Supabase remoto.

## Gate 0

Antes de empezar:

1. Lee completos `AGENTS.md`, `docs/AI_CONTEXT.md`, `README.md` y los handoffs recientes.
2. Verifica el estado del PR #29.
3. Solo iniciar 018 cuando PR #29 esté MERGED en `main`.
4. Ejecuta:
   - `git fetch origin`
   - `git checkout main`
   - `git pull --ff-only origin main`
   - `git status`
   - `git log --oneline -12`
5. Working tree debe estar limpio y `main` sincronizado.

Si #29 aún no está mergeado, esperar/monitorizar sin crear otra rama ni duplicar trabajo.

Cuando #29 esté integrado, crear:

`feature/responsables-fase-018`

## Regla crítica de concurrencia

Antes de escribir archivos, verifica que no haya otra ejecución de Hermes trabajando sobre SchoolManager.

Si existe otra ejecución:
- no abras una segunda rama sobre el mismo trabajo;
- no edites los mismos archivos en paralelo;
- coordina por estado Git y conserva cambios válidos;
- solo una ejecución debe hacer commits/push/PR.

## Fase 018A — Descubrimiento y auditoría técnica

No empieces programando. Primero construye el mapa real del módulo Responsables.

Audita:

- `database/baseline/`
- `database/migrations/004_crear_responsables_y_alumno_responsable.sql`
- migración 007 RBAC
- migración 009 RLS/RPC
- validaciones y rollback relacionados
- `docs/database/ERD.md`
- `docs/database/MODULOS.md`
- `docs/database/RBAC.md`
- `docs/database/SUPABASE_SECURITY.md`
- tests DB actuales
- backend actual
- frontend actual

Determina con evidencia:

- qué existe ya;
- qué falta;
- qué está legacy;
- qué permisos están vigentes y cuáles son históricos;
- si existen APIs/RPC suficientes;
- si el frontend tiene flujo parcial;
- si `padres_encargados` legacy sigue apareciendo y dónde;
- si la relación `alumno_responsable` permite el flujo funcional esperado.

Documenta la auditoría en:

`docs/handoffs/018A-auditoria-responsables.md`

No cambies esquema todavía.

## Modelo funcional obligatorio

Respeta las decisiones vigentes del proyecto:

- Persona es identidad global.
- Responsable es un perfil institucional basado en Persona + Institución.
- Usuario y Responsable NO son la misma entidad.
- Un responsable puede representar varios alumnos.
- Un alumno puede tener varios responsables.
- `alumno_responsable` es N:M.
- Puede existir como máximo un responsable principal activo por alumno.
- No usar DELETE físico para históricos.
- Estados de Responsable y vínculo deben respetar el modelo existente.
- No duplicar personas innecesariamente.
- Multiinstitución debe permanecer aislada.

Si alguna de estas reglas contradice el esquema real, documenta la contradicción y toma la decisión mínima compatible con `AI_CONTEXT.md`; si implica cambio de dominio no documentado, BLOQUEA esa subparte.

## Fase 018B — Base de datos / seguridad

Primero decide si realmente hace falta una migración nueva.

NO reescribir migraciones 004, 007, 009 ni ninguna migración aplicada.
NO crear una migración solo porque el bloque se llama 018.

Si el esquema actual ya soporta el flujo correctamente:
- no crear migración;
- agregar únicamente tests/validaciones faltantes si corresponde.

Si existe un hueco objetivo de esquema, constraint, RLS, grants o RPC necesario para el flujo:
- determinar el siguiente número de migración libre en el repositorio;
- crear una migración nueva secuencial;
- incluir rollback seguro cuando la convención lo requiera;
- incluir validation SQL;
- agregar tests DB;
- NO aplicar la migración a Supabase remoto.

Auditar y asegurar:

- `responsables` aislado por institución;
- `alumno_responsable` no cruza instituciones;
- Persona reutilizable sin duplicaciones indebidas;
- unicidad Persona+Institución para Responsable;
- máximo un principal activo por alumno;
- vínculos duplicados manejados correctamente;
- estados activo/inactivo coherentes;
- RLS de lectura/escritura según permisos;
- grants mínimos;
- `search_path` seguro en funciones SECURITY DEFINER;
- ninguna escritura crítica dependa de DML directo inseguro desde frontend.

Si faltan RPCs para crear/editar/vincular responsables y son necesarias para conservar ACID/RLS:
- diseñarlas de forma mínima;
- mantener lógica transaccional en DB/backend;
- no duplicar transacciones en frontend.

## Fase 018C — Backend/API

Implementar o completar API .NET para Responsables usando patrones existentes del repositorio.

Antes de crear archivos, busca patrones en Alumnos y Matrículas.

Objetivos funcionales mínimos:

- listar responsables por institución;
- obtener responsable por id;
- listar responsables de un alumno;
- crear responsable reutilizando Persona cuando corresponda según reglas existentes;
- editar datos permitidos;
- activar/inactivar Responsable cuando el modelo lo permita;
- vincular Responsable ↔ Alumno;
- editar parentesco/datos del vínculo;
- marcar principal garantizando la invariante de un único principal activo;
- desactivar/reactivar vínculo sin borrar históricos;
- impedir cruces de institución;
- devolver 400/403/404/409 de forma consistente;
- nunca filtrar SQL/stack traces al cliente.

Permisos:

Revisa los permisos canónicos reales antes de usarlos. Hay permisos históricos con nombres distintos; NO elijas por intuición. Determina cuál es el namespace vigente y úsalo consistentemente.

No usar checks `rol == admin`.

No introducir:
- CQRS;
- MediatR;
- Generic Repository;
- UnitOfWork artificial;
- microservicios;
- capas nuevas sin necesidad.

Aplicar SOLID pragmáticamente: controllers delgados, DTOs claros, responsabilidades enfocadas y lógica crítica en el lugar correcto.

## Fase 018D — Frontend Angular

Crear/completar flujo de Responsables siguiendo Angular standalone y patrones actuales.

Experiencia mínima:

### Página `/responsables`

- listado;
- loading;
- vacío;
- error;
- búsqueda por nombre/documento si los datos disponibles lo permiten;
- estado activo/inactivo;
- acciones condicionadas por permisos;
- responsive razonable.

### Crear/editar Responsable

Campos según el modelo existente de Persona/Responsable; NO inventar columnas.

Debe:
- validar requeridos;
- manejar documento/persona existente sin duplicación cuando backend lo soporte;
- mostrar errores 400/403/404/409 de forma segura;
- no exponer reglas SQL.

### Integración desde Alumno

En detalle/gestión del alumno:
- ver responsables vinculados;
- agregar/vincular responsable;
- parentesco;
- principal;
- activar/inactivar vínculo;
- impedir visualmente acciones sin permiso;
- mostrar claramente cuál es principal.

El frontend NO debe intentar garantizar por sí solo la invariante de principal único; puede mejorar UX, pero DB/backend siguen siendo autoridad.

## Fase 018E — Eliminación/aislamiento de legacy

Buscar todo uso de:

- `padres_encargados`
- textos legacy de responsables embebidos en alumno
- DML directo antiguo contra `responsables`/`alumno_responsable`
- permisos antiguos inconsistentes

No borrar datos ni columnas legacy automáticamente.

Si todavía se necesita compatibilidad:
- aislarla;
- evitar que nuevos flujos sigan escribiendo en legado;
- documentar deuda pendiente.

Solo eliminar código legacy si es inequívocamente obsoleto y no rompe compatibilidad.

## Fase 018F — Tests profundos

Revisar cobertura existente antes de agregar.

### DB

Cubrir como mínimo:

- Responsable Persona+Institución único;
- vínculo Alumno↔Responsable;
- duplicado rechazado o reactivado según modelo vigente;
- máximo un principal activo;
- no cruces de institución;
- FK válidas;
- estados;
- RLS/permiso si existe infraestructura de pruebas para ello;
- RPCs nuevas si se crean;
- rollback/validation si hubo migración.

### API

Cubrir:

- listar;
- detalle;
- crear;
- editar;
- vincular;
- principal;
- inactivar/reactivar;
- 400;
- 403;
- 404;
- 409;
- multiinstitución;
- actor/claims cuando aplique.

### Frontend

Cubrir:

- listado;
- vacío;
- loading;
- error;
- permisos;
- creación válida;
- error 409;
- edición;
- vínculo a alumno;
- principal;
- inactivo;
- ausencia de acciones sin permiso;
- errores seguros.

Evitar `any` en producción y minimizarlo en tests.

## Fase 018G — Auditoría remota Supabase read-only

Si las credenciales de auditor existentes están disponibles, realizar auditoría READ ONLY de Supabase.

Verificar metadata de:

- tablas;
- constraints;
- índices;
- funciones;
- grants;
- policies;
- permisos RBAC relacionados con responsables.

Comparar remoto vs repo.

NO:
- ejecutar DML remoto;
- aplicar migraciones;
- modificar policies;
- usar service-role;
- leer PII innecesaria.

Si no hay credenciales disponibles, documentar pendiente y continuar con todas las demás fases.

## Fase 018H — Validación integral

Ejecutar como mínimo:

```bash
dotnet build backend/SchoolManager.API/SchoolManager.API.csproj -c Release

dotnet test tests/SchoolManager.API.IntegrationTests/SchoolManager.API.IntegrationTests.csproj -c Release

dotnet test tests/SchoolManager.Database.IntegrationTests/SchoolManager.Database.IntegrationTests.csproj -c Release

cd frontend/schoolmanager-frontend
npm ci --ignore-scripts
npx ng test --watch=false
npm run build -- --configuration production
cd ../..

git diff --check
```

Si una validación falla por trabajo de 018:
- diagnosticar;
- corregir;
- volver a ejecutar;
- continuar.

No detenerse por fallos rutinarios corregibles.

## Fase 018I — Revisión de calidad

Antes del commit final, revisar el diff completo y hacer una segunda pasada buscando:

- sobrealcance;
- duplicación de lógica;
- `any` innecesario;
- N+1 evidente;
- errores de autorización;
- exposición cross-institution;
- mensajes de error inseguros;
- transacciones incompletas;
- accesibilidad básica en forms;
- controles sin label;
- HTML inválido;
- código muerto;
- warnings nuevos;
- restos de debug/console.log;
- secretos;
- archivos truncados o generados accidentalmente.

Nunca reconstruir archivos desde outputs truncados.

## Fase 018J — Documentación

Actualizar `docs/AI_CONTEXT.md` solo con estado funcional/arquitectónico real.

Crear handoffs separados cuando aporte claridad:

- `docs/handoffs/018A-auditoria-responsables.md`
- `docs/handoffs/018B-db-seguridad.md`
- `docs/handoffs/018C-backend.md`
- `docs/handoffs/018D-frontend.md`
- `docs/handoffs/018F-tests.md`
- `docs/handoffs/018-cierre.md`

### AGENTS.md

AGENTS.md es compartido por múltiples agentes.

Puedes AGREGAR tu propio contenido/HANDOFF y nuevas reglas derivadas de esta sesión si están dentro del alcance.

NO puedes:
- borrar contenido de otros agentes;
- reemplazar silenciosamente su handoff;
- reescribir decisiones anteriores solo para acomodar tu solución.

Si el runtime bloquea escribir `AGENTS.md`, no detengas todo el bloque por eso: conserva handoff en `docs/handoffs/` y reporta la limitación.

## Git autorizado

Autorizado:
- crear `feature/responsables-fase-018`;
- editar backend/frontend/database/tests/docs dentro de 018;
- commits Conventional Commits en español;
- push normal;
- crear PR a main;
- corregir CI/Sonar/Vercel causados por 018;
- marcar PR Ready for review si todo queda verde.

No autorizado:
- merge a main;
- force push;
- `git reset --hard`;
- `git clean -fd`;
- rebase destructivo;
- borrar ramas ajenas;
- modificar producción;
- aplicar migraciones remotas.

Commits sugeridos, solo si corresponden:

- `feat(responsables): implementar gestion de responsables`
- `feat(responsables): integrar responsables con alumnos`
- `test(responsables): completar cobertura del modulo`
- `docs(contexto): documentar bloque de responsables`

## Criterio de terminación

No declarar 018 cerrado hasta haber completado todas las fases posibles de forma segura.

Si alguna subfase queda pendiente por entorno externo, marcar 018 PARCIAL y explicar exactamente cuál, sin abandonar las demás.

Al finalizar:

- código coherente;
- tests verdes;
- build verde;
- `git diff --check` verde;
- documentación actualizada;
- commit(s);
- push;
- PR creado;
- PR Ready for review si checks verdes;
- NO merge.

Enviar resumen final por Telegram con:

- estado: CERRADO / PARCIAL / BLOQUEADO;
- rama;
- HEAD;
- PR;
- si hubo o no migración y por qué;
- API tests;
- DB tests;
- frontend tests;
- build backend/frontend;
- CI;
- Sonar;
- Vercel;
- auditoría Supabase;
- riesgos;
- deuda legacy;
- siguiente paso exacto.

NO iniciar Bloque 019.
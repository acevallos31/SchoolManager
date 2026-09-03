# AGENTS.md — Reglas operativas para agentes de IA

Este archivo define cómo deben trabajar Hermes, Codex, Copilot u otros agentes sobre SchoolManager. Su objetivo es permitir trabajo autónomo sin perder control arquitectónico, trazabilidad ni seguridad.

## 1. Orden obligatorio de lectura

Antes de analizar, planificar o modificar código, el agente debe leer en este orden:

1. `AGENTS.md` completo.
2. `docs/AI_CONTEXT.md` completo.
3. `README.md`.
4. Los archivos específicos del módulo o tarea asignada.
5. `git status`, rama actual y últimos commits relevantes.

`docs/AI_CONTEXT.md` es la fuente principal para estado funcional, roadmap y decisiones arquitectónicas. `AGENTS.md` define reglas de operación. No duplicar innecesariamente información entre ambos.

## 2. Arquitectura y decisiones cerradas

Mantener las decisiones actuales salvo instrucción explícita del responsable del proyecto:

- Frontend Angular 22 standalone con TypeScript.
- Backend ASP.NET Core Web API sobre .NET 10.
- PostgreSQL/Supabase como base de datos.
- Supabase Auth + JWT.
- Monolito modular.
- UUID como PK/FK internos.
- RLS y RPC para escrituras críticas donde corresponda.
- RBAC por permisos; no checks hardcodeados por nombre de rol.
- No DELETE físico de históricos.
- Evitar CQRS, MediatR, microservicios, Generic Repository y UnitOfWork artificial.
- No mezclar Angular standalone con `AppModule`/`NgModule` clásico.

Si una tarea exige cambiar una de estas decisiones, el agente debe detenerse y pedir aprobación antes de implementar.

## 3. Fuente de verdad y contexto de dominio

El agente debe respetar lo documentado en `docs/AI_CONTEXT.md`, incluyendo:

- modelo académico vigente;
- estado de migraciones;
- modo single/multiinstitución;
- fases completadas y pendientes;
- reglas de matrículas, períodos, grados, jornadas y secciones;
- roadmap activo.

Si código, documentación y migraciones parecen contradecirse, no elegir una interpretación por cuenta propia. Documentar la contradicción y pedir decisión.

## 4. Reglas de alcance

Trabajar únicamente dentro del alcance solicitado.

Permitido:

- leer e inspeccionar cualquier archivo necesario;
- crear una rama de trabajo cuando la tarea lo requiera;
- modificar archivos directamente relacionados con la tarea;
- agregar o mejorar tests;
- ejecutar builds, tests, linters y validaciones;
- refactorizar internamente si no cambia comportamiento y está dentro del alcance;
- documentar hallazgos y handoff.

No permitido sin aprobación explícita:

- ampliar el alcance por iniciativa propia;
- cambiar arquitectura global;
- cambiar contratos públicos o modelo de dominio no solicitados;
- aplicar migraciones en Supabase o producción;
- borrar datos;
- cambiar secretos o credenciales;
- modificar infraestructura de producción;
- hacer merge a `main`;
- hacer push o abrir PR si la tarea no lo autoriza;
- modificar decisiones cerradas de `docs/AI_CONTEXT.md`.

## 5. Regla de parada obligatoria

El agente debe detenerse y pedir decisión si encuentra cualquiera de estos casos:

- una decisión arquitectónica no resuelta;
- necesidad de modificar producción fuera del alcance;
- migración destructiva o irreversible;
- riesgo de pérdida de datos;
- cambios de permisos/RLS/RPC cuyo comportamiento no esté documentado;
- secretos, tokens o credenciales expuestos;
- tests que solo pueden pasar alterando comportamiento funcional no solicitado;
- discrepancia importante entre contexto, código y base de datos;
- necesidad de reescribir una migración ya aplicada;
- necesidad de force-push, rebase destructivo o cambio directo sobre `main`.

Cuando se detenga debe explicar: problema, evidencia, opciones y recomendación.

## 6. Git y ramas

Reglas obligatorias:

- No trabajar directamente sobre `main` para cambios reales.
- Crear una rama descriptiva desde una base limpia y actualizada.
- Usar Conventional Commits en español.
- No hacer commit, push ni PR automáticamente salvo que la instrucción actual lo autorice.
- Antes de cada commit revisar `git diff` y `git status`.
- No incluir archivos colaterales no relacionados con la tarea.
- Si una herramienta modifica `package.json`, `package-lock.json` u otros archivos fuera del alcance, revertir esos cambios salvo que sean necesarios y estén explicados.
- Nunca usar `git reset --hard`, `git clean -fd`, force-push u operaciones destructivas sin autorización explícita.

Formato recomendado de ramas:

- `feature/<descripcion>`
- `fix/<descripcion>`
- `refactor/<descripcion>`
- `test/<descripcion>`
- `docs/<descripcion>`

## 7. Base de datos y migraciones

- Las migraciones son manuales en Supabase después de revisión y CI.
- No aplicar migraciones a producción automáticamente.
- No alterar retrospectivamente una migración ya aplicada salvo instrucción explícita.
- Mantener script principal, rollback y validación cuando la convención de la migración lo requiera.
- Antes de proponer una migración nueva, revisar baseline, migraciones previas y tests de integración.
- Toda escritura crítica debe respetar RLS/RPC y contexto de institución según el diseño vigente.
- No introducir identificadores secuenciales como sustituto de UUID internos.

## 8. Seguridad y secretos

- Nunca imprimir, copiar, registrar o commitear `.env`, API keys, tokens, JWT, passwords o cadenas de conexión completas.
- No leer secretos si no son necesarios para la tarea.
- No insertar credenciales reales en tests o documentación.
- Usar placeholders en ejemplos.
- Si se detecta un secreto versionado, detenerse y reportarlo; no rotarlo ni eliminarlo sin instrucciones.

## 9. Backend

Para cambios de backend:

- Mantener ASP.NET Core Web API y .NET 10.
- Respetar separación actual entre Controllers, DTOs, Models y acceso a datos existente.
- No introducir capas o patrones nuevos solo por preferencia del agente.
- Preferir cambios pequeños y verificables.
- Asegurar validación de inputs, autorización por permisos y contexto institucional.
- Evitar lógica de negocio duplicada entre controllers, SQL/RPC y frontend.
- Agregar tests cuando se cambie comportamiento.

Validación mínima cuando el backend sea afectado:

```bash
cd backend/SchoolManager.API
dotnet build -c Release
cd ../..
dotnet test
```

Si existen pruebas específicas del módulo modificado, ejecutarlas también.

## 10. Frontend

Para cambios de frontend:

- Angular 22 standalone.
- No introducir `NgModule` clásico.
- Mantener tipado estricto; evitar `any` salvo necesidad documentada.
- Mantener servicios y componentes enfocados.
- No cambiar contratos backend/frontend unilateralmente.
- Si se modifica `package.json`, regenerar `package-lock.json` con la versión de npm declarada por el proyecto.

Validación mínima cuando el frontend sea afectado:

```bash
cd frontend/schoolmanager-frontend
npm test -- --watch=false
npm run build
```

Si el runner actual interpreta `npm test` de otra manera, usar el comando equivalente que ejecute Vitest una sola vez y documentarlo.

## 11. Validación global obligatoria

Antes de declarar una tarea terminada ejecutar las validaciones que correspondan al alcance. Como regla general:

```bash
git diff --check
```

Y, cuando aplique:

```bash
dotnet build -c Release
dotnet test
npm test -- --watch=false
npm run build
```

No declarar "completado" si una validación requerida falla. Distinguir claramente entre:

- fallo causado por el cambio;
- fallo preexistente;
- validación no ejecutada y motivo.

No ocultar warnings nuevos.

## 12. Calidad de cambios

- Preferir el cambio mínimo que resuelva el objetivo.
- No hacer refactors cosméticos masivos durante una tarea funcional.
- No renombrar archivos, símbolos o carpetas sin necesidad.
- No introducir dependencias nuevas si la plataforma ya resuelve el problema.
- Mantener compatibilidad con decisiones actuales de despliegue en Render, Vercel y Supabase.
- Los comentarios deben explicar decisiones no obvias, no describir código evidente.
- Tests deben validar comportamiento observable, no detalles internos frágiles.

## 13. Trabajo autónomo de larga duración

Cuando Hermes u otro agente trabaje durante varias horas:

1. Confirmar rama y working tree antes de comenzar.
2. Dividir el objetivo en checkpoints pequeños.
3. Ejecutar tests después de cada cambio importante.
4. No acumular múltiples bloques funcionales sin validación intermedia.
5. Si una ruta falla repetidamente, detener esa línea de trabajo y documentar el bloqueo en lugar de improvisar cambios amplios.
6. Mantener el alcance original.
7. No crear commits intermedios salvo autorización o que la tarea lo pida explícitamente.
8. No dejar procesos destructivos o migraciones esperando confirmación automática.
9. Antes de terminar, dejar el repositorio en un estado coherente y reproducible.

## 14. Uso de modelos auxiliares

Los modelos auxiliares pueden usarse para tareas de bajo riesgo como títulos, reescritura de consultas, búsqueda de skills, clasificación, triage y revisión auxiliar.

No delegar exclusivamente a modelos pequeños/locales:

- decisiones arquitectónicas;
- migraciones destructivas;
- cambios de seguridad/RLS/RBAC;
- diseño de dominio;
- resolución final de conflictos ambiguos;
- operaciones Git destructivas.

El modelo principal conserva responsabilidad sobre herramientas, ejecución y decisión final.

## 15. Documentación de contexto

Actualizar `docs/AI_CONTEXT.md` solo cuando cambie realmente el estado arquitectónico, funcional o del roadmap.

No usar `AI_CONTEXT.md` como diario de cada sesión.

Este `AGENTS.md` puede contener el handoff operativo más reciente, pero no debe convertirse en un log histórico ilimitado. Al iniciar un nuevo bloque, reemplazar el handoff anterior una vez que su información relevante haya sido consolidada en commits, PR o `AI_CONTEXT.md`.

## 16. Criterio de finalización

Una tarea solo puede declararse terminada cuando:

- el alcance solicitado está implementado;
- el diff fue revisado;
- no hay cambios colaterales inexplicados;
- las validaciones aplicables pasan;
- `git diff --check` pasa;
- el estado de Git está documentado;
- cualquier riesgo o pendiente está explícito;
- el handoff está actualizado si la sesión termina sin completar todo el bloque.

## 17. HANDOFF operativo

Al finalizar una sesión larga, completar o reemplazar esta sección.

### Rama

`main` / por definir al iniciar el siguiente bloque.

### Objetivo de la sesión

Sin trabajo autónomo activo. Este archivo establece las reglas base para futuros agentes.

### Completado

- Reglas operativas para agentes definidas.
- Separación establecida entre `AGENTS.md` y `docs/AI_CONTEXT.md`.

### Archivos modificados

- `AGENTS.md`

### Validaciones

- Documentación únicamente; no requiere build ni tests.

### Decisiones tomadas

- `docs/AI_CONTEXT.md` continúa siendo la fuente de verdad de arquitectura/roadmap.
- `AGENTS.md` controla ejecución, seguridad, Git, validaciones y handoff.

### Pendientes

- Revisar y aprobar estas reglas antes de iniciar un bloque autónomo largo.
- Actualizar este HANDOFF al comenzar y terminar el siguiente bloque real.

### Bloqueos

Ninguno.

### Siguiente paso recomendado

Revisar este archivo y `docs/AI_CONTEXT.md`; luego asignar un único bloque funcional bien delimitado a Hermes en una rama dedicada.

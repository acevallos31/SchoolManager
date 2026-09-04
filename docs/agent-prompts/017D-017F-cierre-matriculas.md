# Prompt operativo — Bloque 017D–017F

Trabaja de forma autónoma sobre `/opt/projects/SchoolManager`.

## Objetivo
Cerrar completamente el Bloque 017 Matrículas Fase 1C mediante:
- 17D Integración Alumnos → Matricular
- 17E Tests completos
- 17F Integración/Preview/cierre

No iniciar Bloque 018 Responsables.
No crear trabajo artificial para ocupar tiempo. Si terminas antes de 3 horas con todo validado, termina y reporta.

Antes de modificar código lee completos:
1. `AGENTS.md`
2. `docs/AI_CONTEXT.md`
3. `README.md`
4. `docs/HANDOFF.md` si existe

Respeta ACID y SOLID según `AGENTS.md`.

## GATE 0
Ejecuta:
- `git fetch origin`
- `git checkout main`
- `git pull --ff-only origin main`
- `git status`
- `git log --oneline -10`

Verifica:
- PR #28 mergeado en `main`.
- 17A Backend presente.
- 17C Frontend presente.
- working tree limpio.
- `origin/main` sincronizado.

Si PR #28 NO está mergeado:
- reporta BLOQUEADO por Telegram;
- no inicies una rama nueva;
- no modifiques código.

Si todo está correcto, crea desde `main` actualizado:
`feature/matriculas-fase-1c-cierre`

## 17D — Integración Alumnos → Matricular
Audita la integración creada en 17C.

Verifica funcionalmente:
- botón `Matricular` solo para alumnos activos;
- requiere permiso `academico.matriculas.crear`;
- alumno inactivo no ofrece acción;
- navegación a `/matriculas?alumnoId=<id>`;
- alumno llega preseleccionado;
- formulario no duplica reglas de negocio del backend;
- errores de matrícula se muestran de forma segura;
- usuario sin permiso no puede crear;
- no existe estado `cancelada`;
- no quedan restos del flujo antiguo de matrícula con monto o estado `pagada`.

Si ya está correcto, no lo reescribas. Agrega solo tests o correcciones necesarias.

## 17E — Tests completos
Revisa cobertura real de Matrículas en frontend, backend y database integration. Agrega tests solo donde exista un hueco relevante.

Cobertura mínima esperada en frontend:
- listado;
- vacío;
- loading;
- error;
- filtros alumno/ciclo/estado;
- alumno preseleccionado por query param;
- creación válida;
- campos obligatorios;
- 409 matrícula duplicada;
- permisos ver/crear/cambiar_estado;
- alumno inactivo sin acción Matricular;
- transiciones válidas;
- transiciones inválidas no ofrecidas;
- motivo obligatorio;
- ausencia completa del estado `cancelada`.

Servicio HTTP:
- GET;
- POST;
- PUT estado;
- errores 400, 403, 404, 409 y 500.

Backend/API:
- revisa tests existentes antes de agregar otros;
- no dupliques tests ya cubiertos.

DB:
- revisa cobertura existente antes de agregar;
- no modifiques migraciones aplicadas para hacer pasar tests.

Ejecuta:
- `dotnet build backend/SchoolManager.API/SchoolManager.API.csproj -c Release`
- `dotnet test tests/SchoolManager.API.IntegrationTests/SchoolManager.API.IntegrationTests.csproj -c Release`
- `dotnet test tests/SchoolManager.Database.IntegrationTests/SchoolManager.Database.IntegrationTests.csproj -c Release`
- `cd frontend/schoolmanager-frontend`
- `npm ci --ignore-scripts`
- `npx ng test --watch=false`
- `npm run build -- --configuration production`
- `cd ../..`
- `git diff --check`

## 17F — Integración / Supabase / Preview
Verifica que el contrato completo de Matrículas sea coherente entre frontend, API .NET y PostgreSQL/Supabase.

No crear migración nueva salvo evidencia objetiva de una divergencia real.
No aplicar migraciones a Supabase.
No escribir datos reales de producción.
No modificar RLS/RPC remotos.
No usar service-role para saltarse seguridad.

Está permitido:
- auditoría read-only de Supabase usando las credenciales de auditor existentes;
- consultar metadata, funciones, constraints, grants y policies;
- comparar contra `database/migrations` y backend;
- usar Testcontainers/local DB para pruebas de escritura;
- usar preview de Vercel/API para verificaciones no destructivas.

Audita que existan y coincidan con repo:
- tablas necesarias de matrículas;
- constraints;
- estados;
- `rpc_matricular_alumno`;
- `rpc_cambiar_estado_matricula`;
- permisos;
- RLS;
- aislamiento por institución;
- capacidad/cupo;
- matrícula única alumno+ciclo;
- historial de estados.

Si detectas divergencia remota:
- no corrijas Supabase;
- documenta evidencia;
- marca 17F PARCIAL o BLOQUEADO en esa parte.

## E2E / contrato real
Intenta la validación más realista posible sin tocar datos productivos.

Prioridad:
1. Testcontainers/local PostgreSQL.
2. Entorno preview/staging desechable si existe.
3. Auditoría remota read-only.

No inventes credenciales o entornos.
Si no existe un entorno seguro para E2E con escritura, documenta que E2E real queda pendiente, pero continúa todas las demás validaciones.

## Calidad
Aplica SOLID de forma pragmática.

No introducir:
- capas nuevas innecesarias;
- CQRS;
- MediatR;
- microservicios;
- Generic Repository;
- UnitOfWork artificial;
- reglas transaccionales en frontend.

ACID: la DB/backend siguen siendo autoridad para operaciones de matrícula.

## Autonomía
No pidas aprobación para:
- leer archivos;
- corregir código dentro del alcance;
- agregar tests;
- ejecutar builds/tests;
- corregir Sonar;
- commit;
- push normal;
- crear PR;
- actualizar PR;
- documentación;
- HANDOFF.

Si algo falla:
- investiga;
- corrige si está dentro del alcance;
- vuelve a validar;
- continúa.

Solo detente por:
- merge a main;
- force push;
- reset destructivo;
- producción;
- escritura en datos reales remotos;
- migración remota;
- riesgo de pérdida de datos;
- secretos faltantes;
- cambio arquitectónico fuera del alcance.

## Git
Autorizado:
- rama `feature/matriculas-fase-1c-cierre`;
- commits;
- push normal;
- PR hacia `main`;
- marcar PR Ready for review si todo queda verde.

No autorizado:
- merge a main;
- force push;
- `reset --hard`;
- `git clean -fd`;
- borrar ramas ajenas.

Commits sugeridos:
- `test(matriculas): completar cobertura del flujo de matriculas`
- `fix(matriculas): cerrar integracion del flujo de matriculas`
- `docs(contexto): cerrar bloque de matriculas fase 1c`

## Documentación
Actualiza `docs/AI_CONTEXT.md`.

Agrega nuevos HANDOFF sin borrar aportes de otros agentes, siguiendo `AGENTS.md`.

Preferir:
- `docs/handoffs/017D-integracion-alumnos.md`
- `docs/handoffs/017E-tests.md`
- `docs/handoffs/017F-cierre.md`

Si `docs/handoffs` no existe, créalo.

En `AGENTS.md`, agrega contenido nuevo de esta sesión sin borrar ni reemplazar contenido creado por otros agentes. Puedes actualizar únicamente contenido creado por ti en esta misma sesión cuando sea necesario.

## Final
Si todo queda verde:
- commit;
- push;
- PR;
- Ready for review;
- no merge.

Enviar por Telegram:
`BLOQUE 017 CERRADO / PARCIAL / BLOQUEADO`

Incluir:
- rama;
- HEAD;
- PR;
- frontend tests;
- backend tests;
- DB tests;
- build;
- CI;
- Sonar;
- Vercel;
- auditoría Supabase;
- divergencias;
- bloqueos;
- próximo paso exacto.

No iniciar 018.

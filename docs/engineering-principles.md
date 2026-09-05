# Principios de Ingeniería — SchoolManager

Reglas del proyecto derivadas de la experiencia acumulada en las fases
001→018 (matrículas, responsables, configuración financiera). Son normas
vinculantes para todo agente (humano o IA) que toque este repositorio.
En PRs, handoffs y decisiones técnicas relevantes se pueden citar por
número (p. ej. «según #4») para señalar qué norma aplica.

No son teoría: cada principio incluye cómo se aplica **aquí**.

---

## Los 12 principios

### #1 — Se lee 10x más de lo que se escribe. Optimizar para el lector.
El código se escribe una vez y se lee cientos. Nombres explícitos,
estructura obvia y cambios pequeños valen más que ingenio comprimido.
En SchoolManager: nombres largos y claros en DTOs, controladores y
migraciones (p. ej. `ConfiguracionFinancieraMultitenancyTests`),
rutas y funciones con nombre auto-descriptivo. Un PR que cuesta entender
es un PR que no está listo.

### #2 — Piezas pequeñas con nombre claro — base del testing y diseño.
Una clase/archivo/migración debe tener una sola responsabilidad y un
nombre que la delate. Las suites del repo se organizan por pieza
(`ResponsablesGestionTests`, `ConfiguracionFinancieraMultitenancyTests`),
y una migración = un archivo `NNN_descripcion.sql` versionado.

### #3 — El código dice qué hace; el comentario explica decisiones y restricciones.
No comentes lo obvio. Comenta el *por qué*: restricciones de negocio,
motivos de un enfoque no trivial, advertencias de seguridad. Ejemplo real:
la nota en controladores .NET sobre pasar `?? DBNull.Value` a Npgsql
(no `null`) — el *cómo* está en el código, el *por qué* (falla en runtime)
es el comentario que salva al siguiente.

### #4 — Duplicar es el smell más caro: cada regla de negocio vive en UN solo lugar.
Si una regla (precio, estado, permiso, versión de esquema) aparece en dos
archivos, tarde o temprano diverge. En SchoolManager: permisos declarados
una vez en `Permisos.cs`, secuencia de migraciones en un solo test
(`MigrationTests.cs`), convención de migraciones en `001_*`.

### #5 — La solución más simple que funcione hoy. La sobre-ingeniería también es deuda.
No introduzcas abstracciones que hoy nadie usa. SchoolManager es un
**monolito modular**: sin microservicios, sin CQRS, sin Generic
Repository/UoW artificiales. Cuando algo se vuelve complejo de verdad,
se extrae un módulo con contrato claro — no antes.

### #6 — Tests primero, refactor después. Cambio sin verificación = apuesta.
Una migración, un permiso o un endpoint que cambia sin test que lo fije
es una apuesta. El pipeline debe correr la matriz completa antes de
integrar. Refactorizar sin red de tests no es refactor, es reescribir.

### #7 — Ramas cortas, commits chicos, merges frecuentes. 20 pasos seguros > 1 salto heroico.
Cada fase es una rama (`feature/...`, `chore/...`) con commits pequeños
conventional y un PR contra `main`. Un cambio grande se entrega en
bloques revisables, no en un commit monolítico de 40 archivos.

### #8 — PR + code review, también y especialmente el código generado por IA.
Ningún output se integra sin revisión humana. La IA acelera, no decide.
Esto aplica al doble: el revisor debe leer lo que la IA produjo y la IA
debe producir algo legible de revisar (véase #1).

### #9 — Bajo acoplamiento, alta cohesión, dependencias hacia adentro: SOLID y arquitectura.
Mantener el monolito modular: capas que dependen hacia dentro, módulos
de negocio que no se acoplan entre sí por encima de lo necesario, RLS y
RPC en base, autorización por permiso en backend y cheques de permiso en
los componentes.

### #10 — Tests, lint, build, deploy: si se hace dos veces manualmente, debe ir al pipeline.
Si un paso de validación se repite a mano (tests, build, `git diff --check`),
automatízalo en CI. Lo manual se olvida; el pipeline no. Lo que ya está en
el pipeline no se ejecuta "por si acaso" a mano.

### #11 — Logs, métricas y monitoreo: el software vivo se observa. "Funciona en mi máquina" no es evidencia.
Verde en local no es evidencia de que funcione en producción. Hace falta
healthcheck, logging útil y la capacidad de ver el sistema desplegado.
Lo que no se observa, se rompe en silencio.

### #12 — Verificar todo output, entender lo que se integra, nunca delegar el criterio.
Nadie —ni una IA ni una herramienta— sustituye la verificación. Todo
output (código, migración, documento) se lee y se prueba antes de
integrar. Si no puedes explicarlo, no lo integres.

---

## Prácticas de base de datos y operación

### ACID como criterio para operaciones de base de datos
Toda operación que afecte varias filas/tablas (p. ej. crear una
configuración que valida contra otras) debe evaluarse bajo ACID:
atomicidad, consistencia, aislamiento, durabilidad. Ver
`docs/database/ACID.md` para el detalle aplicado al modelo académico.

### SOLID pragmático
Aplicar SOLID con criterio de *cuándo hace falta*, no como dogma. SRP e
inyección de dependencias sí; interfaces y abstracciones solo cuando hay
una segunda implementación o un punto de extensión real (principio #5).

### Seguridad por mínimo privilegio
La base usa RLS + RPC con `security definer` y revokes directos sobre las
tablas: la app solo toca la base a través de funciones autorizadas. Los
usuarios y roles tienen únicamente los privilegios que su función exige.
Los permisos de aplicación viven en `Permisos.cs` y se validan por
permiso, no por rol implícito.

### Migraciones versionadas
Cada cambio de esquema es un archivo `NNN_nombre.sql` en
`database/migrations/`, precedido por una convención (`001_*`). La
secuencia se valida con un test de orden
(`Migraciones_activas_estan_ordenadas_de_001_a_018`). Una migración solo
puede depender de una versión ya existente (`prerequisite version='NNN'`).

### Tests automatizados
Matriz por capa: tests de identidad/autorización (API), tests de base de
datos (DB Integration, incluyen multitenancy/atomicidad y acceso
directo), tests unitarios del frontend, build de producción. Los números
se reportan en cada PR/handoff.

### Rollback
Cada migración reversible tiene su `rollback/NNN_*.rollback.sql`. Todo
PR que introduzca un cambio de esquema debe acompañar su rollback y
validación (`validation/*`).

### CI/CD
`.github/workflows/deploy.yml` valida backend, API tests, DB tests y
build frontend en cada push/PR; despliega a producción solo desde `main`.
El despliegue de frontend lo gestiona Vercel vía integración Git.

### Handoffs
Cada bloque/fase cerrado documenta su estado en `docs/handoffs/` y en
`docs/HANDOFF.md`, incluyendo qué se validó, qué quedó pendiente y los
contratos disponibles para la siguiente fase. Un trabajo sin handoff no
está terminado.

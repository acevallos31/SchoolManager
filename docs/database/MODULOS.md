# Módulos de base de datos

SchoolManager mantiene un monolito modular: una sola base PostgreSQL y una sola
API, con límites de negocio explícitos. Las FK entre módulos se permiten cuando
representan relaciones del dominio; no se duplican identidades ni datos
derivables para simular independencia.

PostgreSQL es el núcleo de integridad, transacciones ACID, RLS y RPC. La API
.NET atiende Angular y autoriza por permisos obtenidos desde RBAC. Un cliente
Expo futuro podrá usar Supabase directamente: PostgREST + RLS para lectura y
RPC para escrituras críticas. Se mantiene un monolito modular; no se introducen
microservicios, CQRS ni MediatR.

## Módulos actuales

### Identidad

- `personas`: identidad civil global, independiente de los perfiles.
- `usuarios`: vínculo 1:1 entre Persona y Supabase Auth mediante
  `auth_user_id`.
- `roles` y `permisos`: catálogo estable de autorización.
- `usuarios_roles`: asignaciones globales o por institución, con histórico de
  desactivación.
- `roles_permisos`: composición vigente de permisos de cada rol.

### Académico

- `alumnos`: perfil institucional de una Persona.
- `ciclos_escolares`, `grados`, `jornadas` y `secciones`: estructura académica.
- `periodos_matricula` y `matriculas`: admisión e histórico de matrícula.

Las secciones son ediciones concretas por ciclo. Matrícula conserva el hecho
histórico y deriva el grado desde Sección. Sus reglas se detallan en
[ACADEMICO.md](ACADEMICO.md) y [HISTORICO.md](HISTORICO.md).

### Responsables

- `responsables`: perfil institucional de una Persona responsable.
- `alumno_responsable`: relación N:M y atributos propios del vínculo.

## Módulos futuros

Solo se documentan; este bloque no crea sus tablas ni permisos operativos.

- **Docencia:** docentes, asignaturas, asignaciones docentes y horarios.
- **Aula virtual:** tareas, entregas, calificaciones y comentarios.
- **Comunicación:** avisos y notificaciones.
- **Finanzas:** cargos y pagos.
- **Auditoría:** eventos y trazabilidad transversal.

Estos módulos reutilizarán `personas`, el contexto institucional y RBAC. Una
futura tabla no debe almacenar nombres, correos, roles o instituciones que ya
puedan resolverse mediante una FK estable.

La frontera para clientes Supabase se define en
[SUPABASE_SECURITY.md](SUPABASE_SECURITY.md): lectura por RLS y operaciones
transaccionales por RPC autorizada.

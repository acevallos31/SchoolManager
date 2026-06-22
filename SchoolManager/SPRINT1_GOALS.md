# Sprint Goals · Semana 1 · SchoolManager

## 🏃 Sprint 1 · Días 1-7
**Goal:** *"El Administrador puede registrar alumnos, matricularlos y gestionar todos los pagos mensuales."*

---

## User Stories del Sprint 1

| US | Historia | Épica | Prioridad |
|---|---|---|---|
| US1 | Registrar nuevo alumno | Gestión de Matrícula | Alta |
| US2 | Registrar matrícula por ciclo escolar | Gestión de Matrícula | Alta |
| US3 | Buscar alumno por nombre, identidad o grado | Gestión de Matrícula | Alta |
| US5 | Registrar pago de mensualidad | Gestión de Mensualidades | Alta |
| US6 | Listar mensualidades pendientes y vencidas | Gestión de Mensualidades | Alta |

---

## Tareas técnicas por día

### Día 1-2 · Setup e infraestructura
- [ ] Configurar proyecto Angular con estructura de carpetas
- [ ] Configurar proyecto .NET Core 10 con autenticación JWT
- [ ] Verificar conexión con Supabase (tablas ya creadas)
- [ ] Configurar CORS entre Angular y .NET

### Día 3-4 · US1 y US2 · Registro de alumnos y matrículas
- [ ] Crear formulario de registro de alumno en Angular
- [ ] Implementar endpoint POST /api/alumnos en .NET
- [ ] Implementar endpoint POST /api/matriculas en .NET
- [ ] Conectar formulario con la API

### Día 5 · US3 · Búsqueda de alumnos
- [ ] Crear componente de listado con barra de búsqueda
- [ ] Implementar filtros por nombre, identidad y grado
- [ ] Implementar endpoint GET /api/alumnos con filtros

### Día 6 · US5 y US6 · Pagos y mensualidades
- [ ] Crear formulario de registro de pago
- [ ] Implementar endpoint POST /api/pagos
- [ ] Crear listado de mensualidades pendientes/vencidas
- [ ] Implementar filtros de estado en el panel

### Día 7 · Pruebas y ajustes
- [ ] Pruebas manuales de todos los flujos del Sprint 1
- [ ] Corrección de errores encontrados
- [ ] Commit final con tag v0.1

---

## Criterio de éxito del Sprint 1
✅ El administrador puede ingresar un alumno nuevo
✅ Puede asignarlo a un grado y registrar su matrícula
✅ Puede registrar un pago de mensualidad
✅ Puede ver qué alumnos tienen pagos pendientes o vencidos
✅ Todo sin usar Excel

---

## Definition of Done (DoD) Sprint 1
- Código subido al repositorio con commits descriptivos
- Endpoints responden correctamente en Swagger
- Formularios validan campos obligatorios
- Datos se guardan correctamente en Supabase
- Probado manualmente por el desarrollador

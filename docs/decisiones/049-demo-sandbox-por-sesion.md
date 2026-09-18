# 049 — Demo pública con sandbox aislada por sesión

Estado: **DECISIÓN ARQUITECTÓNICA APROBADA — 049A EN PREPARACIÓN**

Rama inicial: `feature/demo-sandbox-049`.

## 1. Objetivo

Permitir que cualquier visitante pruebe SchoolManager con flujos reales de alumnos,
responsables, matrícula, cargos, pagos y documentos sin compartir un dataset mutable
con otros visitantes y sin tocar datos productivos ajenos a su sandbox.

La Demo pública usará una **institución sandbox temporal por sesión Demo**. No se
usará un único dataset compartido mutable y tampoco se limitará la Demo a solo lectura.

## 2. Decisión principal

Cada ingreso mediante **Probar Demo** crea o recupera una `demo_session` vigente y
trabaja dentro de una institución temporal aislada.

Modelo conceptual:

```text
DEMO_TEMPLATE
      |
      | clonado controlado
      v
DEMO_SANDBOX / demo_session
      |
      +-- alumnos
      +-- responsables
      +-- matrículas
      +-- cargos
      +-- pagos
      +-- estructura académica
      +-- configuración demo permitida
```

No se clona físicamente una base PostgreSQL completa. Se clonan únicamente los datos
de negocio necesarios de una institución plantilla, conservando el modelo
multiinstitución vigente mediante `institucion_id`.

## 3. Invariantes del bloque

1. Dos sesiones Demo distintas nunca deben leer ni modificar datos una de la otra.
2. Ninguna operación Demo debe afectar una institución normal.
3. `DEMO_TEMPLATE` es fuente de seed y no es editable desde la UI Demo.
4. Refrescar el navegador conserva la misma sandbox mientras la sesión siga vigente.
5. Otro navegador/incógnito obtiene otra sandbox.
6. El usuario Demo entra directamente al dashboard; no debe terminar en
   `/acceso-pendiente`.
7. OAuth Google/Microsoft y el login normal no se modifican para implementar Demo.
8. El frontend no decide el `institucion_id` de una sandbox arbitraria; el backend
   resuelve el contexto autorizado de la sesión.
9. La expiración/limpieza nunca debe borrar datos de instituciones normales.
10. La Demo puede ejecutar operaciones funcionales reales, pero no operaciones de
    plataforma/infraestructura, secretos, OAuth global ni administración de otros
    tenants.

## 4. Tipos institucionales

La implementación deberá distinguir conceptualmente:

- `NORMAL`: institución real.
- `DEMO_TEMPLATE`: plantilla protegida usada únicamente para generar sandboxes.
- `DEMO_SANDBOX`: institución temporal asociada a una sesión Demo.

La forma exacta de persistir esta clasificación debe respetar el esquema vigente y se
cerrará en 049B después de revisar la migración 045 pendiente de 048. No reutilizar un
campo existente con semántica distinta solo para evitar una migración.

## 5. Ciclo de vida de Demo

### Crear

`POST /api/demo/session` será la operación de aplicación responsable de:

1. validar rate limit y precondiciones;
2. reutilizar una sesión Demo vigente cuando corresponda;
3. crear una sandbox temporal;
4. clonar un dataset pequeño y coherente desde `DEMO_TEMPLATE`;
5. asignar la identidad/sesión Demo al contexto sandbox;
6. emitir/establecer el contexto de sesión necesario;
7. devolver únicamente información segura para entrar al dashboard.

La transacción debe ser atómica: una sandbox incompleta no puede quedar publicada como
activa.

### Usar

Todas las operaciones existentes siguen pasando por API .NET + PostgreSQL/RPC y por el
contexto institucional normal. La Demo no introduce un segundo camino de negocio.

### Reiniciar

`POST /api/demo/session/reset` invalida la sandbox vigente y crea una nueva limpia.
No reconstruye manualmente módulo por módulo desde Angular.

### Expirar

Valor inicial propuesto:

- expiración por inactividad: **2 horas**;
- vida máxima: **24 horas**.

Son valores operativos configurables, no reglas de dominio permanentes.

### Limpiar

Un proceso de limpieza elimina exclusivamente sandboxes expiradas identificadas de
forma inequívoca como `DEMO_SANDBOX`.

La limpieza debe ser idempotente y segura frente a concurrencia. Antes de implementar
DELETE físico de cualquier dato Demo se debe demostrar que todas las filas objetivo son
temporales y pertenecen a la sandbox; el principio general del producto de conservar
históricos sigue vigente para instituciones reales.

## 6. Dataset mínimo de plantilla

La plantilla debe ser pequeña, determinista y útil para demostración:

- institución;
- ciclo y período de matrícula vigentes;
- grados, jornadas y secciones;
- varios alumnos en estados representativos;
- responsables y relaciones alumno-responsable;
- matrículas;
- conceptos financieros y plan de pago;
- cargos pendientes/parciales/pagados;
- pagos y aplicaciones representativas;
- configuración mínima necesaria para documentos.

No usar nombres, correos, teléfonos, direcciones ni datos de personas reales.
Usar datos sintéticos reservados para Demo.

## 7. Autenticación y acceso

Flujo objetivo:

```text
/login
  |
  +-- usuario/contraseña
  +-- Google
  +-- Microsoft
  +-- Probar Demo
          |
          v
   POST /api/demo/session
          |
          +-- crea/reutiliza sandbox
          +-- resuelve identidad Demo
          +-- establece contexto
          v
      /dashboard
```

`/acceso-pendiente` sigue reservado al caso real de una identidad autenticada sin
acceso institucional válido. No forma parte del flujo normal de Demo.

La sesión Demo no debe conceder capacidades globales. Debe usar permisos funcionales
suficientes para demostrar el producto dentro de su propia sandbox.

## 8. Seguridad y abuso

Controles mínimos antes de publicación:

- rate limiting para creación/reset de sandboxes;
- límite de registros creados por sandbox;
- tamaño máximo de archivos/documentos temporales;
- prohibición explícita de administración global;
- no exposición de secretos ni configuración OAuth;
- no acceso a otras instituciones por UUID conocido;
- protección CSRF/sesión según el mecanismo vigente;
- logs con `demo_session_id` e `institucion_id`, sin PII innecesaria;
- limpieza de archivos temporales junto con la sandbox.

## 9. Pruebas obligatorias

### DB/API

1. Demo A crea/modifica un alumno y Demo B no lo ve.
2. Demo A registra un pago y Demo B conserva sus saldos originales.
3. Demo A modifica un responsable y Demo B no cambia.
4. UUID de un recurso de otra sandbox devuelve 404/403 según el contrato vigente sin
   filtrar existencia.
5. una sandbox no puede escribir sobre `DEMO_TEMPLATE`.
6. una sandbox no puede escribir sobre una institución `NORMAL`.
7. reset crea un dataset limpio y deja inaccesible el anterior.
8. expiración invalida la sesión.
9. creación concurrente de la misma sesión no produce dos sandboxes activas.
10. fallo a mitad de clonación hace rollback completo.

### E2E

Abrir dos contextos de navegador independientes y demostrar que ambos pueden ejecutar
el mismo flujo financiero/académico sin contaminación cruzada.

## 10. Fases

### 049A — contrato y arquitectura

- decisión documentada;
- inventario del dataset;
- contrato de ciclo de vida;
- estrategia de pruebas;
- dependencia explícita respecto al cierre de 048.

### 049B — persistencia y clonación

- migración nueva con número asignado después de integrar 045;
- tabla/estado de sesiones Demo;
- clasificación segura de plantilla/sandbox;
- operación transaccional de clonación;
- validation + rollback;
- tests de aislamiento.

### 049C — API y sesión

- `DemoSandboxService`;
- endpoints de crear/reutilizar/reset;
- rate limiting;
- resolución de contexto;
- tests de concurrencia/autorización.

### 049D — frontend y E2E

- botón `Probar Demo`;
- entrada directa al dashboard;
- banner de Demo;
- acción `Reiniciar Demo`;
- E2E con dos sesiones simultáneas;
- expiración/UX.

## 11. Dependencia temporal con 048

Al abrir 049, el PR #119 / Bloque 048 todavía contiene la migración 045 y su propia
secuencia de despliegue productivo. Por eso **049A no asigna todavía número a una nueva
migración**.

No crear otra `045` ni saltarse la secuencia del runner. Tras cerrar 048 y tener 045
integrada en `main`, 049B tomará el siguiente número real disponible.

## 12. Fuera de alcance

- clonar una base PostgreSQL completa por visitante;
- usar producción para E2E destructivo;
- crear una arquitectura paralela de negocio para Demo;
- auto-link de identidades por correo;
- convertir Demo en un Superadministrador;
- reescribir pagos/cargos existentes;
- facturación fiscal o pasarela de pagos;
- exportación JSON general de reportes.

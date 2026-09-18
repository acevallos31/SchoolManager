# 049 — Demo pública aislada: entorno separado + sandbox por sesión

Estado: **049A CERRADA / 049B EN IMPLEMENTACIÓN**

Rama: `feature/demo-sandbox-049`.

## 1. Objetivo

Permitir que cualquier visitante pruebe SchoolManager con flujos reales de alumnos,
responsables, matrícula, cargos, pagos y documentos sin compartir un dataset mutable
con otros visitantes y sin mezclar datos o secretos con producción.

## 2. Decisión arquitectónica

049 **extiende y conserva** la decisión previa de
`docs/handoffs/042-entorno-demo-aislado.md`.

La Demo pública tendrá dos niveles de aislamiento:

1. **Entorno Demo separado de producción**:
   - frontend/deployment Demo dedicado;
   - API .NET Demo;
   - proyecto Supabase Demo independiente;
   - secretos, Auth y datos propios;
   - nunca reutiliza `DATABASE_URL`, service role, proyecto Supabase ni datos de
     producción.
2. **Sandbox institucional por sesión dentro del entorno Demo**:
   - cada visitante obtiene una institución temporal `demo_sandbox`;
   - se clona desde una `demo_template` protegida;
   - dos visitantes nunca comparten el mismo dataset mutable.

No se crea una base PostgreSQL por visitante. El aislamiento por visitante reutiliza el
modelo multiinstitución ya existente mediante `institucion_id`, pero siempre dentro
del proyecto Supabase Demo.

## 3. Topología

```text
Producción                             Demo pública
──────────                             ───────────
Frontend prod                          Frontend demo
API prod                               API demo
Supabase prod                          Supabase demo
instituciones normales                 |
                                       +-- DEMO_TEMPLATE
                                       |
                                       +-- sesión A -> DEMO_SANDBOX_A
                                       +-- sesión B -> DEMO_SANDBOX_B
                                       +-- sesión C -> DEMO_SANDBOX_C
```

Una fuga entre sandboxes es un defecto multi-tenant. Una conexión desde Demo hacia
producción es un defecto de despliegue crítico.

## 4. Clasificación institucional

La migración 046 agrega a `instituciones.tipo`:

- `normal`;
- `demo_template`;
- `demo_sandbox`.

El valor por defecto es `normal`. La misma migración puede existir en todos los
entornos, pero producción no crea plantillas ni sandboxes Demo.

## 5. Sesión Demo

`public.demo_sessions` relaciona:

- identidad Auth temporal (`auth_user_id`);
- institución sandbox;
- institución plantilla;
- estado;
- actividad y expiración.

Estados iniciales:

- `activa`;
- `expirada`;
- `reiniciada`;
- `cerrada`.

Invariantes de 046:

1. una identidad solo puede tener una sesión Demo activa;
2. una sandbox pertenece a una sola sesión;
3. la institución destino debe ser `demo_sandbox` activa;
4. la plantilla debe ser `demo_template` activa;
5. plantilla y sandbox deben ser distintas;
6. no se puede cambiar el tipo de una institución referenciada por sesiones Demo;
7. no se puede desactivar una institución con sesión Demo activa;
8. `demo_sessions` tiene RLS y no concede acceso directo a `anon` ni
   `authenticated`.

## 6. Identidad recomendada

Para la Demo pública se usará **Supabase Anonymous Sign-In en el proyecto Demo**.

Cada navegador obtiene un `auth.users.id` distinto mediante
`signInAnonymously()`, por lo que cada visitante puede vincularse a una sola sandbox
sin compartir un usuario Demo común.

Guardrails:

- anonymous sign-in se habilita únicamente en el proyecto Supabase Demo;
- comprobar el claim `is_anonymous` en el flujo Demo;
- no confiar en `user_metadata` para autorización;
- aplicar CAPTCHA/Turnstile y rate limiting antes de publicar;
- Google/Microsoft y login normal permanecen independientes;
- nunca exponer `service_role` al navegador;
- la limpieza de sandboxes y la limpieza de usuarios Auth anónimos son procesos
  separados y controlados.

No se habilita Anonymous Sign-In como parte de 049B; se hará al preparar el entorno Demo
y 049C.

## 7. Ciclo de vida

### Crear/reutilizar

El flujo objetivo de 049C será:

```text
/login
  |
  +-- Probar Demo
          |
          +-- signInAnonymously() en Supabase Demo
          |
          v
   POST /api/demo/session
          |
          +-- valida DemoModeEnabled
          +-- valida identidad anónima
          +-- reutiliza sesión vigente o clona sandbox
          +-- crea usuario/rol interno acotado para esa sandbox
          v
      /dashboard
```

La creación de la sandbox debe ser transaccional. Una clonación parcial se revierte por
completo.

### Usar

Los módulos existentes continúan por API .NET + PostgreSQL/RPC. Demo no crea un camino
paralelo para alumnos, matrículas, cargos o pagos.

### Reiniciar

`POST /api/demo/session/reset` cerrará la sesión actual como `reiniciada` y creará
una nueva sandbox limpia para la misma identidad.

### Expirar

Configuración inicial propuesta:

- inactividad: 2 horas;
- vida máxima: 24 horas.

Son valores configurables del entorno Demo.

## 8. Dataset de plantilla

La plantilla será pequeña y 100 % sintética. Debe incluir:

- configuración de institución e identificadores;
- ciclo, período, grados, jornadas y secciones;
- personas sintéticas;
- alumnos;
- responsables y relaciones;
- matrículas;
- conceptos financieros;
- planes y cuotas;
- cargos;
- pagos y aplicaciones representativas.

No copiar datos reales desde producción ni usar backups productivos para construir el
seed Demo.

## 9. Regla de clonado

`personas` y `usuarios` son globales, por lo que **no se comparten** entre
sandboxes.

Cada clon genera nuevos UUID para toda entidad mutable y mantiene mapas
`origen -> destino` durante la transacción.

Campos con unicidad global que no pueden copiarse literalmente:

- `personas.numero_identificacion_normalizado`: regenerar o dejar nulo según el caso
  sintético;
- `alumnos.rne`: regenerar o dejar nulo;
- `pagos.numero_recibo`: dejar que la secuencia genere un nuevo valor;
- cualquier identidad Auth: nunca clonar `auth_user_id`;
- referencias externas de pago: regenerar/nullificar cuando corresponda.

Campos cuya unicidad ya está acotada por institución, como
`alumnos.codigo_interno`, pueden conservar su valor al clonarse hacia otra
institución.

## 10. Qué se clona y qué no

Orden conceptual:

1. institución sandbox;
2. configuración de identificadores;
3. ciclos y períodos;
4. grados y jornadas;
5. secciones;
6. conceptos financieros;
7. planes de pago y cuotas;
8. personas sintéticas;
9. alumnos y responsables;
10. vínculos alumno-responsable;
11. usuario interno Demo + rol institucional acotado;
12. matrículas;
13. cargos;
14. pagos;
15. aplicaciones de pago.

No se clonan:

- `auth.users` de la plantilla;
- roles globales;
- `platform_admin`;
- permisos globales;
- invitaciones de acceso;
- auditoría histórica;
- `schema_migrations`;
- secretos;
- configuración OAuth;
- datos productivos.

## 11. Seguridad

Antes de publicación:

- `DemoModeEnabled=false` por defecto y obligatoriamente en producción;
- deployment Demo separado;
- CAPTCHA/Turnstile;
- rate limiting de creación/reset;
- límite de registros por sandbox;
- límites de archivos/documentos temporales;
- sin administración global;
- pruebas de UUID cruzado entre sandboxes;
- logs con `demo_session_id` e `institucion_id`;
- cleanup idempotente;
- ningún endpoint Demo disponible cuando el feature flag está apagado.

## 12. Pruebas obligatorias

### DB/API

1. Demo A crea/modifica alumno y B no lo ve.
2. A registra pago y B conserva sus saldos.
3. A cambia responsable y B no cambia.
4. UUID de otra sandbox no filtra existencia.
5. sandbox no puede escribir sobre `demo_template`.
6. sandbox no puede escribir sobre institución `normal`.
7. reset produce dataset limpio.
8. expiración invalida sesión.
9. creación concurrente no produce dos sandboxes activas.
10. fallo durante clonación hace rollback completo.
11. usuario Demo no obtiene permisos globales.
12. producción con `DemoModeEnabled=false` rechaza el flujo Demo.

### E2E

Dos contextos de navegador independientes ejecutan el mismo flujo académico/financiero
y demuestran aislamiento.

## 13. Fases

### 049A — arquitectura
**CERRADA.**

- conserva el aislamiento de infraestructura de 042;
- agrega sandbox por sesión;
- define identidad anónima como estrategia preferida;
- inventaría restricciones de clonado.

### 049B — persistencia
**EN IMPLEMENTACIÓN.**

- migración `046_demo_sandbox_sesiones.sql`;
- clasificación institucional;
- `demo_sessions`;
- RLS/revokes;
- validation/rollback;
- tests de invariantes.

049B no crea la plantilla ni clona datos todavía.

### 049C — clonación + API

- servicio transaccional de clonación;
- creación del usuario/rol Demo;
- `DemoModeEnabled`;
- endpoints create/reuse/reset;
- rate limiting;
- pruebas de aislamiento.

### 049D — frontend + E2E + cleanup

- `Probar Demo`;
- sesión anónima;
- entrada directa a dashboard;
- banner/reinicio;
- E2E paralelo;
- expiración y limpieza.

## 14. Estado de migraciones

Al comenzar 049B se verificó:

- `045_operacion_vinculacion_identidad_autorizada.sql` ya está en `main`;
- la migración 045 también está aplicada en el Supabase productivo;
- por tanto 049B usa el siguiente número disponible: **046**.

049 no ejecuta la 046 en producción automáticamente.

## 15. Fuera de alcance

- una DB por visitante;
- Demo pública en la DB productiva;
- backups productivos como seed;
- bypass de API/RPC;
- auto-link por correo;
- Superadministrador público;
- reescritura de invariantes financieras;
- facturación fiscal/pasarela;
- exportación JSON general.

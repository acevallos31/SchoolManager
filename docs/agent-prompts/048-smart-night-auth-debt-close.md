# Hermes Smart Night — 048: autenticación pendiente + cierre de deuda técnica

## Contexto y objetivo

Trabaja en **modo Smart durante el resto de la noche (~6–8 horas efectivas)** sobre SchoolManager.

Rama de trabajo obligatoria:

```
chore/048-night-auth-debt-close
```

Base al crear esta rama: `main` después del merge de 047A (PR #118).

No dediques el turno a re-descubrir el repositorio. Ya existe suficiente contexto. Haz un preflight breve y empieza a ejecutar.

Objetivo general:

1. diagnosticar y corregir el bug actual de autenticación/vinculación que afecta al usuario Demo y completar el flujo administrativo de identidades pendientes;
2. cerrar, en lotes pequeños y verificables, la deuda técnica #15 / issue #109 de RPC `SECURITY DEFINER` expuestas innecesariamente a `authenticated`;
3. avanzar y, si es posible de forma segura, cerrar la deuda #14 / issue #85 de prueba de carga backend **sin usar producción para carga**;
4. dejar documentación canónica actualizada, pruebas verdes, commits temáticos y push;
5. NO hacer merge.

La prioridad es calidad y cierre real, no cantidad de cambios.

---

## Reglas no negociables

### Seguridad / producción

- **NO hagas merge a `main`.**
- **NO ejecutes migraciones ni DDL en Supabase/producción.**
- **NO modifiques datos reales de producción.**
- **NO hagas pruebas de carga contra producción/Render productivo.**
- **NO uses ni imprimas secretos, JWT, access tokens, refresh tokens, cookies, service_role, contraseñas ni credenciales.**
- **NO vincules identidades automáticamente por coincidencia de correo.**
- Google/Microsoft OAuth que ya funciona no debe romperse.
- Los roles/permisos siguen siendo autoridad de SchoolManager, no del proveedor OAuth.
- Mantén compatibilidad single-institution y multi-institution.
- No relajes RLS, RBAC, Quality Gates, cobertura ni Sonar para conseguir verde.

### Arquitectura

Mantén el estándar del proyecto:

```
Angular
   ↓
API .NET
   ↓
capa de aplicación / servicios
   ↓
PostgreSQL / RPC / RLS
```

- lógica de negocio en backend;
- controllers delgados;
- frontend solo presentación/orquestación;
- SOLID/DIP;
- transacciones ACID donde haya más de una escritura relacionada;
- autorización siempre server-side;
- no introducir acceso de negocio directo desde Angular a Supabase;
- Supabase directo en frontend sigue reservado a Auth.

---

# FASE 0 — Preflight máximo 15 minutos

Lee únicamente lo necesario:

1. `AGENTS.md`
2. `docs/engineering-principles.md`
3. `docs/technical-debt.md`
4. `docs/HANDOFF.md`
5. `docs/handoffs/040-oauth-google-avance.md`
6. `docs/handoffs/042F-identidad-registro-responsables.md`
7. `docs/handoffs/046a-alta-usuarios-invitacion-plan.md`
8. `docs/handoffs/047A-documentos-recibo-pago.md`

Confirma rama y árbol limpio.

Ejecuta un baseline rápido de tests solo si no hay evidencia reciente suficiente. No gastes una hora en inventario general.

Después **empieza a modificar código**.

---

# FASE 1 — Bug actual de autenticación / usuario Demo

## Evidencia ya confirmada manualmente

En Supabase producción se verificó para `demo@schoolmanager.com`:

- existe identidad en `auth.users`;
- `auth.users.id = a6a3d715-2418-4fb5-b95c-072e0ce216cf`;
- existe `public.usuarios` correspondiente;
- `public.usuarios.activo = true`;
- `public.usuarios.auth_user_id` coincide exactamente con ese UUID;
- existe una asignación `usuarios_roles` activa con `institucion_id` no nulo;
- sin embargo la UI/login reporta:
  `IDENTIDAD_NO_VINCULADA` / “Tu identidad externa no está vinculada…”.

Por tanto:

**NO hagas UPDATE de auth_user_id.**
**NO recrees el usuario.**
**NO reasignes el rol a ciegas.**

El bug debe diagnosticarse de extremo a extremo.

## Investigar en este orden

### A. Backend `/api/auth/me`

Revisa:

- `UsuarioActualService`;
- extracción del claim `sub`;
- validación JWT issuer/audience;
- conexión PostgreSQL usada por producción;
- manejo de `IdentidadNoVinculadaException`;
- cualquier cache/sesión intermedia.

Construye pruebas que demuestren:

1. JWT con `sub` vinculado → usuario resuelto;
2. usuario activo + rol institucional activo → acceso aplicable;
3. `sub` diferente → `IDENTIDAD_NO_VINCULADA`;
4. no confundir usuario sin rol con identidad no vinculada.

### B. Vercel/Auth frontend

Revisa:

- `api/auth/session.ts`;
- `AuthService.restaurarSesion()`;
- `asegurarUsuarioInicial()`;
- `/auth/callback`;
- sincronización cookie ↔ access token ↔ `/api/auth/me`;
- almacenamiento/mensaje de sesión inválida;
- posibilidad de que un error viejo se conserve y reaparezca aunque la sesión actual sea válida.

Hipótesis importante a probar:
un mensaje previo `IDENTIDAD_NO_VINCULADA` puede estar persistiendo en frontend/cookie/session state y mostrarse después, aunque el usuario ya esté vinculado.

Otra hipótesis:
el backend productivo podría apuntar a una base/proyecto distinto del Supabase Auth que emitió el JWT. No revelar connection strings; verifica mediante configuración segura, tests/config validation o fingerprint no secreto si es posible.

### C. Error semántico

Si un usuario está vinculado pero carece de rol/permisos, debe recibir un error distinto y accionable; nunca `IDENTIDAD_NO_VINCULADA`.

No cambies semántica HTTP sin pruebas y documentación.

## Resultado esperado de FASE 1

- causa raíz demostrada con evidencia;
- test de regresión que falla antes / pasa después;
- fix mínimo;
- build/tests verdes;
- documentación del incidente;
- ningún cambio manual a datos productivos.

---

# FASE 2 — Completar aprobación administrativa de identidades pendientes

Existe una brecha funcional confirmada en:

`Configuración > Seguridad y acceso`

Actualmente:

- `Activo` = `usuarios.activo`;
- `Pendiente` = `usuarios.auth_user_id IS NULL`;
- un superadmin puede editar persona y roles, pero **no existe acción para aprobar/vincular una identidad externa pendiente**.

Implementa una solución segura **sin vincular por correo automáticamente**.

## Modelo esperado

Una identidad externa autenticada pero no vinculada debe poder generar/representar una solicitud pendiente con:

- `auth_user_id` externo;
- proveedor si puede obtenerse de forma segura;
- correo solo como dato informativo/no autoritativo;
- fecha;
- estado;
- usuario interno candidato solo si el operador lo selecciona explícitamente.

El operador autorizado debe poder:

- ver la solicitud;
- seleccionar explícitamente el usuario SchoolManager;
- aprobar;
- rechazar/cancelar;
- auditar la operación.

La aprobación debe reutilizar la semántica de:

```
public.vincular_identidad_usuario(p_usuario_id, p_auth_user_id)
```

No dupliques su lógica.

## Permisos

Diseña capacidad explícita, preferiblemente alineada con:

- `identidad.usuarios.editar`;
- o un permiso nuevo específico si la arquitectura realmente lo requiere.

No conviertas `platform_admin` en bypass oculto fuera de los patrones RBAC existentes.

## UI

En lugar de “Habilitar cuenta”, usar lenguaje correcto:

- Estado de cuenta: Activo/Inactivo
- Identidad: Vinculada / Pendiente de vinculación

Para solicitudes reales:

- `Aprobar vinculación`
- `Rechazar`

La UI no debe recibir IDs externos innecesarios si pueden mantenerse opacos en backend.

## Migraciones

Si esta fase necesita tablas/RPC nuevas:

- crea la siguiente migración versionada disponible;
- agrega validation + rollback siguiendo estándar del repo;
- agrega DB tests;
- **NO la ejecutes en producción**.

---

# FASE 3 — Cerrar deuda #15 / issue #109

Deuda canónica:

```
Auditoría de RPC SECURITY DEFINER expuestas a authenticated
```

Hay decenas de funciones históricas. No hagas una revocación masiva.

## Estrategia

Crea un inventario versionado, por ejemplo:

```
docs/security/security-definer-inventory.md
```

Por cada RPC SECURITY DEFINER relevante registra:

- función/firma;
- módulo;
- consumidor real;
- si se llama desde Angular/Data API;
- si se llama únicamente desde API .NET;
- grant actual;
- necesidad real de SECURITY DEFINER;
- clasificación:
  - KEEP_AUTHENTICATED
  - API_ONLY_REVOKE_AUTHENTICATED
  - CONVERT_SECURITY_INVOKER
  - MOVE/HIDE
  - JUSTIFIED_EXCEPTION
- riesgo;
- evidencia/test requerido.

## Orden de trabajo

Procesa grupos pequeños:

1. identidad/RBAC;
2. configuración;
3. alumnos/responsables;
4. matrículas;
5. finanzas;
6. restantes.

Antes de revocar una función:

- busca consumidores reales;
- confirma que frontend no la usa directamente;
- prueba API .NET;
- agrega test negativo de ejecución directa si corresponde;
- agrega test positivo por backend.

## Hardening

Si detectas RPC API-only:

- prepara migración incremental;
- revoke `authenticated`;
- conserva acceso mínimo necesario;
- evita cambiar lógica funcional;
- añade validation y rollback;
- tests DB/API.

**NO aplicar en producción.**

## Criterio de cierre #15

Solo marca deuda #15 RESUELTA si:

- inventario completo versionado;
- todas las RPC expuestas clasificadas;
- exposición innecesaria corregida en migraciones versionadas;
- excepciones justificadas explícitamente;
- suite DB/API verde;
- Security Advisor puede quedar limpio o con excepciones justificadas documentadas.

Si no puedes completar todo antes del final de la noche, no inventes cierre. Deja exactamente qué firmas faltan.

---

# FASE 4 — Deuda #14 / issue #85: carga backend

No ejecutar carga contra producción.

Primero inspecciona si el repo ya tiene scripts/documentación de loader/perf.

Objetivo seguro:

- entorno local/staging efímero;
- `/health`;
- `/health/ready`;
- una ruta autenticada read-only;
- identidad exclusiva de prueba;
- dataset sintético;
- escalado progresivo;
- métricas:
  - RPS;
  - p50/p95/p99;
  - errores;
  - CPU/memoria si están disponibles;
  - saturación/conexiones DB;
- condiciones de stop.

Si existe staging seguro capaz de ejecutar la prueba sin afectar usuarios reales, úsalo siguiendo guardrails existentes.

Si el criterio de cierre canónico exige una medición sobre Render productivo y no hay autorización, **NO la ejecutes**. En ese caso:

- deja scripts listos;
- valida local/staging;
- documenta resultados;
- marca únicamente lo que realmente quedó cerrado;
- deja un comando/runbook explícito para la prueba productiva futura autorizada.

No falsifiques el cierre de #14.

---

# FASE 5 — Documentación obsoleta

Después de código/tests:

- sincroniza `docs/technical-debt.md`;
- sincroniza `docs/HANDOFF.md`;
- sincroniza `docs/AI_CONTEXT.md`;
- corrige README si todavía afirma que el proyecto llega solo a migración 018 o que pagos/portal no tienen backend;
- agrega un handoff nocturno específico:
  `docs/handoffs/048-auth-debt-night-close.md`.

No reescribas historia. Diferencia:
- cerrado;
- parcialmente cerrado;
- bloqueado por autorización;
- funcionalidad futura.

---

# FASE 6 — Calidad obligatoria

Antes de push final:

```
git diff --check
```

Backend:

```
dotnet build backend/SchoolManager.API/SchoolManager.API.csproj --configuration Release
dotnet test tests/SchoolManager.API.IntegrationTests/SchoolManager.API.IntegrationTests.csproj --configuration Release
dotnet test tests/SchoolManager.Database.IntegrationTests/SchoolManager.Database.IntegrationTests.csproj --configuration Release
```

Frontend:

```
cd frontend/schoolmanager-frontend
npm ci --ignore-scripts
npm test -- --watch=false
npm run build
npm run test:vercel
```

Ejecuta cualquier suite E2E relevante si el entorno seguro lo permite.

Revisa:

- cobertura;
- Quality Gate / Sonar;
- secretos;
- RLS/RBAC;
- aislamiento institucional;
- OAuth Google/Microsoft;
- errores 401/403 correctos;
- que no se haya reintroducido Supabase de negocio en frontend.

No uses `NOSONAR`, exclusiones nuevas o reducción de thresholds como atajo.

---

# Política de commits

Haz commits temáticos, no un mega-commit caótico. Sugerencia:

1. `fix(auth): corregir resolución de identidad vinculada`
2. `feat(identity): aprobar vinculaciones pendientes desde seguridad`
3. `chore(security): inventariar security definer`
4. `chore(security): hardening incremental rpc api-only`
5. `perf(test): baseline seguro backend`
6. `docs: cerrar deuda técnica y handoff 048`

No hace falta usar exactamente esos mensajes si el trabajo real difiere.

Haz push de la rama.

Puedes abrir PR al final si el cambio está coherente y los tests están verdes.

**NO MERGE.**

---

# Cómo evitar otra sesión de 7 minutos

Este prompt está diseñado para ejecución, no exploración.

Reglas de trabajo:

1. preflight máximo 15 min;
2. no vuelvas a hacer búsquedas generales una vez identificado el archivo/módulo;
3. cuando un subproblema esté claro, implementa + prueba inmediatamente;
4. si una línea queda bloqueada por autorización externa, documenta el bloqueo y pasa a la siguiente fase;
5. no consumas iteraciones repitiendo `git status`, `grep` o inventarios que ya hiciste;
6. agrupa lecturas y comandos independientes;
7. agrupa cambios relacionados;
8. crea checkpoint documental antes de aproximarte al límite de herramientas.

Si el runtime de Hermes impone un límite de iteraciones:

- antes de agotarlo, guarda en
  `docs/agent-runs/048-night-checkpoint.md`
  el SHA, fase actual, pruebas, fallos, siguiente comando y archivos pendientes;
- commit + push del checkpoint;
- si Hermes puede iniciar otra sesión Smart automáticamente, continúa leyendo este mismo prompt + checkpoint;
- si no puede, termina con una instrucción corta exacta para reanudar sin repetir reconocimiento.

**No conviertas un límite de herramientas en “trabajo terminado”.**

---

# Presupuesto aproximado de la noche

- 0:00–0:15 preflight
- 0:15–1:45 bug Demo/auth + pruebas
- 1:45–3:15 aprobación administrativa de identidad pendiente
- 3:15–5:30 deuda #15 inventario + hardening por lotes
- 5:30–6:30 deuda #14 local/staging segura
- 6:30–7:15 suites completas + correcciones
- 7:15–8:00 documentación, CI, PR/handoff

Si una fase termina antes, usa el tiempo sobrante en la siguiente.

---

# Stop conditions

Detente únicamente ante:

- necesidad de secreto no disponible;
- necesidad de cambio destructivo en producción;
- necesidad de ejecutar DDL/migración en producción;
- riesgo de romper OAuth Google/Microsoft sin prueba suficiente;
- regla financiera/académica que requiera decisión funcional humana;
- operación de carga que pueda afectar usuarios reales.

En esos casos documenta el bloqueo y continúa con trabajo seguro restante.

---

# Entrega final obligatoria

Al terminar reporta:

## Resumen
- qué quedó corregido;
- qué deuda quedó cerrada;
- qué quedó pendiente.

## Auth
- causa raíz del problema Demo;
- pruebas que la demuestran;
- SHA del fix;
- estado del flujo de identidades pendientes.

## Deuda #15
- número total de RPC auditadas;
- clasificación;
- RPC endurecidas;
- migraciones creadas pero NO aplicadas;
- excepciones justificadas.

## Deuda #14
- entorno usado;
- comandos;
- resultados;
- si queda pendiente producción, por qué.

## Calidad
- API tests;
- DB tests;
- frontend tests;
- build;
- E2E;
- Sonar/Quality Gate;
- CI URLs/IDs si existen.

## Git
- rama;
- commits;
- SHA final;
- PR si fue abierto.

## Producción
Escribe literalmente:

```
MERGE: NO
PRODUCTION DB CHANGES: NO
PRODUCTION DATA CHANGES: NO
PRODUCTION LOAD TEST: NO
```

salvo que algo externo al agente ya hubiese ocurrido; en ese caso descríbelo, pero el agente no debe ejecutarlo.

## Próximos pasos
Máximo 3, concretos y accionables.

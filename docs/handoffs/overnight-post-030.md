# Overnight post-030 — Hallazgos, RBAC, E2E-01 y estado CD

Fecha: 2026-09-08 · Rama: `docs/overnight-post-030` (solo documentación, sin mergear).
Contenido: estado del fix de change detection; hallazgos H1/H2/M3/M4; endpoints legacy;
inconsistencias RBAC reales (verificadas en código); checklist E2E-01; decisiones pendientes;
siguiente bloque recomendado.

> Regla aplicada: **solo se registra lo confirmado leyendo el código real.** Todo lo que un
> subagente no pudo verificar por lectura queda marcado `[NO CONFIRMADO]` y NO entra como deuda.

---

## 1. Estado del fix de change detection (CD)

- **PR #55 MERGEADO a `main` hoy 07:51Z** (merge `a52945a`) por el usuario. No está abierto.
- Fix que quedó en producción: **`provideZoneChangeDetection()` central en `app.config.ts`** +
  `alumnos.css` + specs. Es decir, se restauró el CD **basado en zona**.
- El trabajo zoneless (revertir `provideZoneChangeDetection()`, `markForCheck()` en
  Alumnos/Matrículas) **NO está en main**; quedó stashado en `wip-cd-zoneless-20260908`.
- **Regresión/limitación del repro honesto**: el test de Alumnos pasaba sin el fix
  (cargando=false + dato visible tras resolver) porque `zone.js` está en los polyfills de
  `angular.json` (L22–23, `"polyfills": ["zone.js"]`), lo que enmascara el modelo zoneless.
- **CI/Sonar/Vercel del merge**: verde — validate/deploy/sonar success (run `34201411418`);
  deploy de producción de Vercel para `a52945a` OK. SonarCloud QG pasado (cobertura New Code 86,5%).
- **Decisión pendiente (requiere aprobación del usuario):** producción corre hoy con CD por
  zona. ¿Se acepta ese PR como válido (fin del tema), o se reabre la migración a zoneless real?
  Si se reabre, hay que resolver antes el `zone.js` en polyfills del entorno de test para que
  el repro sea honesto.

---

## 2. Hallazgos H1/H2/M3/M4

> Los códigos H1–H4 son nomenclatura de tracking interna del asistente; no existen como strings
> en el repo. Reflejan deuda/endpoints legacy **confirmados en código**.

- **H1 — Factories viejas**: `CargosApiFactory` / `MatriculasApiFactory` (vestigiales, bloque 020).
  Referencia: `docs/technical-debt.md` § (factories).
- **M3 — Endpoint legacy muerto**: `POST /api/matriculas/registrar`
  (`MatriculasController.cs`), reemplazado por el flujo `Create` + RPC.
- **M4 — Reuso de RPC en Alumnos**: `AlumnosController.cs` (≈L185) reutiliza
  `rpc_crear_alumno_nueva_persona_con_documento` (no reimplementa la creación).

**Endpoints / configuración sin staging (confirmado):**
- Backend: solo existe `appsettings.json` (apunta a prod); sin `appsettings.Staging.json`.
- Frontend: solo `environment.ts` / `environment.prod.ts`; sin `environment.staging.ts`
  ni configuración `staging` en `angular.json` (solo `production`/`development`).
- CI: 3 workflows (`deploy.yml`, `hermes-auto-health.yml`, `hermes-control.yml`); sin job E2E
  on-demand; únicos secrets de CI = `SONAR_TOKEN` y `RENDER_DEPLOY_HOOK_URL` (nivel repo, prod).

---

## 3. Inconsistencias RBAC reales (verificadas en código)

Matriz compacta de la auditoría de 4 capas ([Authorize] .NET / Angular / catálogo SQL / requisitos
internos de RPC). Solo filas confirmadas leyendo archivos reales.

| # | Permiso/Área | Dónde se define | Dónde falta/usa | Tipo | Sev. |
|---|---|---|---|---|---|
| F1 | Estructura académica — doble exigencia `academico.estructura.*` vs `configuracion.*` | .NET: `EstrucAcademicaController.cs:22,55,78,…` (Permisos.cs:30-32) | RPC internas exigen `configuracion.grados/jornadas/secciones.*` — 016:46,52,60,68,104,126,136,141 y 020:171,189,… | Nombres inconsistentes entre capas: mismo endpoint sujeto a 2 familias de permiso | ALTA |
| F2 | Ciclos/períodos — doble exigencia `academico.ciclos.*` vs `configuracion.ciclos.*`/`periodos_matricula.*` | .NET: `CiclosEscolaresController.cs:22,58,83,…` (Permisos.cs:22-25) | RPC internas exigen `configuracion.ciclos.*`/`configuracion.periodos_matricula.*` — 014:56,57,73,135,136 y 015:17,42,64 | Nombres inconsistentes (academico vs configuracion) | ALTA |
| F3 | Frontend evalúa permiso distinto al backend (Ciclos) | Backend exige `academico.ciclos.ver` (CiclosEscolaresController.cs:23) | Frontend usa `configuracion.ciclos.ver` (configuracion.ts:58) y `configuracion.ciclos.*`/`periodos_matricula.*` (configuracion-ciclos.ts:15-16); ruta `/configuracion/ciclos` sin guard de permiso (app.routes.ts:52) | Divergencia frontend-vs-backend | ALTA |
| F4 | Frontend evalúa permiso distinto al backend (Estructura académica) | Backend exige `academico.estructura.ver` (EstrucAcademicaController.cs:22-24) | Frontend usa `configuracion.grados/jornadas/secciones.*` (configuracion-estructura-academica.ts:19-21, configuracion.ts:62-64); ruta sin guard (app.routes.ts:54-55) | Divergencia frontend-vs-backend | ALTA |
| F5 | `configuracion.sistema.ver` sin contraparte .NET | SQL: 012:26 (seed), grant admin (001:1432) | Solo frontend: app-shell.ts:63, dashboard.ts:29; `Permisos.cs` solo `configuracion.sistema.editar` (L7) | Permiso en SQL+Angular sin constante .NET | MEDIA |
| F6 | `responsables.responsables.*` huérfana/duplicada | 007:119-121 (seed) y 007:149-151 (rol operador) | Ninguna capa la exige; se usa `academico.responsables.*` (ResponsablesController, 009:113, responsables.ts:71-72) | Catálogo+grant sin uso real; duplicación de nomenclatura | MEDIA |
| F7 | `academico.matriculas.editar` y `.anular` huérfanos | 007:117-118, 001:422-423 (solo admin) | Ninguna capa los usa; .NET Matriculas solo ver/crear/cambiar_estado (Permisos.cs:37-39) | Permisos de catálogo sin uso ni rol no-admin | BAJA |
| F8 | `academico.secciones.*` residual viva en RLS, duplicada de `configuracion.secciones.*` | 009:21-24 (seed), 009:40-42 (operador) | Sigue exigida solo por RLS/RPC antiguos (009:111,449-460; 020:172); capa actual usa `configuracion.secciones.*` (016:104-141, 020:257-288) | Legacy como condición de RLS; doble nomenclatura de secciones | MEDIA |
| F9 | **Roles sin grants sembrados**: cajero, consulta, usuario, padre, docente | Roles definidos 007:100-106, 001:406-412 | Solo `admin` (007:133-138) y `operador` (007:140-154 + 009:37-49) reciben `roles_permisos`; los otros 5 sin permisos → cargos/pagos operables solo por admin | Grants incompletos/ausentes | MEDIA |
| F10 | Operador "ve" pero no opera toda la pantalla de alumnos | Grants operador 007:144-151 + 009:40-49 (alumnos.*, matriculas.*, SIN cargos) | Frontend `alumnos.ts` refiere `academico.cargos.ver` (resumen financiero del expediente); operador no lo tiene | Grant incompleto rol-vs-pantalla | MEDIA |
| F11 | Guard inconsistente: `/configuracion/ciclos` y `/configuracion/estructura-academica` sin `data.permiso` | app.routes.ts:49-55 (solo autenticación) | El resto de pantallas sí llevan guard de permiso (app.routes.ts:39,46,60,66,72,78,84) | Estrategia de guard divergente en Angular (no brecha: backend autoriza por acción/RPC) | BAJA |

**Áreas coherentes (verificadas, sin hallazgo):** `configuracion.conceptos_financieros.*` y
`configuracion.planes_pago.*` alineadas en las 4 capas; flujos Alumnos/Matrículas/Responsables/
Cargos/Pagos usan `academico.*` consistentemente; los 35 códigos .NET tienen fila sembrada en SQL;
no hay string Angular inexistente en el catálogo SQL.

**NO_CONFIRMADO (no registrar como deuda):**
- F9: si en runtime existe una UI/administración que asigne `roles_permisos` fuera de los seeds
  (no localizada) — el hallazgo se ciñe al catálogo de seeds.
- F8: la doc RBAC no zanja cuál familia de secciones es canónica; se confirma la coexistencia y la
  doble condición, no cuál debiera sobrevivir.
- F10: que operador sin acceso financiero sea diseño o defecto es decisión; lo confirmado es la
  divergencia rol-vs-pantalla.

**Restricción respetada:** NO se rediseñó RBAC, NO se crearon migraciones. Los hallazgos de
duplicación de módulos (F1–F4) son candidatos a decisión de consolidación de nomenclatura, no a
cambio de código en este lote.

---

## 4. Checklist E2E-01 — Infraestructura staging (planificación)

Fuente oficial: `docs/testing/e2e-staging-plan.md`. Solo planificación; NO implementado.

**1. Proyecto Supabase staging (manual humano, no automatizable)**
- Crear proyecto Supabase nuevo; NUNCA el de prod (`pzhcpdznjoyukbhhodjz`).
- Anotar `SUPABASE_URL` y `SUPABASE_ANON_KEY` del staging (públicos); `service_role` solo para
  migración manual (secreto, no se versiona).
- Crear usuarios de prueba en Supabase Auth **staging**: admin A, admin B, solo-lectura, responsable.

**2. Migraciones a aplicar en staging (MANUALES — humano en SQL de Supabase)**
- `database/baseline/001_schoolmanager_fase1a.sql`
- Migraciones `001`…`024`, **incluidas `023` y `024`** (aún pendientes de aplicar manualmente,
  incluso en prod); sin ellas no corren los casos de ciclos/estructura.
- Opcional: checks de `database/migrations/validation/*.sql` (existen para 023/024).
- Rollback disponible en `database/migrations/rollback/`.

**3. Backend staging**
- Elegir Render dedicado o staging efímero en CI (`dotnet run --environment Staging`).
- Nunca compartir connection string ni `Jwt.Issuer` con prod.

**4. Frontend staging**
- Crear `environment.staging.ts` (production:false, supabaseUrl/anonKey y apiUrl del staging;
  solo valores públicos; `.gitignore` cubre `**/environment.secret.ts`).
- Config `staging` en `angular.json` (fileReplacements → `environment.staging.ts`).

**5. CORS / dominios**
- `appsettings.Staging.json` → `Cors.AllowedOrigins` = solo frontend staging + `http://localhost:4200`.
- NUNCA orígenes prod (`schoolmanager.vercel.app`, `school-manager-self.vercel.app`,
  `schoolmanager.nocpbx.com`).

**6. Orden de provisión**
1. Humano: Supabase staging + claves. 2. Humano: migraciones baseline+001–024.
3. Backend `appsettings.Staging.json` + fail-fast + health env; levantar backend.
4. `environment.staging.ts` + config `angular.json`; build `--configuration staging`.
5. Humano: GitHub Environment `staging` + secrets. 6. Guardrails (allowlist + `global-setup.ts`).
7. Validar.

**7. Validaciones (criterio "listo")**
- `auth.spec.ts` corre verde contra staging real sin tocar prod.
- Guardrail bloquea: `E2E_BASE_URL` de prod Y build con Supabase/API embebido de prod.
- Backend staging rechaza arrancar si su config coincide con valores de prod (fail-fast).
- Health reporta `Staging`.

**8. Rollback**
- Código: revertir commit de rama corta (git-reversible, no toca prod).
- GitHub env `staging`: borrar. Backend Render staging: apagar (independiente de prod).
- Supabase staging: descartable (drop/recrear); migraciones 023/024 tienen `rollback/*.sql`.

**9. Accionable por Hermes** (con PR): `environment.staging.ts` (placeholders), `angular.json`,
`appsettings.Staging.json`, `Program.cs` (fail-fast + `/health` con ambiente), reescribir
`playwright.config.ts` a allowlist, crear `e2e/global-setup.ts` y registrarlo.
**Requiere humano:** proyecto Supabase + claves, migraciones manuales, usuarios de prueba,
backend staging en Render, GitHub Environment `staging` + secrets, revisión del PR.

---

## 5. Matriz E2E → endpoint → permiso → fixture

El subagente (sa-1) confirmó por lectura **solo** Alumnos + PortalResponsable (rutas, verbos,
policies, RPC) y las policies genéricas de `Permisos.cs`/handler. La correspondencia
acción→policy de **MatriculasController (15 matches), CiclosEscolaresController (23) y
EstructuraAcademicaController (33)** quedó **sin leer** (solo conteo de atributos) →
**`[NO CONFIRMADO]`**. **No se inventan rutas ni permisos.**

- **Login correcto** → `POST /api/auth/login` (AuthController; [NO CONFIRMADO ruta exacta]) →
  Supabase Auth (`signInWithPassword` en `auth.ts`, único acceso directo restante) → admin A
  staging → 200 + token + dashboard.
- **Login incorrecto** → mismo endpoint → credenciales inválidas → 401/400.
- **No autenticado a ruta protegida** → redirect `/login`.
- **Alumnos** (confirmado) → AlumnosController (listar/crear) con `academico.alumnos.*` → RPC
  `rpc_crear_alumno_nueva_persona_con_documento` (AlumnosController:200) → admin A + seed alumno.
- **Matrícula / cambio de estado** → `[NO CONFIRMADO]` policies/RPC de MatriculasController.
- **Ciclos/períodos** → `[NO CONFIRMADO]` (ver F2: backend `academico.ciclos.*`, RPC
  `configuracion.ciclos.*`/`periodos_matricula.*`).
- **Estructura académica CRUD** → `[NO CONFIRMADO]` por acción; ver F1 (policy `academico.estructura.*`,
  RPC `configuracion.grados/jornadas/secciones.*`).
- **401** (API sin token), **403** (usuario solo-lectura), **404** (recurso inexistente),
  **409** (transición de estado no permitida), **cross-institución 403/404**, **token expirado/
  firma inválida** → pendientes de confirmar endpoint/policy concreto antes de E2E-05.

> **Acción pendiente**: completar la lectura de MatriculasController, CiclosEscolaresController y
> EstructuraAcademicaController (acción→policy→RPC) para cerrar la matriz E2E-05 sin investigación
> posterior. Es requisito antes de implementar E2E-05.

---

## 6. Inventario de secretos/variables de staging

Resumen (del subagente sa-2, leído en código real; sin valores):
- **Frontend (`environment.staging.ts`, compile-time, públicos):** `environment.production`,
  `supabaseUrl`, `supabaseAnonKey` (anon, NUNCA service_role), `apiUrl` — hoy apuntan a prod; valores
  staging FALTA. Config build `staging` en `angular.json` FALTA.
- **Backend (runtime):** `ASPNETCORE_ENVIRONMENT`=Staging (FALTA), `ConnectionStrings__PostgreSQL`
  (SECRETA, en Render staging/env; nunca commiteada), `Jwt__Issuer`/`Audience` (público, apunta a
  Supabase staging — FALTA), `Cors__AllowedOrigins` (solo staging + localhost), `AllowedHosts`.
- **Playwright:** `E2E_BASE_URL` (pública), `E2E_STAGING=1` (flag), `E2E_USER_EMAIL` (secreto),
  `E2E_USER_PASSWORD` (SECRETA), `E2E_ALLOW_PROD` (no usar).
- **CI:** `SONAR_TOKEN` (RESUELTO, repo secret), `RENDER_DEPLOY_HOOK_URL` (prod). GitHub Environment
  `staging` FALTA (contenedor de secrets E2E). Propuestos: `SUPABASE_URL_STAGING`,
  `SUPABASE_ANON_KEY_STAGING`, `STAGING_BACKEND_CONNECTION`.
- **Plataformas:** Render staging separado FALTA; Vercel preview de rama `staging` (claves viajan en
  `environment.*.ts` compile-time, no como env de Vercel).
- **Nota de ruta:** el directorio real de environments es `src/app/environments/` (lo usa
  `angular.json`); `docs/ci/e2e-auth-setup.md` y una exclusión de cobertura citan
  `src/environments/` — usar SIEMPRE `src/app/environments/` para staging.

---

## 7. Diseño del seed E2E (planificación)

Del subagente sa-3 (reutilización de RPC; sin script). Entidades, función y orden:
1. Roles/permisos: **NO crear** (migraciones 007/023/024). Solo referenciar por `codigo`.
2. `personas` (INSERT directo, no hay RPC pública dedicada).
3. Institución A/B → `rpc_crear_institucion(...)` (nombre con marcador `E2E-STAGING-`).
4. Usuarios auth (INSERT en `auth.users` + `public.usuarios`, idempotente por email).
5. `usuarios_roles` → `rpc_asignar_rol_usuario(...)` (admin A/B, solo-lectura=`consulta`).
6. Fijar sesión **como admin A** (los RPC derivan contexto de `usuarios_roles`+`auth.uid()`).
7. Ciclo → `rpc_crear_ciclo_escolar(...)`; Periodo → `rpc_crear_periodo_matricula(...)`.
8. Grado → `rpc_crear_grado(...)`; Jornada → `rpc_crear_jornada(...)`; Sección → `rpc_crear_seccion(...)`.
9. Alumno → `rpc_crear_alumno_nueva_persona_con_documento(...)` (AlumnosController:200).
10. Matrícula activa → `rpc_matricular_alumno(...)` (MatriculasController).
11. Responsable (opcional) → `rpc_crear_responsable_con_documento(...)` + `rpc_vincular_alumno_responsable(...)`.

- **IDs deterministas**: UUIDs pre-generados constantes (`…A1`/`…A2`) o tabla `seed_ids`; nombre
  lógico estable por entidad.
- **Idempotente**: guard por clave natural antes de cada alta (email único, `(institucion,nombre)`,
  `numero_identificacion_normalizado`, matrícula activa existente, etc.).
- **Cleanup/reset (solo-staging)**: exige marcador `E2E-STAGING-` en `instituciones.nombre`;
  borrar en orden (historial→matrículas→vínculos→alumnos→secciones→jornadas/grados/periodos/
  ciclos→usuarios_roles→usuarios→personas→instituciones); ejecutar como `postgres`/`service_role`
  de staging, no desde API.
- **QUÉ NO DEBE BORRARSE**: filas `roles`/`permisos`/`roles_permisos` de sistema; instituciones sin
  marcador staging; usuarios de otras instituciones o globales (null); `schema_migrations`/validaciones.
- **PENDIENTE confirmar firma** (no bloquea): `rpc_crear_usuario*`, `rpc_asignar_rol_usuario*`,
  arg-config de `rpc_matricular_alumno`. Fuente canónica: `database/baseline/001_schoolmanager_fase1a.sql`.

---

## 8. Guardrails anti-producción (propuesta E2E-02, no implementada)

- `e2e/playwright.config.ts`: invertir a **allowlist** de hosts staging (`localhost:4200`, dominio
  staging); bloquear todo lo demás por defecto (hoy es blocklist `PROD_HOSTS`).
- Crear `e2e/global-setup.ts`: verificar el `supabaseUrl`/`apiUrl` **embebido del build** y abortar
  si coincide con producción (cierra el vacío: hoy solo se valida `E2E_BASE_URL`).
- Backend: fail-fast al arranque si `Jwt.Issuer`/connection string coinciden con prod
  (patrón base ya existe en `Program.cs`); health endpoint que reporta el ambiente.
- Aislamiento de secrets: job E2E usa solo env `staging`, nunca `RENDER_DEPLOY_HOOK_URL`/secrets prod.
- Archivos exactos a tocar en E2E-02: `backend/SchoolManager.API/appsettings.Staging.json` (crear),
  `backend/SchoolManager.API/Program.cs` (modificar), `e2e/playwright.config.ts` (modificar),
  `e2e/global-setup.ts` (crear). `auth.spec.ts` sin cambios.

---

## 9. Decisiones que necesitan aprobación

1. **CD**: ¿aceptar el fix de zona (PR #55 ya en main) como válido, o reabrir migración a zoneless
   real? Si se reabre, resolver antes `zone.js` en polyfills del test.
2. **RBAC F1–F4**: decidir la familia canónica de nomenclatura (`academico.*` vs `configuracion.*`)
   para estructura académica y ciclos/periodos. No es cambio de código en este lote.
3. **RBAC F9/F10**: ¿asignar grants a cajero/consulta/… (especialmente cargos/pagos) y darle
   `academico.cargos.ver` a operador? Requiere decisión de negocio antes de cualquier cambio.
4. **Aplicar migraciones 023/024** (pendientes incluso en prod) como prerrequisito de E2E.

## 10. Siguiente bloque recomendado

1. Completar la matriz E2E-05 (leer Matriculas/Ciclos/Estructura action→policy→RPC) — requisito
   de implementación.
2. E2E-01: provisión manual de Supabase staging + migraciones 023/024 (humano) y los archivos de
   código (Hermes, PR separado sin mergear).
3. E2E-02: guardrails anti-producción.
4. Revisar RBAC F1–F4/F9/F10 como un ticket de consolidación de nomenclatura separado.

---

## 11. Riesgos críticos

- Producción corre hoy con CD por zona; si se decide migrar a zoneless, el repro actual está
  enmascarado por `zone.js` en polyfills → riesgo de regresión silenciosa.
- Migraciones 023/024 pendientes en prod y staging → ciclos/estructura no son testeables E2E hasta
  aplicarlas.
- Matriz E2E incompleta (Matriculas/Ciclos/Estructura sin leer) → E2E-05 no debe implementarse
  sobre esta base.

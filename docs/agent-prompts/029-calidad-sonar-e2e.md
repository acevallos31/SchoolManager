# Prompt operativo — Bloque 029: calidad, Sonar real y E2E autenticado

## Objetivo
Cerrar la deuda de calidad posterior al rediseño UI/UX de SchoolManager mediante dos líneas coordinadas:

1. garantizar que SonarCloud ejecute análisis real y que un job verde no pueda ocultar un `sonar-scanner` omitido;
2. preparar y ejecutar pruebas E2E autenticadas de los flujos críticos en un entorno seguro que no sea producción.

Este bloque es de calidad/validación. No agrega funcionalidades de producto.

## Lectura obligatoria
Antes de cambiar código, leer completos y en este orden:
1. `AGENTS.md`
2. `docs/AI_CONTEXT.md`
3. `docs/engineering-principles.md`
4. `docs/technical-debt.md`
5. `.github/workflows/*` relacionados con CI
6. `sonar-project.properties` y `.sonarcloud.properties`
7. documentación/handoffs de 028
8. este prompt

Luego ejecutar `git status`, confirmar la rama y revisar los últimos commits.

## Estado base esperado
- `main` contiene PR #51 / Bloque 028, merge commit `a80c19cc10520f0d2cc6c820c288db9bb28631ab`.
- Rama de trabajo: `feature/calidad-sonar-e2e-029`.
- El workflow actual puede mostrar el job SonarCloud en verde aunque el paso `Analizar con sonar-scanner` quede `skipped` cuando `SONAR_TOKEN` está vacío.
- No existe todavía una validación E2E autenticada completa documentada tras el rediseño.

## Alcance permitido
- `.github/workflows/` y configuración CI relacionada con Sonar;
- `sonar-project.properties` / `.sonarcloud.properties` si hay evidencia de que requieren ajuste;
- scripts de validación/healthcheck necesarios;
- configuración de tests E2E del frontend si se requiere;
- tests E2E y fixtures/datos sintéticos seguros;
- documentación de staging/E2E;
- `docs/AI_CONTEXT.md`, `docs/technical-debt.md` y handoff 029;
- cambios frontend/backend mínimos exclusivamente para testabilidad si no cambian comportamiento público y están justificados.

## Fuera de alcance
NO incluir:
- nuevas funciones de producto;
- deuda #10 (migración de accesos directos Supabase frontend a API .NET);
- selector global multiinstitución;
- nuevas migraciones de dominio;
- cambios de modelo académico/financiero;
- rediseño UI adicional;
- secretos reales dentro del repositorio;
- escritura destructiva en producción;
- force-push o merge a `main`.

Si un E2E exige cambiar una regla de negocio o contrato real, detener esa línea y reportar.

# Línea A — SonarCloud real

## A1. Auditar el workflow
Determinar exactamente:
- cómo se obtiene `SONAR_TOKEN`;
- por qué puede estar vacío en PRs/runs actuales;
- qué eventos disparan el análisis;
- si el job se considera success aunque el scanner no corra;
- cómo se valida el Quality Gate;
- si hay diferencias entre PRs internos, forks y `main`.

No asumir que “job verde” significa “Sonar ejecutado”. Verificar pasos y logs.

## A2. Criterio de corrección
El pipeline debe distinguir claramente entre:
- análisis Sonar ejecutado y aprobado;
- análisis no disponible por una limitación conocida (por ejemplo fork sin secrets);
- configuración rota o secreto ausente en un contexto donde debería existir.

Para ramas/PRs internos del repositorio, si `SONAR_TOKEN` es requisito esperado, evitar un falso verde silencioso. Preferir fallo explícito o una verificación clara del precondicionado según las reglas del repo.

No imprimir ni exponer el secreto.

## A3. Secretos
No crear ni commitear `SONAR_TOKEN`.
Si el token no está configurado en GitHub, documentar el paso humano exacto requerido y dejar el workflow preparado para validarlo cuando exista.

Si las herramientas disponibles permiten verificar metadatos del secret sin revelar su valor, hacerlo. Nunca leer/copiar el valor.

## A4. Quality Gate
Confirmar si SonarCloud devuelve Quality Gate efectivo. Si el workflow solo dispara análisis pero no espera/valida el gate, evaluar el cambio mínimo para hacerlo verificable sin introducir complejidad innecesaria.

# Línea B — staging / E2E autenticado

## B1. Descubrir el entorno real
Antes de inventar infraestructura, revisar lo ya existente:
- Vercel frontend/preview;
- Render backend;
- Supabase proyecto(s) y configuración de entorno;
- variables/env del frontend y backend;
- cualquier staging previo documentado.

Preferir reutilizar infraestructura existente de preview/staging si puede aislar datos con seguridad.

No tocar producción para preparar datos E2E.

## B2. Requisito de seguridad
El E2E debe usar:
- usuarios de prueba dedicados;
- datos sintéticos/controlados;
- institución/alumnos/responsables de prueba claramente identificables;
- operaciones reversibles o base desechable;
- ninguna credencial commiteada.

Si no existe un entorno seguro, preparar la automatización/documentación necesaria y reportar el bloqueo en lugar de ejecutar escrituras en producción.

## B3. Herramienta E2E
Auditar si el repo ya usa Playwright/Cypress/u otra herramienta.
- Reusar la existente.
- Si no existe, elegir la opción mínima compatible con Angular 22 y CI.
- No agregar una dependencia pesada sin justificarla.

Priorizar Playwright si no hay framework E2E y resulta compatible con la configuración vigente, pero decidir después de revisar el repo.

## B4. Flujos críticos
Cubrir, en orden de prioridad y solo cuando el entorno permita hacerlo de forma segura:

1. **Autenticación**
   - login real contra Supabase Auth;
   - obtención/carga de sesión;
   - `/auth/me` backend;
   - logout.

2. **Permisos/navegación**
   - usuario autorizado ve rutas permitidas;
   - rutas protegidas no quedan accesibles solo por URL;
   - AppShell/drawer con sesión real.

3. **Alumnos → Matrícula**
   - localizar/crear dato de prueba según fixture permitido;
   - navegación desde alumno;
   - matrícula con opciones válidas;
   - verificar resultado observable.

4. **Alumno → Cargos**
   - consultar cargos/resumen;
   - si la generación de cargo se prueba, hacerlo únicamente con datos sintéticos y entorno seguro.

5. **Pagos**
   - registrar pago sintético si existe base segura;
   - verificar aplicación/saldo/estado observable;
   - no sobrepago ni modificaciones manuales de DB para “hacer pasar” el test.

6. **Portal Responsable**
   - login con responsable de prueba;
   - alumnos vinculados;
   - resumen/cargos/pagos read-only;
   - confirmar ausencia de acciones administrativas.

7. **Responsive autenticado**
   - smoke en desktop/tablet/móvil para AppShell/drawer y portal.

No es obligatorio automatizar absolutamente todos los casos en un único PR si el entorno no lo permite. Priorizar smoke crítico reproducible y documentar lo no ejecutable.

## B5. Datos de prueba
No meter IDs reales hardcodeados si pueden resolverse por fixture/seed controlado.
Si se necesita seed, debe ser específico para test/staging y nunca ejecutarse automáticamente contra producción.

# Validaciones mínimas
Según archivos afectados:

```bash
git diff --check
cd frontend/schoolmanager-frontend
npm test -- --watch=false
npm run build
```

Si backend es afectado:

```bash
dotnet build backend/SchoolManager.API/SchoolManager.API.csproj -c Release
dotnet test tests/SchoolManager.API.IntegrationTests/SchoolManager.API.IntegrationTests.csproj -c Release
dotnet test tests/SchoolManager.Database.IntegrationTests/SchoolManager.Database.IntegrationTests.csproj -c Release
```

Ejecutar además los E2E creados cuando exista entorno seguro.

# Documentación obligatoria
Crear:
- `docs/handoffs/029-calidad-sonar-e2e.md`

Actualizar:
- `docs/AI_CONTEXT.md` al estado real final;
- `docs/technical-debt.md` únicamente para cerrar/actualizar deudas de Sonar/E2E afectadas;
- documentación de ejecución E2E si se crea framework o staging.

Documentar claramente:
- qué Sonar ejecutó realmente;
- Quality Gate real;
- qué E2E fueron ejecutados realmente;
- entorno usado;
- qué quedó bloqueado y por qué;
- ninguna afirmación de E2E si solo hubo mocks o inspección estática.

# Git / autonomía
Tienes autorización para:
- trabajar en `feature/calidad-sonar-e2e-029`;
- modificar archivos dentro del alcance;
- ejecutar tests/builds;
- hacer commits pequeños con Conventional Commits en español;
- push normal;
- abrir o actualizar PR contra `main`.

NO tienes autorización para:
- mergear el PR;
- force-push;
- cambiar secretos/credenciales reales por cuenta propia;
- modificar producción;
- ejecutar escrituras de prueba contra producción.

Si se requiere que el humano configure `SONAR_TOKEN` o credenciales de staging, deja primero todo el código/configuración posible terminado, documenta exactamente qué necesita el humano y continúa con las líneas que no estén bloqueadas.

# Criterio de terminado
El bloque puede declararse completo solo cuando:
- CI no produzca un falso verde silencioso de Sonar para el escenario interno esperado;
- exista evidencia de análisis Sonar real o quede identificado un único paso humano externo claramente bloqueante;
- exista una estrategia de staging/E2E segura y reproducible;
- se hayan ejecutado los smoke/E2E posibles de forma auténtica, no simulada;
- suite/build correspondientes estén verdes;
- handoff y contexto reflejen exactamente lo ocurrido;
- PR esté abierto contra `main` y sin merge.

# Reporte final
Enviar:
- PR y HEAD;
- commits;
- cambios de CI/Sonar;
- si `sonar-scanner` corrió realmente;
- Quality Gate real;
- tests unit/integration;
- E2E ejecutados y entorno;
- staging/preview usado;
- bloqueos humanos pendientes;
- riesgos residuales.

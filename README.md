# SchoolManager

Sistema de gestión escolar (monolito modular) para administrar alumnos,
matrículas, responsables y configuración financiera de una institución
educativa, sobre PostgreSQL con RLS y RPC.

> Estado real: la base de datos llega a la **migración 018**. Los módulos
> listados abajo son los integrados y navegables. Mensualidades, pagos y
> portal de padres existen como vista/servicio en frontend pero **no
> tienen backend** — no se anuncian aquí como terminados.

## Stack

| Capa | Tecnología |
| --- | --- |
| Frontend | Angular 22 standalone + TypeScript |
| Backend | ASP.NET Core Web API (.NET) |
| Base de datos | PostgreSQL (Supabase) con RLS + RPC `security definer` |
| Autenticación | Supabase Auth + JWT |
| Frontend hosting | Vercel |
| Backend hosting | Render |
| Migraciones | SQL versionado en `database/migrations/` (001→018) |

## Estructura

```txt
SchoolManager/
├── backend/
│   └── SchoolManager.API/          # API REST en ASP.NET Core (monolito modular)
│       ├── Controllers/            # Alumnos, Matriculas, Responsables,
│       │                           #   ConceptosFinancieros, PlanesPago, Auth
│       ├── Authorization/          # Permisos RBAC (Permisos.cs)
│       ├── DTOs/ Models/ Identity/
│       └── Program.cs
├── frontend/
│   └── schoolmanager-frontend/     # App Angular 22 standalone
│       └── src/app/
│           ├── pages/              # login, dashboard, alumnos, matriculas,
│           │                       #   responsables, configuracion*
│           └── core/services/      # servicios por módulo
├── database/
│   ├── baseline/                   # baseline consolidado (solo uso de referencia)
│   └── migrations/
│       ├── 001…018_*.sql           # migraciones versionadas
│       ├── rollback/               # rollbacks reversibles
│       └── validation/             # validaciones por migración
├── tests/
│   ├── SchoolManager.API.IntegrationTests/    # identidad + autorización
│   └── SchoolManager.Database.IntegrationTests/ # DB 001→018 (multitenancy/ACID)
├── perf/                           # benchmarks reproducibles
├── docs/
│   ├── engineering-principles.md   # 12 principios del proyecto (reglas vinculantes)
│   ├── AI_CONTEXT.md               # contexto para agentes
│   └── handoffs/  database/  decisiones/
├── .github/workflows/deploy.yml    # CI/CD
└── AGENTS.md                       # reglas operativas para agentes de IA
```

## Módulos actuales (integrados)

- **Alumnos** — gestión de alumnos y sus documentos.
- **Matrículas** — ciclo/período → matrícula → alumno; cambio de estado.
- **Responsables** — vínculos alumno–responsable, gestión RPC.
- **Configuración** — centro educativo, grados/jornadas/secciones, ciclos y períodos.
- **Conceptos financieros** — catálogo de conceptos (migración 018).
- **Planes de pago** — catálogo de planes (migración 018).

## Requisitos

- .NET SDK compatible con el `TargetFramework` del backend.
- Node.js `^22.22.3 || ^24.15.0 || >=26.0.0` y npm 11+.
- Proyecto PostgreSQL/Supabase (solo para entorno real; tests usan Postgres 16 desechable vía Testcontainers).
- Opcional para despliegue: cuentas en Render y Vercel.

## Backend local

```bash
cd backend/SchoolManager.API
dotnet restore
dotnet run          # endpoints bajo /api; Swagger en desarrollo
```

## Frontend local

```bash
cd frontend/schoolmanager-frontend
npm install
npm run start       # http://localhost:4200
npm run build       # build de producción → dist/schoolmanager-frontend/browser
npm test -- --watch=false   # tests unitarios (Vitest)
```

## Tests

Matriz validada en cada PR (ver `AGENTS.md` y `docs/engineering-principles.md` #6/#10):

```bash
# Backend Release build
dotnet build backend/SchoolManager.API/SchoolManager.API.csproj --configuration Release

# API integration tests (identidad + autorización)
dotnet test tests/SchoolManager.API.IntegrationTests/*.csproj --configuration Release

# DB integration tests 001→018
dotnet test tests/SchoolManager.Database.IntegrationTests/*.csproj --configuration Release

# Frontend unit tests + build de producción
cd frontend/schoolmanager-frontend
npm test -- --watch=false
npm run build -- --configuration production
```

## CI/CD

`.github/workflows/deploy.yml` valida backend, API tests, DB tests y build
frontend en cada push/PR; despliega a producción solo desde `main`
(Render vía webhook, frontend por integración Git de Vercel).

## Documentación del proyecto

- `docs/engineering-principles.md` — los 12 principios (reglas del proyecto).
- `docs/database/*.md` — ACID, ERD, RBAC, RLS/RPC, historial de normalización.
- `docs/handoffs/` — estado por bloque/fase.

## Notas de mantenimiento

- Frontend Angular **standalone**: evitar `NgModule` clásico.
- Monolito modular: **sin** microservicios, CQRS ni Generic Repository/UoW
  artificiales (principio #5).
- Toda migración nueva va acompañada de su `rollback/` y `validation/`.
- Todo cambio de esquema/permiso/endpoint exige su test (principio #6).
- Si el bundle crece, aplicar lazy loading por ruta (ver `app.routes.ts`)
  en lugar de subir los presupuestos de `angular.json` sin revisar.

## Licencia

Uso libre para fines educativos.

LEARN-CAP-AEDC71C1

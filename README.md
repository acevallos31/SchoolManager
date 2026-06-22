# SchoolManager

Sistema de gestion escolar para administrar alumnos, matriculas, mensualidades,
pagos y acceso de padres de familia.

## Stack

| Capa | Tecnologia |
| --- | --- |
| Frontend | Angular 22 + TypeScript |
| Backend | ASP.NET Core Web API |
| Base de datos | Supabase PostgreSQL |
| Autenticacion | Supabase Auth + JWT |
| Frontend hosting | Vercel |
| Backend hosting | Render |

## Estructura

```txt
SchoolManager/
├── backend/
│   └── SchoolManager.API/          # API REST en ASP.NET Core
│       ├── Controllers/
│       ├── DTOs/
│       ├── Models/
│       ├── Program.cs
│       ├── SchoolManager.API.csproj
│       └── Dockerfile              # Dockerfile para Render si el root es backend
├── database/
│   ├── schema.sql                  # Script SQL de Supabase
│   └── GUIA_SUPABASE.md
├── frontend/
│   └── schoolmanager-frontend/     # App Angular 22 standalone
│       ├── src/app/
│       ├── angular.json
│       ├── package.json
│       └── package-lock.json
├── Dockerfile                      # Dockerfile para Render desde la raiz
└── README.md
```

## Requisitos

- .NET SDK compatible con el `TargetFramework` del backend.
- Node.js compatible con Angular 22:

```txt
^22.22.3 || ^24.15.0 || >=26.0.0
```

- npm 11+
- Cuenta/proyecto en Supabase.
- Opcional para despliegue: cuentas en Render y Vercel.

## Configuracion de Supabase

1. Crear un proyecto en Supabase.
2. Ejecutar `database/schema.sql` en el SQL Editor.
3. Revisar los pasos de `database/GUIA_SUPABASE.md`.
4. Copiar las credenciales necesarias:
   - Supabase URL
   - Publishable/anon key
   - JWT secret
   - Service/secret key, si aplica al backend

## Backend local

```bash
cd backend/SchoolManager.API
dotnet restore
dotnet run
```

La API expone endpoints bajo `/api`.

En desarrollo, Swagger se habilita cuando el entorno es `Development`.

Variables recomendadas para produccion:

```txt
ASPNETCORE_ENVIRONMENT=Production
Supabase__Url=https://TU-PROYECTO.supabase.co
Supabase__PublishableKey=TU_PUBLISHABLE_KEY
Supabase__SecretKey=TU_SECRET_KEY
Supabase__JwtSecret=TU_JWT_SECRET
```

## Frontend local

```bash
cd frontend/schoolmanager-frontend
npm install
npm run start
```

La aplicacion corre normalmente en:

```txt
http://localhost:4200
```

Para build de produccion:

```bash
npm run build
```

Salida generada para Vercel:

```txt
dist/schoolmanager-frontend/browser
```

## Rutas principales

| Ruta | Uso |
| --- | --- |
| `/login` | Inicio de sesion |
| `/dashboard` | Panel administrativo |
| `/alumnos` | Gestion de alumnos |
| `/matriculas` | Gestion de matriculas |
| `/mensualidades` | Gestion de mensualidades |
| `/portal-padre` | Vista para padres |

## Despliegue en Render

El backend puede desplegarse como Web Service usando Docker.

Configuracion si Render usa la raiz del repositorio:

```txt
Environment: Docker
Root Directory: vacio
Dockerfile Path: ./Dockerfile
Docker Build Context Directory: .
```

Configuracion alternativa si Render usa el backend como root:

```txt
Environment: Docker
Root Directory: backend/SchoolManager.API
Dockerfile Path: ./Dockerfile
Docker Build Context Directory: .
```

URL actual usada por el frontend:

```txt
https://schoolmanager-xdxx.onrender.com/api
```

## Despliegue en Vercel

Configuracion recomendada:

```txt
Framework Preset: Angular
Root Directory: frontend/schoolmanager-frontend
Install Command: npm install
Build Command: npm run build
Output Directory: dist/schoolmanager-frontend/browser
```

## GitHub Actions

El workflow `.github/workflows/deploy.yml` valida backend y frontend. En `main`,
puede disparar despliegues hacia Render y Vercel si los secrets estan
configurados.

Secrets esperados:

```txt
RENDER_DEPLOY_HOOK_URL
VERCEL_TOKEN
VERCEL_ORG_ID
VERCEL_PROJECT_ID
```

## Roles

- Admin: gestiona alumnos, matriculas, mensualidades y pagos.
- Padre: consulta estado de cuenta y mensualidades.

## Modulos

- Alumnos
- Matriculas
- Mensualidades
- Pagos
- Portal de padres

## Notas de mantenimiento

- El frontend esta organizado como Angular 22 standalone. Evitar mezclarlo con
  `AppModule`/`NgModule` clasico.
- Si se modifica `package.json`, regenerar `package-lock.json` con
  `npm install`.
- Si el bundle crece por `jspdf`/`html2canvas`, revisar presupuestos en
  `angular.json` o aplicar lazy loading donde convenga.

## Licencia

Uso libre para fines educativos.

# SchoolManager

Sistema de gestión escolar (alumnos, matrículas, mensualidades y pagos) con:

- **Backend**: ASP.NET Core Web API (.NET 8)
- **Frontend**: Angular
- **Base de datos / Auth**: Supabase (PostgreSQL)

## Estructura del proyecto

```
SchoolManager/
├── database/                          # Script SQL y guía de configuración de Supabase
│   ├── schema.sql
│   └── GUIA_SUPABASE.md
├── backend/SchoolManager.API/         # API REST en .NET
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Controllers/
│   ├── Models/
│   └── DTOs/
└── frontend/schoolmanager-frontend/   # Aplicación Angular
    └── src/app/
        ├── core/services/
        ├── core/guards/
        ├── core/interceptors/
        └── environments/
```

## Requisitos previos

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/) y Angular CLI (`npm install -g @angular/cli`)
- Una cuenta gratuita en [Supabase](https://supabase.com)

## Puesta en marcha

### 1. Base de datos (Supabase)

1. Crea un proyecto nuevo en Supabase.
2. Sigue la guía paso a paso en `database/GUIA_SUPABASE.md`.
3. Ejecuta el script `database/schema.sql` en el editor SQL de Supabase.

### 2. Backend (API)

```bash
cd backend/SchoolManager.API
# Edita appsettings.json con tus credenciales de Supabase (URL, Service Key, JWT Secret)
dotnet restore
dotnet run
```

La API queda disponible en `https://localhost:5001` y la documentación Swagger en `https://localhost:5001/swagger`.

### 3. Frontend (Angular)

```bash
cd frontend/schoolmanager-frontend
# Edita src/app/environments/environment.ts con tu URL y anon key de Supabase
npm install
ng serve
```

La aplicación queda disponible en `http://localhost:4200`.

## Roles del sistema

- **Admin**: gestiona alumnos, matrículas, mensualidades y pagos.
- **Padre**: consulta el estado de cuenta y mensualidades de sus hijos.

## Módulos principales

- **Alumnos**: datos personales y académicos de cada estudiante.
- **Matrículas**: inscripción de un alumno en un año/ciclo escolar.
- **Mensualidades**: cargos mensuales generados por alumno.
- **Pagos**: registro de pagos aplicados a una mensualidad.

## Próximos pasos sugeridos

- Completar la lógica de negocio dentro de cada Controller.
- Implementar las pantallas Angular que consuman los servicios ya creados.
- Configurar políticas de Row Level Security (RLS) adicionales según tus reglas de negocio.

## Licencia

Uso libre para fines educativos.

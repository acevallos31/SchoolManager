# Guía: Configurar Supabase para SchoolManager

## Paso 1 · Crear proyecto en Supabase
1. Ir a https://supabase.com → New Project
2. Name: schoolmanager | Region: US East
3. Esperar ~2 minutos

## Paso 2 · Crear tablas
1. SQL Editor → New Query
2. Pegar todo el contenido de `database/schema.sql`
3. Clic en "Run and enable RLS"
4. Resultado: "Success. No rows returned"

## Paso 3 · Obtener credenciales
- Settings → API Keys → copiar Publishable Key
- Settings → JWT Keys → Legacy JWT Secret → Reveal → copiar
- Project URL: https://TU_ID.supabase.co

## Paso 4 · Pegar credenciales
- `backend/SchoolManager.API/appsettings.json`
- `frontend/schoolmanager-frontend/src/environments/environment.ts`

## Paso 5 · Crear usuario administrador
1. Authentication → Users → Add User
2. Copiar el UUID del usuario creado
3. Ejecutar en SQL Editor:
```sql
INSERT INTO usuarios (nombre, correo, rol, supabase_uid)
VALUES ('Admin Principal', 'admin@schoolmanager.com', 'admin', 'UUID-AQUI');
```

# Guía paso a paso: configurar Supabase para SchoolManager

## 1. Crear el proyecto en Supabase

1. Entra a [supabase.com](https://supabase.com) y crea una cuenta (o inicia sesión).
2. Haz clic en **New Project**.
3. Elige una organización, nombre del proyecto (por ejemplo `schoolmanager`), contraseña de la base de datos y región más cercana.
4. Espera unos minutos a que el proyecto termine de aprovisionarse.

## 2. Ejecutar el script de base de datos

1. En el panel izquierdo, entra a **SQL Editor**.
2. Haz clic en **New query**.
3. Copia y pega todo el contenido de `database/schema.sql`.
4. Ejecuta el script (botón **Run**). Esto crea las tablas `usuarios`, `alumnos`, `matriculas`, `mensualidades`, `pagos`, los índices, las políticas de RLS y el trigger de actualización de pagos.

## 3. Configurar autenticación

1. Ve a **Authentication > Providers** y verifica que **Email** esté habilitado (viene activado por defecto).
2. (Opcional) En **Authentication > URL Configuration**, agrega la URL de tu frontend (por ejemplo `http://localhost:4200`) como *Redirect URL*.
3. Crea al menos un usuario administrador:
   - Ve a **Authentication > Users > Add user** y crea el usuario admin con correo y contraseña.
   - Copia el `UUID` generado para ese usuario.
   - En **SQL Editor**, inserta su perfil:
     ```sql
     insert into public.usuarios (id, nombre_completo, rol)
     values ('UUID-DEL-USUARIO', 'Administrador Principal', 'admin');
     ```

## 4. Obtener tus credenciales

Ve a **Project Settings > API** y copia los siguientes valores, los necesitarás más adelante:

| Valor              | Dónde se usa                                   |
|---------------------|-------------------------------------------------|
| `Project URL`       | `backend/SchoolManager.API/appsettings.json` y `frontend/.../environments/environment.ts` |
| `anon public key`    | Frontend (`environment.ts`)                     |
| `service_role key`   | Backend únicamente (`appsettings.json`) — **nunca la expongas en el frontend** |

Ve a **Project Settings > API > JWT Settings** y copia:

| Valor       | Dónde se usa                                            |
|-------------|----------------------------------------------------------|
| `JWT Secret` | `backend/SchoolManager.API/appsettings.json` (para validar los tokens que emite Supabase) |

## 5. Configurar el backend

Edita `backend/SchoolManager.API/appsettings.json`:

```json
{
  "Supabase": {
    "Url": "https://TU-PROYECTO.supabase.co",
    "ServiceRoleKey": "TU-SERVICE-ROLE-KEY"
  },
  "Jwt": {
    "Secret": "TU-JWT-SECRET",
    "Issuer": "https://TU-PROYECTO.supabase.co/auth/v1"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:4200"]
  }
}
```

## 6. Configurar el frontend

Edita `frontend/schoolmanager-frontend/src/app/environments/environment.ts`:

```ts
export const environment = {
  production: false,
  supabaseUrl: 'https://TU-PROYECTO.supabase.co',
  supabaseAnonKey: 'TU-ANON-KEY',
  apiUrl: 'https://localhost:5001/api'
};
```

## 7. Verificación final

- [ ] El script `schema.sql` se ejecutó sin errores.
- [ ] Existe al menos un usuario con rol `admin` en la tabla `usuarios`.
- [ ] `appsettings.json` tiene la URL, Service Role Key y JWT Secret correctos.
- [ ] `environment.ts` tiene la URL y la anon key correctas.
- [ ] El backend corre con `dotnet run` sin errores de conexión.
- [ ] El frontend corre con `ng serve` y puede iniciar sesión contra Supabase.

¡Listo! Con esto la base de datos y la autenticación quedan completamente configuradas.

# 046G — Edición de usuarios/personas existentes

## Objetivo

Completar la administración de usuarios existentes desde `Configuración > Seguridad y acceso` sin mezclar el perfil funcional de SchoolManager con la identidad externa de Google, Microsoft o Supabase Auth.

## Decisión de autoridad de datos

- `public.personas` es la fuente de verdad para nombres, apellidos y correo de contacto mostrado dentro de SchoolManager.
- `public.usuarios.auth_user_id` conserva únicamente el vínculo con la identidad de autenticación.
- Editar la ficha de usuario **no modifica** `auth.users`, `user_metadata`, Google ni Microsoft.
- No se importan automáticamente teléfono, dirección ni otros metadatos OAuth.
- Teléfono, dirección y demás datos personales del padre/encargado se capturarán o confirmarán durante matrícula / gestión de responsable.

## Backend

`GET /api/configuracion/seguridad/usuarios` ahora devuelve además:

- `nombres`
- `apellidos`
- `puedeEditar`

`puedeEditar` depende de `identidad.usuarios.editar` para la institución activa y de que exista una persona asociada.

Nuevo endpoint:

`PUT /api/configuracion/seguridad/usuarios/{usuarioId}`

Payload:

```json
{
  "institucionId": "uuid",
  "nombres": "Ana",
  "apellidos": "Pérez",
  "correo": "ana@example.com"
}
```

La operación:

1. exige `identidad.usuarios.editar` mediante `usuario_tiene_permiso_institucional_estricto`;
2. valida el alcance institucional o de `platform_admin`;
3. actualiza solo `public.personas`;
4. registra auditoría `usuario.persona_editar`;
5. registra explícitamente `oauth_modificado=false` en la auditoría.

No requiere migración de base de datos.

## Frontend

En Administración de usuarios se agrega acción `Editar` cuando el actor posee capacidad efectiva.

Campos editables para este bloque:

- nombres;
- apellidos;
- correo de contacto.

La interfaz aclara que:

- la identidad Google/Microsoft no cambia;
- teléfono, dirección y otros datos personales se completarán en matrícula;
- la edición afecta únicamente el perfil interno de SchoolManager.

## Alcance intencional

No se implementa en este bloque:

- sincronización de metadata OAuth;
- cambio de email de autenticación en Supabase Auth;
- teléfono/dirección desde Seguridad y acceso;
- impresión/PDF/email de documentos.

La impresión y generación documental será el siguiente bloque transversal antes de profundizar la lógica financiera, porque matrícula, recibos y reportes necesitan salida a papel/PDF y, cuando aplique, envío por correo.

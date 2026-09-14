-- Bloque 042 - resolver usuario objetivo para bootstrap del primer Superadministrador.
-- SOLO LECTURA. Sustituir el correo antes de ejecutar en SQL Manager.

-- 1) Resolver identidad Auth + usuario interno.
select
  au.id as auth_user_id,
  au.email,
  u.id as usuario_id,
  u.activo as usuario_activo,
  p.nombres,
  p.apellidos
from auth.users au
left join public.usuarios u on u.auth_user_id = au.id
left join public.personas p on p.id = u.persona_id
where lower(au.email) = lower('<CORREO_OBJETIVO>');

-- Resultado esperado antes de bootstrap:
-- exactamente 1 fila, usuario_id no nulo y usuario_activo=true.

-- 2) Confirmar que todavía no existe un Superadministrador activo.
select count(*) as superadministradores_activos
from public.usuarios_roles ur
join public.roles r on r.id = ur.rol_id
join public.usuarios u on u.id = ur.usuario_id
where ur.activo
  and ur.institucion_id is null
  and r.activo
  and r.tipo = 'plataforma'
  and r.codigo = 'platform_admin'
  and u.activo;
-- En el bootstrap inicial debe devolver 0.

-- 3) Copiar manualmente el auth_user_id de la consulta 1 y ejecutar, en la MISMA sesion:
-- set schoolmanager.bootstrap_auth_user_id = '<AUTH_USER_ID_UUID>';
--
-- Después ejecutar completo:
-- database/operations/bootstrap_first_platform_admin.sql

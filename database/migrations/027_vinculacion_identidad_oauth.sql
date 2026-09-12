-- ======================================================================
-- Migracion 027 - vinculacion explicita entre la identidad OAuth de
-- Supabase (auth.users.id) y el usuario de aplicacion (public.usuarios).
--
-- Problema que resuelve:
--   El backend resuelve el perfil con
--     select ... from public.usuarios u where u.auth_user_id = $1
--   pero ninguna migracion, trigger ni endpoint del repositorio escribe
--   public.usuarios.auth_user_id. Sin ese vinculo, el JWT de Google es valido
--   pero /api/auth/me responde 403, el frontend limpia la sesion y el usuario
--   vuelve a /login con "No se pudo validar la sesion".
--
-- Decision de diseno:
--   NO se vincula por correo. Se crea una RPC explicita, idempotente y
--   auditable que un operador autorizado ejecuta a mano para un par
--   (usuario_id, auth_user_id) concreto y verificado. La funcion NO asigna
--   roles ni permisos: solo escribe la columna de identidad.
--
-- Alcance: una funcion. Sin tablas nuevas, sin triggers sobre auth.users,
-- sin cambios de RLS ni de permisos de aplicacion.
-- ======================================================================

begin;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM public.schema_migrations WHERE version = '026'
  ) THEN
    RAISE EXCEPTION 'La migracion 027 requiere la 026 aplicada previamente.';
  END IF;
END
$$;

-- Vincula una identidad OAuth a un usuario existente.
-- Devuelve 'vinculado' cuando escribe y 'ya_vinculado' cuando el par ya
-- coincidia (idempotente, sin escritura). Cualquier otro caso es un error
-- explicito: la funcion nunca elige ni adivina el usuario destino.
create or replace function public.vincular_identidad_usuario(
  p_usuario_id uuid,
  p_auth_user_id uuid
) returns text
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
  v_usuario_id uuid;
  v_auth_user_id uuid;
  v_activo boolean;
begin
  if p_usuario_id is null or p_auth_user_id is null then
    raise exception 'vincular_identidad_usuario requiere usuario_id y auth_user_id.'
      using errcode = '22004';
  end if;

  select u.id, u.auth_user_id, u.activo
    into v_usuario_id, v_auth_user_id, v_activo
    from public.usuarios u
   where u.id = p_usuario_id
     for update;

  if v_usuario_id is null then
    raise exception 'El usuario % no existe.', p_usuario_id
      using errcode = 'P0002';
  end if;

  if not v_activo then
    raise exception 'El usuario % esta inactivo; actívalo antes de vincular la identidad.', p_usuario_id
      using errcode = 'P0001';
  end if;

  -- ux_usuarios_auth_user_id ya lo impide en la base; aqui se traduce a un
  -- mensaje accionable en lugar de una violacion de unicidad cruda.
  if exists (
    select 1
      from public.usuarios o
     where o.auth_user_id = p_auth_user_id
       and o.id <> v_usuario_id
  ) then
    raise exception 'La identidad % ya esta vinculada a otro usuario.', p_auth_user_id
      using errcode = '23505';
  end if;

  if v_auth_user_id = p_auth_user_id then
    return 'ya_vinculado';
  end if;

  -- Reasignar en silencio dejaria al usuario anterior sin acceso: se exige
  -- una desvinculacion explicita y consciente.
  if v_auth_user_id is not null then
    raise exception 'El usuario % ya tiene la identidad % vinculada; desvincúlala antes de reasignar.',
      p_usuario_id, v_auth_user_id
      using errcode = 'P0001';
  end if;

  update public.usuarios
     set auth_user_id = p_auth_user_id
   where id = v_usuario_id;

  return 'vinculado';
end
$$;

-- ======================================================================
-- GRANTS (pattern 021/022): helper interno de operacion, no expuesto a
-- public/anon/authenticated. Lo ejecuta service_role o el operador SQL.
-- ======================================================================
revoke all on function public.vincular_identidad_usuario(uuid, uuid)
  from public, anon, authenticated;

grant execute on function public.vincular_identidad_usuario(uuid, uuid)
  to service_role;

insert into public.schema_migrations (version, nombre, checksum)
values ('027', 'vinculacion_identidad_oauth', null)
on conflict (version) do nothing;

commit;

-- Migracion 041 - hardening de invitaciones de acceso.
-- 046B: reduce superficie Data API de la RPC 040 y agrega indices para FK.

begin;

do $$
begin
  if not exists (select 1 from public.schema_migrations where version = '040') then
    raise exception 'La migracion 041 requiere la 040 aplicada previamente.';
  end if;
end
$$;

-- La aplicacion invoca esta RPC exclusivamente desde la API .NET usando la
-- conexion del backend y fijando request.jwt.claim.sub dentro de la transaccion.
-- No existe un caso valido para exponerla directamente por PostgREST al rol
-- authenticated.
revoke execute on function public.rpc_preparar_invitacion_usuario(uuid,text,text,text,uuid,text)
  from authenticated;

-- Indices de soporte para las FK de invitaciones_acceso. Los indices existentes
-- comienzan por institucion_id o correo y no cubren por si solos estas busquedas.
create index if not exists ix_invitaciones_acceso_persona_id
  on public.invitaciones_acceso(persona_id);

create index if not exists ix_invitaciones_acceso_usuario_id
  on public.invitaciones_acceso(usuario_id);

create index if not exists ix_invitaciones_acceso_rol_id
  on public.invitaciones_acceso(rol_id);

create index if not exists ix_invitaciones_acceso_solicitada_por
  on public.invitaciones_acceso(solicitada_por);

insert into public.schema_migrations(version, nombre, checksum)
values ('041', 'hardening_invitaciones_acceso', null)
on conflict (version) do nothing;

commit;

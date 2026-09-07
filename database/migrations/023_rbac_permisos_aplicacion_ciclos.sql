-- ======================================================================
-- Bloque 030D - RBAC: permisos de aplicacion para Ciclos/Periodos.
-- ----------------------------------------------------------------------
-- Registra en el catalogo de permisos de la DB los permisos de APLICACION
-- aprobados para el modulo de ciclos escolares y periodos de matricula
-- (capa de autorizacion .NET del CiclosEscolaresController):
--   academico.ciclos.ver / crear / editar / desactivar
--
-- Esta capa es ADICIONAL y convive con la capa interna de la DB, que NO se
-- modifica:
--   * academico.ciclos.*  -> autorizacion de aplicacion .NET (policy).
--   * configuracion.ciclos.* / configuracion.periodos_matricula.* ->
--     invariante interna de la DB (RPCs 014 + RLS), intacta.
--
-- No renombra ni elimina configuracion.ciclos.*; no toca las RPC 014.
-- Aditiva e idempotente (patron 014). Otorga a 'admin' para conservar el
-- comportamiento actual (014 ya concedia configuracion.ciclos.* solo a
-- admin; la gestion de ciclos/peridos es de alcance admin).
-- ======================================================================
begin;

-- Precedencia explicita: requiere la 022.
do $$ begin
  if not exists (select 1 from public.schema_migrations where version = '022') then
    raise exception 'Migracion 023 requiere la migracion 022 (portal_responsable_lectura).';
  end if;
end $$;

-- ============ 1. CATALOGO DE PERMISOS (aditivo) ============
insert into public.permisos (codigo, modulo, nombre) values
 ('academico.ciclos.ver','academico','Ver ciclos escolares'),
 ('academico.ciclos.crear','academico','Crear ciclos escolares'),
 ('academico.ciclos.editar','academico','Editar ciclos escolares'),
 ('academico.ciclos.desactivar','academico','Desactivar ciclos escolares')
on conflict (codigo) do nothing;

-- ============ 2. GRANT A 'admin' (paridad con 014) ============
insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
cross join public.permisos p
where r.codigo = 'admin'
  and p.codigo like 'academico.ciclos.%'
on conflict do nothing;

insert into public.schema_migrations (version, nombre, checksum)
values ('023', 'rbac_permisos_aplicacion_ciclos', null)
on conflict (version) do nothing;

commit;

-- ======================================================================
-- Bloque 030E - RBAC: permisos de aplicacion para Estructura Academica.
-- ----------------------------------------------------------------------
-- Registra en el catalogo de permisos de la DB los permisos de APLICACION
-- aprobados para el modulo de estructura academica (grados/jornadas/secciones,
-- capa de autorizacion .NET del EstructuraAcademicaController):
--   academico.estructura.ver / editar / desactivar
--
-- Esta capa es ADICIONAL y convive con la capa interna de la DB, que NO se
-- modifica:
--   * academico.estructura.*   -> autorizacion de aplicacion .NET (policy).
--   * configuracion.grados.* / configuracion.jornadas.* /
--     configuracion.secciones.* -> invariante interna de la DB (RPCs 016 +
--     RLS), intacta.
--
-- No renombra ni elimina configuracion.grados/jornadas/secciones.*; no toca
-- las RPC 016. Aditiva e idempotente (patron 023). Otorga a 'admin' para
-- conservar el comportamiento actual (016 ya concedia configuracion.* solo a
-- admin; la gestion de la estructura academica es de alcance admin).
-- Nota: el modulo solo tiene Ver/Editar/Desactivar (no Crear): crear y
-- reactivar se autorizan con editar, desactivar con desactivar.
-- ======================================================================
begin;

-- Precedencia explicita: requiere la 023.
do $$ begin
  if not exists (select 1 from public.schema_migrations where version = '023') then
    raise exception 'Migracion 024 requiere la migracion 023 (rbac_permisos_aplicacion_ciclos).';
  end if;
end $$;

-- ============ 1. CATALOGO DE PERMISOS (aditivo) ============
insert into public.permisos (codigo, modulo, nombre) values
 ('academico.estructura.ver','academico','Ver estructura academica (grados, jornadas, secciones)'),
 ('academico.estructura.editar','academico','Crear/editar/reactivar estructura academica'),
 ('academico.estructura.desactivar','academico','Desactivar estructura academica')
on conflict (codigo) do nothing;

-- ============ 2. GRANT A 'admin' (paridad con 016) ============
insert into public.roles_permisos (rol_id, permiso_id)
select r.id, p.id
from public.roles r
cross join public.permisos p
where r.codigo = 'admin'
  and p.codigo like 'academico.estructura.%'
on conflict do nothing;

insert into public.schema_migrations (version, nombre, checksum)
values ('024', 'rbac_permisos_aplicacion_estructura', null)
on conflict (version) do nothing;

commit;

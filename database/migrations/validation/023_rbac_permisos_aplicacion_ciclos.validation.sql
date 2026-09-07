-- Validacion Migracion 023: los permisos de aplicacion academico.ciclos.*
-- existen en el catalogo, estan otorgados al rol admin, y la capa interna
-- configuracion.ciclos.* / configuracion.periodos_matricula.* sigue intacta.
-- Una fila devuelta por cualquiera de estas consultas es un hallazgo.

-- 1. Permisos de aplicacion ausentes del catalogo.
select esperado.codigo as permiso_aplicacion_faltante
from (values
 ('academico.ciclos.ver'),('academico.ciclos.crear'),
 ('academico.ciclos.editar'),('academico.ciclos.desactivar')
) esperado(codigo)
where not exists (
  select 1 from public.permisos p
  where p.codigo = esperado.codigo
);

-- 2. Rol admin sin el grant correspondiente a cada permiso de aplicacion.
select p.codigo as permiso_sin_grant_admin
from public.permisos p
where p.codigo like 'academico.ciclos.%'
  and not exists (
    select 1
    from public.roles r
    join public.roles_permisos rp on rp.rol_id = r.id and rp.permiso_id = p.id
    where r.codigo = 'admin' and r.activo = true
  );

-- 3. Capa interna de la DB intacta (no renombrada ni eliminada).
select esperado.codigo as permiso_interno_faltante
from (values
 ('configuracion.ciclos.ver'),('configuracion.ciclos.crear'),
 ('configuracion.ciclos.editar'),('configuracion.ciclos.desactivar'),
 ('configuracion.periodos_matricula.ver'),('configuracion.periodos_matricula.crear'),
 ('configuracion.periodos_matricula.editar'),('configuracion.periodos_matricula.desactivar')
) esperado(codigo)
where not exists (
  select 1 from public.permisos p
  where p.codigo = esperado.codigo
);

-- 4. Migracion registrada.
select '023' as migracion_no_registrada
where not exists (select 1 from public.schema_migrations where version = '023');

-- Validacion Migracion 024: los permisos de aplicacion academico.estructura.*
-- existen en el catalogo, estan otorgados al rol admin, y la capa interna
-- configuracion.grados.* / configuracion.jornadas.* / configuracion.secciones.*
-- sigue intacta. Una fila devuelta por cualquiera de estas consultas es un
-- hallazgo.

-- 1. Permisos de aplicacion ausentes del catalogo.
select esperado.codigo as permiso_aplicacion_faltante
from (values
 ('academico.estructura.ver'),
 ('academico.estructura.editar'),
 ('academico.estructura.desactivar')
) esperado(codigo)
where not exists (
  select 1 from public.permisos p
  where p.codigo = esperado.codigo
);

-- 2. Rol admin sin el grant correspondiente a cada permiso de aplicacion.
select p.codigo as permiso_sin_grant_admin
from public.permisos p
where p.codigo like 'academico.estructura.%'
  and not exists (
    select 1
    from public.roles r
    join public.roles_permisos rp on rp.rol_id = r.id and rp.permiso_id = p.id
    where r.codigo = 'admin' and r.activo = true
  );

-- 3. Capa interna de la DB intacta (no renombrada ni eliminada).
select esperado.codigo as permiso_interno_faltante
from (values
 ('configuracion.grados.ver'),('configuracion.grados.crear'),
 ('configuracion.grados.editar'),('configuracion.grados.desactivar'),
 ('configuracion.jornadas.ver'),('configuracion.jornadas.crear'),
 ('configuracion.jornadas.editar'),('configuracion.jornadas.desactivar'),
 ('configuracion.secciones.ver'),('configuracion.secciones.crear'),
 ('configuracion.secciones.editar'),('configuracion.secciones.desactivar')
) esperado(codigo)
where not exists (
  select 1 from public.permisos p
  where p.codigo = esperado.codigo
);

-- 4. Migracion registrada.
select '024' as migracion_no_registrada
where not exists (select 1 from public.schema_migrations where version = '024');

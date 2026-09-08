-- Rollback Migracion 024: revierte el seed RBAC de permisos de aplicacion
-- academico.estructura.* (Bloque 030E). Aditivo: solo elimina los permisos de
-- aplicacion registrados en 024. NO toca la capa interna
-- configuracion.grados.* / configuracion.jornadas.* / configuracion.secciones.*
-- (previa a 024), ni las RPC 016, ni los datos de grados/jornadas/secciones.
-- roles_permisos se limpia por cascada (FK ON DELETE CASCADE) al eliminar
-- los permisos.
begin;

delete from public.roles_permisos
where permiso_id in (
    select id from public.permisos
    where codigo in (
        'academico.estructura.ver',
        'academico.estructura.editar',
        'academico.estructura.desactivar'
    )
);

delete from public.permisos
where codigo in (
    'academico.estructura.ver',
    'academico.estructura.editar',
    'academico.estructura.desactivar'
);

-- Elimina el registro de la migracion para permitir reversiones completas
-- (patron 023: cada rollback desregistra su version en schema_migrations).
delete from public.schema_migrations where version = '024';

commit;

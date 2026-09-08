-- Rollback Migracion 023: revierte el seed RBAC de permisos de aplicacion
-- academico.ciclos.* (Bloque 030D). Aditivo: solo elimina los permisos de
-- aplicacion registrados en 023. NO toca la capa interna configuracion.ciclos.*
-- / configuracion.periodos_matricula.* (previa a 023), ni las RPC 014/015, ni
-- los datos de ciclos_escolares/periodos_matricula. roles_permisos se limpia
-- por cascada (FK ON DELETE CASCADE) al eliminar los permisos.
begin;

delete from public.roles_permisos
where permiso_id in (
    select id from public.permisos
    where codigo in (
        'academico.ciclos.ver',
        'academico.ciclos.crear',
        'academico.ciclos.editar',
        'academico.ciclos.desactivar'
    )
);

delete from public.permisos
where codigo in (
    'academico.ciclos.ver',
    'academico.ciclos.crear',
    'academico.ciclos.editar',
    'academico.ciclos.desactivar'
);

-- Elimina el registro de la migracion para permitir reversiones completas
-- (patron 022: cada rollback desregistra su version en schema_migrations).
delete from public.schema_migrations where version = '023';

commit;

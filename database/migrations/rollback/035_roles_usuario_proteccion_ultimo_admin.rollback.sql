-- Rollback 035 intencionalmente no automatizado.
-- Revertir esta migracion restauraria una version de rpc_desactivar_rol_usuario
-- que no protege al ultimo administrador institucional frente a retiros
-- concurrentes. Por seguridad, requiere revision manual y no se ejecuta como
-- rollback desatendido.

do $$
begin
  raise exception 'Rollback 035 requiere revision manual: no se debilitan guardas de administracion automaticamente.';
end
$$;

-- Validacion 017: diagnosticos (filas = hallazgos) para la gestion RPC de responsables.
select firma_rpc from (values
  ('rpc_crear_responsable_con_documento(uuid,text,text,text,text,text,text)'),
  ('rpc_crear_responsable_para_persona(uuid,uuid)'),
  ('rpc_editar_responsable(uuid,text,text,text,text)'),
  ('rpc_inactivar_responsable(uuid,text)'),
  ('rpc_reactivar_responsable(uuid)'),
  ('rpc_vincular_alumno_responsable(uuid,uuid,text,boolean,boolean)'),
  ('rpc_editar_vinculo_responsable(uuid,text,boolean,boolean)'),
  ('rpc_desactivar_vinculo_responsable(uuid,text)'),
  ('rpc_reactivar_vinculo_responsable(uuid)')
) esperado(firma_rpc)
where to_regprocedure('public.' || esperado.firma_rpc) is null;

select 'Vinculos inactivos con es_principal=true' error
where exists (
  select 1 from public.alumno_responsable
  where estado = 'inactivo' and es_principal = true
);

select 'Responsable inactivo con vinculos activos' error
where exists (
  select 1 from public.alumno_responsable ar
  join public.responsables r on r.id = ar.responsable_id
  where r.estado = 'inactivo' and ar.estado = 'activo'
);
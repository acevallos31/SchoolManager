-- Rollback 009. Retira RLS/RPC sin eliminar datos RBAC sembrados.
-- Los privilegios preexistentes de authenticated no pueden reconstruirse de
-- forma portable; el rollback deja ese rol sin acceso a tablas (estado seguro).
begin;

drop policy if exists personas_select on public.personas;
drop policy if exists usuarios_select on public.usuarios;
drop policy if exists roles_select on public.roles;
drop policy if exists permisos_select on public.permisos;
drop policy if exists usuarios_roles_select on public.usuarios_roles;
drop policy if exists roles_permisos_select on public.roles_permisos;
drop policy if exists alumnos_select on public.alumnos;
drop policy if exists responsables_select on public.responsables;
drop policy if exists alumno_responsable_select on public.alumno_responsable;
drop policy if exists ciclos_select on public.ciclos_escolares;
drop policy if exists grados_select on public.grados;
drop policy if exists jornadas_select on public.jornadas;
drop policy if exists secciones_select on public.secciones;
drop policy if exists periodos_matricula_select on public.periodos_matricula;
drop policy if exists matriculas_select on public.matriculas;
drop policy if exists matricula_historial_select on public.matricula_estado_historial;

alter table public.personas disable row level security;
alter table public.usuarios disable row level security;
alter table public.roles disable row level security;
alter table public.permisos disable row level security;
alter table public.usuarios_roles disable row level security;
alter table public.roles_permisos disable row level security;
alter table public.alumnos disable row level security;
alter table public.responsables disable row level security;
alter table public.alumno_responsable disable row level security;
alter table public.ciclos_escolares disable row level security;
alter table public.grados disable row level security;
alter table public.jornadas disable row level security;
alter table public.secciones disable row level security;
alter table public.periodos_matricula disable row level security;
alter table public.matriculas disable row level security;
alter table public.matricula_estado_historial disable row level security;

revoke all privileges on all tables in schema public from anon, authenticated;

drop function if exists public.rpc_desactivar_rol_usuario(uuid, text);
drop function if exists public.rpc_asignar_rol_usuario(uuid, text, uuid);
drop function if exists public.rpc_desactivar_seccion(uuid, text);
drop function if exists public.rpc_reactivar_alumno(uuid);
drop function if exists public.rpc_desactivar_alumno(uuid, text);
drop function if exists public.rpc_cambiar_estado_matricula(uuid, text, text);
drop function if exists public.rpc_matricular_alumno(uuid, uuid, uuid);
drop function if exists public.rpc_crear_seccion(uuid, uuid, uuid, uuid, text, integer);
drop function if exists public.rpc_crear_alumno_para_persona(uuid, uuid, date, text, text);
drop function if exists public.rpc_crear_alumno_nueva_persona(uuid, text, text, date, text, text);
drop function if exists public.usuario_puede_ver_matricula(uuid);
drop function if exists public.usuario_puede_ver_responsable(uuid);
drop function if exists public.usuario_puede_ver_alumno(uuid);
drop function if exists public.usuario_puede_ver_ciclo(uuid);
drop function if exists public.usuario_puede_ver_institucion(uuid);
drop function if exists public.usuario_tiene_permiso_en_algun_ambito(text);
drop function if exists public.usuario_tiene_permiso_actual(text, uuid);
drop function if exists public.usuario_actual_id();

alter function public.usuario_tiene_permiso(uuid, text, uuid) security invoker;
alter function public.usuario_tiene_permiso(uuid, text, uuid)
  set search_path = pg_catalog, public;
grant execute on all functions in schema public to public;

delete from public.schema_migrations where version = '009';
commit;

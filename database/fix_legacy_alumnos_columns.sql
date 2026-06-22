-- Ejecuta este script si al crear alumnos Supabase reclama columnas viejas
-- como nombre, identidad o grado. Relaja esas columnas durante la migracion.

alter table public.alumnos alter column nombre drop not null;
alter table public.alumnos alter column identidad drop not null;
alter table public.alumnos alter column grado drop not null;


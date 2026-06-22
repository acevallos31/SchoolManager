
-- =====================================================================
-- SchoolManager - Esquema de base de datos para Supabase (PostgreSQL)
-- Ejecutar completo en: Supabase Dashboard > SQL Editor > New query
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. Extensiones necesarias
-- ---------------------------------------------------------------------
create extension if not exists "uuid-ossp";

-- ---------------------------------------------------------------------
-- 2. Tabla de perfiles (extiende auth.users con rol y datos básicos)
-- ---------------------------------------------------------------------
create table if not exists public.usuarios (
    id              uuid primary key references auth.users(id) on delete cascade,
    nombre_completo text not null,
    rol             text not null check (rol in ('admin', 'padre')),
    telefono        text,
    creado_en       timestamptz not null default now()
);

-- ---------------------------------------------------------------------
-- 3. Alumnos
-- ---------------------------------------------------------------------
create table if not exists public.alumnos (
    id              uuid primary key default uuid_generate_v4(),
    padre_id        uuid references public.usuarios(id) on delete set null,
    nombre          text not null,
    apellido        text not null,
    fecha_nacimiento date not null,
    grado           text not null,
    seccion         text,
    activo          boolean not null default true,
    creado_en       timestamptz not null default now()
);

-- ---------------------------------------------------------------------
-- 4. Matrículas (inscripción de un alumno en un año/ciclo escolar)
-- ---------------------------------------------------------------------
create table if not exists public.matriculas (
    id              uuid primary key default uuid_generate_v4(),
    alumno_id       uuid not null references public.alumnos(id) on delete cascade,
    anio_escolar    integer not null,
    fecha_matricula date not null default current_date,
    monto           numeric(10,2) not null,
    estado          text not null default 'activa' check (estado in ('activa', 'retirada', 'finalizada')),
    creado_en       timestamptz not null default now(),
    unique (alumno_id, anio_escolar)
);

-- ---------------------------------------------------------------------
-- 5. Mensualidades (cargos mensuales por alumno)
-- ---------------------------------------------------------------------
create table if not exists public.mensualidades (
    id                uuid primary key default uuid_generate_v4(),
    alumno_id         uuid not null references public.alumnos(id) on delete cascade,
    matricula_id      uuid references public.matriculas(id) on delete set null,
    mes               integer not null check (mes between 1 and 12),
    anio              integer not null,
    monto             numeric(10,2) not null,
    fecha_vencimiento date not null,
    estado            text not null default 'pendiente' check (estado in ('pendiente', 'pagada', 'vencida')),
    creado_en         timestamptz not null default now(),
    unique (alumno_id, mes, anio)
);

-- ---------------------------------------------------------------------
-- 6. Pagos (registro de pagos aplicados a una mensualidad)
-- ---------------------------------------------------------------------
create table if not exists public.pagos (
    id              uuid primary key default uuid_generate_v4(),
    mensualidad_id  uuid not null references public.mensualidades(id) on delete cascade,
    monto           numeric(10,2) not null,
    fecha_pago      date not null default current_date,
    metodo_pago     text not null check (metodo_pago in ('efectivo', 'transferencia', 'tarjeta', 'otro')),
    comprobante_url text,
    registrado_por  uuid references public.usuarios(id),
    creado_en       timestamptz not null default now()
);

-- ---------------------------------------------------------------------
-- 7. Índices recomendados
-- ---------------------------------------------------------------------
create index if not exists idx_alumnos_padre_id        on public.alumnos(padre_id);
create index if not exists idx_matriculas_alumno_id    on public.matriculas(alumno_id);
create index if not exists idx_mensualidades_alumno_id  on public.mensualidades(alumno_id);
create index if not exists idx_mensualidades_estado     on public.mensualidades(estado);
create index if not exists idx_pagos_mensualidad_id     on public.pagos(mensualidad_id);

-- ---------------------------------------------------------------------
-- 8. Row Level Security (RLS)
-- ---------------------------------------------------------------------
alter table public.usuarios       enable row level security;
alter table public.alumnos        enable row level security;
alter table public.matriculas     enable row level security;
alter table public.mensualidades  enable row level security;
alter table public.pagos          enable row level security;

-- Función auxiliar: obtiene el rol del usuario autenticado
create or replace function public.rol_actual()
returns text
language sql
stable
as $$
  select rol from public.usuarios where id = auth.uid();
$$;

-- usuarios: cada usuario ve su propio perfil; el admin ve todos
create policy "usuarios_select_propio_o_admin"
    on public.usuarios for select
    using (id = auth.uid() or public.rol_actual() = 'admin');

-- alumnos: admin ve todos, padre solo ve a sus hijos
create policy "alumnos_select_admin_o_padre"
    on public.alumnos for select
    using (public.rol_actual() = 'admin' or padre_id = auth.uid());

create policy "alumnos_modificacion_solo_admin"
    on public.alumnos for all
    using (public.rol_actual() = 'admin');

-- matriculas: admin ve todas, padre ve las de sus hijos
create policy "matriculas_select_admin_o_padre"
    on public.matriculas for select
    using (
        public.rol_actual() = 'admin'
        or alumno_id in (select id from public.alumnos where padre_id = auth.uid())
    );

create policy "matriculas_modificacion_solo_admin"
    on public.matriculas for all
    using (public.rol_actual() = 'admin');

-- mensualidades: admin ve todas, padre ve las de sus hijos
create policy "mensualidades_select_admin_o_padre"
    on public.mensualidades for select
    using (
        public.rol_actual() = 'admin'
        or alumno_id in (select id from public.alumnos where padre_id = auth.uid())
    );

create policy "mensualidades_modificacion_solo_admin"
    on public.mensualidades for all
    using (public.rol_actual() = 'admin');

-- pagos: admin ve todos, padre ve los de sus hijos
create policy "pagos_select_admin_o_padre"
    on public.pagos for select
    using (
        public.rol_actual() = 'admin'
        or mensualidad_id in (
            select m.id from public.mensualidades m
            join public.alumnos a on a.id = m.alumno_id
            where a.padre_id = auth.uid()
        )
    );

create policy "pagos_modificacion_solo_admin"
    on public.pagos for all
    using (public.rol_actual() = 'admin');

-- ---------------------------------------------------------------------
-- 9. Trigger: al pagar una mensualidad por completo, marcarla como pagada
-- ---------------------------------------------------------------------
create or replace function public.actualizar_estado_mensualidad()
returns trigger
language plpgsql
as $$
declare
    total_pagado numeric(10,2);
    monto_mensualidad numeric(10,2);
begin
    select monto into monto_mensualidad from public.mensualidades where id = new.mensualidad_id;
    select coalesce(sum(monto), 0) into total_pagado from public.pagos where mensualidad_id = new.mensualidad_id;

    if total_pagado >= monto_mensualidad then
        update public.mensualidades set estado = 'pagada' where id = new.mensualidad_id;
    end if;

    return new;
end;
$$;

create trigger trg_actualizar_estado_mensualidad
    after insert on public.pagos
    for each row
    execute function public.actualizar_estado_mensualidad();

-- =====================================================================
-- Fin del script. Continúa con database/GUIA_SUPABASE.md
-- =====================================================================

-- ============================================================
-- SchoolManager · Schema SQL para Supabase
-- Ejecutar en: Supabase Dashboard → SQL Editor → New Query
-- ============================================================

CREATE TABLE IF NOT EXISTS usuarios (
  id              UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  nombre          TEXT NOT NULL,
  correo          TEXT NOT NULL UNIQUE,
  rol             TEXT NOT NULL CHECK (rol IN ('admin', 'padre')),
  supabase_uid    UUID UNIQUE,
  created_at      TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ciclos_escolares (
  id              UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  nombre          TEXT NOT NULL,
  fecha_inicio    DATE NOT NULL,
  fecha_fin       DATE NOT NULL,
  activo          BOOLEAN DEFAULT FALSE,
  created_at      TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS alumnos (
  id                UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  nombre            TEXT NOT NULL,
  identidad         TEXT NOT NULL UNIQUE,
  fecha_nacimiento  DATE,
  grado             TEXT NOT NULL,
  seccion           TEXT,
  estado            TEXT NOT NULL DEFAULT 'activo' CHECK (estado IN ('activo', 'inactivo')),
  tutor_id          UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  created_at        TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS matriculas (
  id              UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  alumno_id       UUID NOT NULL REFERENCES alumnos(id) ON DELETE CASCADE,
  ciclo_id        UUID NOT NULL REFERENCES ciclos_escolares(id) ON DELETE CASCADE,
  fecha_matricula DATE NOT NULL DEFAULT CURRENT_DATE,
  monto           DECIMAL(10,2) NOT NULL CHECK (monto > 0),
  estado          TEXT NOT NULL DEFAULT 'pendiente' CHECK (estado IN ('pagada', 'pendiente')),
  registrado_por  UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  created_at      TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  UNIQUE (alumno_id, ciclo_id)
);

CREATE TABLE IF NOT EXISTS mensualidades (
  id              UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  alumno_id       UUID NOT NULL REFERENCES alumnos(id) ON DELETE CASCADE,
  ciclo_id        UUID NOT NULL REFERENCES ciclos_escolares(id) ON DELETE CASCADE,
  mes             INTEGER NOT NULL CHECK (mes BETWEEN 1 AND 12),
  monto_original  DECIMAL(10,2) NOT NULL CHECK (monto_original > 0),
  descuento       DECIMAL(10,2) NOT NULL DEFAULT 0 CHECK (descuento >= 0),
  monto_final     DECIMAL(10,2) GENERATED ALWAYS AS (monto_original - descuento) STORED,
  estado          TEXT NOT NULL DEFAULT 'pendiente' CHECK (estado IN ('pendiente', 'pagada', 'vencida')),
  fecha_limite    DATE NOT NULL,
  created_at      TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  UNIQUE (alumno_id, ciclo_id, mes)
);

CREATE TABLE IF NOT EXISTS pagos (
  id              UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  mensualidad_id  UUID NOT NULL REFERENCES mensualidades(id) ON DELETE CASCADE,
  fecha_pago      DATE NOT NULL DEFAULT CURRENT_DATE,
  monto_pagado    DECIMAL(10,2) NOT NULL CHECK (monto_pagado > 0),
  metodo_pago     TEXT NOT NULL DEFAULT 'efectivo' CHECK (metodo_pago IN ('efectivo', 'transferencia', 'tarjeta')),
  registrado_por  UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  created_at      TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_alumnos_identidad ON alumnos(identidad);
CREATE INDEX IF NOT EXISTS idx_alumnos_grado ON alumnos(grado);
CREATE INDEX IF NOT EXISTS idx_mensualidades_alumno ON mensualidades(alumno_id);
CREATE INDEX IF NOT EXISTS idx_mensualidades_estado ON mensualidades(estado);
CREATE INDEX IF NOT EXISTS idx_pagos_mensualidad ON pagos(mensualidad_id);

INSERT INTO ciclos_escolares (nombre, fecha_inicio, fecha_fin, activo)
VALUES ('2026-2027', '2026-01-15', '2026-11-30', TRUE)
ON CONFLICT DO NOTHING;


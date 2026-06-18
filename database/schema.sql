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

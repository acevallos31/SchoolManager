import { inject, Injectable } from '@angular/core';
import { SUPABASE_CLIENT } from './auth';

export interface MatriculaActualAlumno {
  id: string;
  ciclo: string;
  grado: string;
  seccion: string;
}

export interface AlumnoListado {
  id: string;
  personaId: string;
  nombreCompleto: string;
  identidad: string | null;
  rne: string | null;
  estado: 'activo' | 'inactivo';
  matriculaActual: MatriculaActualAlumno | null;
}

export interface CrearAlumnoInput {
  institucionId: string;
  nombres: string;
  apellidos: string;
  tipoIdentificacion: string;
  numeroIdentificacion: string;
  fechaNacimiento: string | null;
  rne: string | null;
  codigoInterno: string | null;
}

interface PersonaRelacion {
  nombres: string;
  apellidos: string;
  numero_identificacion: string | null;
}

interface MatriculaRelacion {
  id: string;
  seccion: {
    nombre: string;
    grado: { nombre: string } | null;
    ciclo: { nombre: string } | null;
  } | null;
}

interface AlumnoRow {
  id: string;
  persona_id: string;
  rne: string | null;
  estado: 'activo' | 'inactivo';
  persona: PersonaRelacion | null;
  matriculas: MatriculaRelacion[] | null;
}

interface SupabaseErrorLike {
  code?: string;
  message?: string;
}

export class AlumnoServiceError extends Error {
  constructor(message: string, public readonly code: string) {
    super(message);
    this.name = 'AlumnoServiceError';
  }
}

@Injectable({ providedIn: 'root' })
export class AlumnoService {
  private readonly supabase = inject(SUPABASE_CLIENT);

  async listar(): Promise<AlumnoListado[]> {
    const { data, error } = await this.supabase
      .from('alumnos')
      .select(`
        id,
        persona_id,
        rne,
        estado,
        persona:personas!alumnos_persona_id_fkey(
          nombres,
          apellidos,
          numero_identificacion
        ),
        matriculas:matriculas!fk_matriculas_alumno_institucion(
          id,
          estado,
          created_at,
          seccion:secciones!fk_matriculas_seccion_contexto(
            nombre,
            grado:grados!fk_secciones_grado(nombre),
            ciclo:ciclos_escolares!fk_secciones_ciclo_institucion(nombre)
          )
        )
      `)
      .eq('matriculas.estado', 'activa')
      .order('created_at', { referencedTable: 'matriculas', ascending: false });

    if (error) {
      throw this.mapError(error, 'No se pudo cargar la lista de alumnos.');
    }

    return ((data ?? []) as unknown as AlumnoRow[]).map(row => {
      const matricula = row.matriculas?.[0];
      const seccion = matricula?.seccion;

      return {
        id: row.id,
        personaId: row.persona_id,
        nombreCompleto: [row.persona?.nombres, row.persona?.apellidos]
          .filter(Boolean)
          .join(' '),
        identidad: row.persona?.numero_identificacion ?? null,
        rne: row.rne,
        estado: row.estado,
        matriculaActual: matricula && seccion
          ? {
              id: matricula.id,
              ciclo: seccion.ciclo?.nombre ?? 'Sin ciclo',
              grado: seccion.grado?.nombre ?? 'Sin grado',
              seccion: seccion.nombre
            }
          : null
      };
    });
  }

  async crear(input: CrearAlumnoInput): Promise<string> {
    const { data, error } = await this.supabase.rpc(
      'rpc_crear_alumno_nueva_persona_con_documento',
      {
        p_institucion_id: input.institucionId,
        p_nombres: input.nombres.trim(),
        p_apellidos: input.apellidos.trim(),
        p_tipo_identificacion: input.tipoIdentificacion.trim(),
        p_numero_identificacion: input.numeroIdentificacion.trim(),
        p_fecha_nacimiento: input.fechaNacimiento,
        p_rne: this.nullIfBlank(input.rne),
        p_codigo_interno: this.nullIfBlank(input.codigoInterno)
      }
    );

    if (error) {
      throw this.mapError(error, 'No se pudo crear el alumno.');
    }
    if (typeof data !== 'string') {
      throw new AlumnoServiceError('La creación no devolvió un alumno válido.', 'INVALID_RESPONSE');
    }

    return data;
  }

  async desactivar(alumnoId: string, motivo: string): Promise<void> {
    const { error } = await this.supabase.rpc('rpc_desactivar_alumno', {
      p_alumno_id: alumnoId,
      p_motivo: motivo.trim()
    });
    if (error) {
      throw this.mapError(error, 'No se pudo desactivar el alumno.');
    }
  }

  async reactivar(alumnoId: string): Promise<void> {
    const { error } = await this.supabase.rpc('rpc_reactivar_alumno', {
      p_alumno_id: alumnoId
    });
    if (error) {
      throw this.mapError(error, 'No se pudo reactivar el alumno.');
    }
  }

  private nullIfBlank(value: string | null): string | null {
    const normalized = value?.trim();
    return normalized ? normalized : null;
  }

  private mapError(error: SupabaseErrorLike, fallback: string): AlumnoServiceError {
    switch (error.code) {
      case '42501':
        return new AlumnoServiceError('No tienes permiso para realizar esta operación.', error.code);
      case '23505':
        return new AlumnoServiceError('Ya existe una persona o alumno con esos identificadores.', error.code);
      case '23503':
        return new AlumnoServiceError('La institución o el contexto académico ya no está disponible.', error.code);
      case '22023':
        return new AlumnoServiceError(error.message || 'Los datos ingresados no son válidos.', error.code);
      default:
        return new AlumnoServiceError(fallback, error.code ?? 'UNKNOWN');
    }
  }
}

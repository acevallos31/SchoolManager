import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { environment } from '../../environments/environment';

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
  codigoInterno: string | null;
  estado: 'activo' | 'inactivo';
  matriculaActual: MatriculaActualAlumno | null;
}

// Respuesta paginada server-side (PERF-02). Los items son el mismo AlumnoListado[].
export interface PaginatedAlumnos {
  items: AlumnoListado[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface FiltroAlumnos {
  termino?: string;
  estado?: 'activo' | 'inactivo';
  page?: number;
  pageSize?: number;
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

export class AlumnoServiceError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
    this.name = 'AlumnoServiceError';
  }
}

@Injectable({ providedIn: 'root' })
export class AlumnoService {
  private readonly baseUrl = `${environment.apiUrl}/alumnos`;

  constructor(private readonly http: HttpClient) {}

  listar(): Promise<AlumnoListado[]> {
    return this.http
      .get<AlumnoListado[]>(this.baseUrl)
      .toPromise()
      .then(items => (items ?? []) as AlumnoListado[])
      .catch(err => Promise.reject(this.mapError(err)));
  }

  /** Búsqueda paginada server-side (PERF-02): la API filtra y recorta la página
   *  antes de devolver filas. Evita descargar todos los alumnos. */
  buscarPaginado(filtro: FiltroAlumnos = {}): Promise<PaginatedAlumnos> {
    const params = new Map<string, string>();
    if (filtro.termino) params.set('termino', filtro.termino);
    if (filtro.estado) params.set('estado', filtro.estado);
    if (filtro.page) params.set('page', String(filtro.page));
    if (filtro.pageSize) params.set('pageSize', String(filtro.pageSize));
    return this.http
      .get<PaginatedAlumnos>(this.baseUrl, { params: Object.fromEntries(params) })
      .toPromise()
      .then(resultado => {
        if (!resultado) throw new AlumnoServiceError('La búsqueda no devolvió resultados válidos.', 0);
        return resultado;
      })
      .catch(err => Promise.reject(this.esAlumnoError(err) ? err : this.mapError(err)));
  }

  obtenerPorId(alumnoId: string): Promise<AlumnoListado | null> {
    return this.http
      .get<AlumnoListado>(`${this.baseUrl}/${alumnoId}`)
      .toPromise()
      .then(alumno => alumno ?? null)
      .catch(err => {
        const mapped = this.esAlumnoError(err) ? err : this.mapError(err);
        // 404 (HTTP directo o ya mapeado) → null, igual que el acceso directo previo
        if (mapped.status === 404) return null;
        return Promise.reject(mapped);
      });
  }

  crear(input: CrearAlumnoInput): Promise<string> {
    const body = {
      institucionId: input.institucionId,
      nombres: input.nombres.trim(),
      apellidos: input.apellidos.trim(),
      tipoIdentificacion: input.tipoIdentificacion.trim(),
      numeroIdentificacion: input.numeroIdentificacion.trim(),
      fechaNacimiento: input.fechaNacimiento,
      rne: this.nullIfBlank(input.rne),
      codigoInterno: this.nullIfBlank(input.codigoInterno)
    };
    return this.http
      .post<{ id: string }>(this.baseUrl, body)
      .toPromise()
      .then(resultado => {
        if (!resultado?.id) {
          throw new AlumnoServiceError('La creación no devolvió un alumno válido.', 0);
        }
        return resultado.id;
      })
      .catch(err => Promise.reject(this.esAlumnoError(err) ? err : this.mapError(err)));
  }

  desactivar(alumnoId: string, motivo: string): Promise<void> {
    return this.http
      .post<void>(`${this.baseUrl}/${alumnoId}/desactivar`, { motivo: motivo.trim() })
      .toPromise()
      .then(() => undefined)
      .catch(err => Promise.reject(this.esAlumnoError(err) ? err : this.mapError(err)));
  }

  reactivar(alumnoId: string): Promise<void> {
    return this.http
      .post<void>(`${this.baseUrl}/${alumnoId}/reactivar`, {})
      .toPromise()
      .then(() => undefined)
      .catch(err => Promise.reject(this.esAlumnoError(err) ? err : this.mapError(err)));
  }

  private nullIfBlank(value: string | null): string | null {
    const normalized = value?.trim();
    return normalized ? normalized : null;
  }

  private esAlumnoError(err: unknown): err is AlumnoServiceError {
    return err instanceof AlumnoServiceError;
  }

  private mapError(err: unknown): AlumnoServiceError {
    let status = 0;
    let message = 'No se pudo completar la operación.';
    if (err instanceof HttpErrorResponse) {
      status = err.status;
      const body = err.error as { error?: string } | null;
      if (body?.error) message = body.error;
      switch (status) {
        case 403:
          message = 'No tienes permiso para realizar esta operación.';
          break;
        case 404:
          message = 'El recurso no existe o no pertenece a la institución actual.';
          break;
        case 409:
          message = body?.error ?? 'Ya existe una persona o alumno con esos identificadores.';
          break;
        case 400:
          message = body?.error ?? message;
          break;
      }
    }
    return new AlumnoServiceError(message, status);
  }
}
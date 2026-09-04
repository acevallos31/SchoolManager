import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export type EstadoMatricula =
  | 'pendiente'
  | 'activa'
  | 'finalizada'
  | 'retirada'
  | 'anulada'
  | 'trasladada';

// Respuesta de solo lectura del backend (MatriculaDto) — los nombres siguen la
// serialización camelCase del API, no el modelo de base de datos.
export interface Matricula {
  id: string;
  alumnoId: string;
  institucionId: string;
  cicloId: string;
  seccionId: string;
  periodoMatriculaId: string;
  registradoPor: string | null;
  fechaMatricula: string;
  estado: EstadoMatricula;
  fechaAnulacion: string | null;
  motivoAnulacion: string | null;
  createdAt: string;
  nombreAlumno: string;
  nombreSeccion: string;
  nombreGrado: string;
  nombreJornada: string | null;
  nombrePeriodo: string;
  cicloNombre: string;
}

export interface MatriculaInput {
  alumnoId: string;
  seccionId: string;
  periodoMatriculaId: string;
}

// Respuesta paginada server-side (PERF-02). Los items son el mismo Matricula[].
export interface PaginatedMatriculas {
  items: Matricula[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface FiltroMatriculas {
  alumnoId?: string;
  cicloId?: string;
  estado?: EstadoMatricula;
  page?: number;
  pageSize?: number;
}

export interface CambioEstadoMatricula {
  estado: EstadoMatricula;
  motivo?: string | null;
}

// Estados terminales que requieren motivo en el contrato vigente.
export const ESTADOS_TERMINALES: readonly EstadoMatricula[] = ['retirada', 'anulada', 'trasladada'];
export const ESTADOS_MATRICULA: readonly EstadoMatricula[] = [
  'pendiente',
  'activa',
  'finalizada',
  'retirada',
  'anulada',
  'trasladada'
];

export class MatriculaError extends Error {
  constructor(
    message: string,
    public readonly status: number
  ) {
    super(message);
    this.name = 'MatriculaError';
  }
}

@Injectable({ providedIn: 'root' })
export class MatriculaService {
  private readonly baseUrl = `${environment.apiUrl}/matriculas`;

  constructor(private readonly http: HttpClient) {}

  listar(alumnoId?: string): Observable<Matricula[]> {
    const params: Record<string, string> = {};
    if (alumnoId) params['alumnoId'] = alumnoId;
    return this.http
      .get<Matricula[]>(this.baseUrl, { params })
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  /** Listado paginado y filtrado server-side (PERF-02). Filtra/recorta en el
   *  backend en vez de descargar todas las matrículas. */
  listarPaginado(filtro: FiltroMatriculas = {}): Observable<PaginatedMatriculas> {
    const params = new Map<string, string>();
    if (filtro.alumnoId) params.set('alumnoId', filtro.alumnoId);
    if (filtro.cicloId) params.set('cicloId', filtro.cicloId);
    if (filtro.estado) params.set('estado', filtro.estado);
    if (filtro.page) params.set('page', String(filtro.page));
    if (filtro.pageSize) params.set('pageSize', String(filtro.pageSize));
    return this.http
      .get<PaginatedMatriculas>(this.baseUrl, { params: Object.fromEntries(params) })
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  crear(input: MatriculaInput): Observable<{ id: string }> {
    return this.http
      .post<{ id: string }>(this.baseUrl, input)
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  cambiarEstado(id: string, cambio: CambioEstadoMatricula): Observable<void> {
    return this.http
      .put<void>(`${this.baseUrl}/${id}/estado`, cambio)
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  private mapError(err: unknown): MatriculaError {
    let status = 0;
    let message = 'No se pudo completar la operación.';
    if (err instanceof HttpErrorResponse) {
      status = err.status;
      const body = err.error as { error?: string } | null;
      if (body?.error) message = body.error;
      switch (status) {
        case 403:
          message = 'No tienes permiso para realizar esta acción.';
          break;
        case 404:
          message = 'El recurso no existe o no pertenece a la institución actual.';
          break;
        case 409:
          message = body?.error ?? 'Ya existe una matrícula para este alumno y ciclo.';
          break;
      }
    }
    return new MatriculaError(message, status);
  }
}

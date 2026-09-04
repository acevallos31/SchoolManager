import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';

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
  estado: string;
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

export interface CambioEstadoMatricula {
  estado: string;
  motivo?: string | null;
}

// Estados terminales: exigen motivo obligatorio antes de aplicar el cambio.
// Coincide con "EstadosTerminales" del MatriculasController y con la RPC
// rpc_cambiar_estado_matricula.
export const ESTADOS_TERMINALES = ['retirada', 'anulada', 'trasladada'];
export const ESTADOS_MATRICULA = ['pendiente', 'activa', 'finalizada', 'retirada', 'anulada', 'trasladada'];

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
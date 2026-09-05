import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export type EstadoResponsable = 'activo' | 'inactivo';

// Solicita la serialización camelCase del API .NET (ResponsableDto).
export interface Responsable {
  id: string;
  personaId: string;
  institucionId: string;
  estado: EstadoResponsable;
  nombres: string;
  apellidos: string;
  tipoIdentificacion: string | null;
  numeroIdentificacion: string | null;
  telefono: string | null;
  correo: string | null;
  createdAt: string;
  fechaDesactivacion: string | null;
  motivoDesactivacion: string | null;
}

// Vínculo Responsable<->Alumno (ResponsableVinculoDto).
export interface ResponsableVinculo {
  id: string;
  responsableId: string;
  parentesco: string | null;
  esPrincipal: boolean;
  accesoFinanciero: boolean;
  estado: EstadoResponsable;
  nombres: string;
  apellidos: string;
  telefono: string | null;
  correo: string | null;
}

export interface CrearResponsableDto {
  institucionId: string;
  nombres: string;
  apellidos: string;
  tipoIdentificacion: string;
  numeroIdentificacion: string;
  telefono?: string | null;
  correo?: string | null;
}

export interface CrearResponsableParaPersonaDto {
  personaId: string;
  institucionId: string;
}

export interface EditarResponsableDto {
  nombres: string;
  apellidos: string;
  telefono?: string | null;
  correo?: string | null;
}

export interface VincularResponsableDto {
  responsableId: string;
  parentesco?: string | null;
  esPrincipal?: boolean;
  accesoFinanciero?: boolean;
}

export interface EditarVinculoDto {
  parentesco?: string | null;
  esPrincipal?: boolean | null;
  accesoFinanciero?: boolean | null;
}

export interface DesactivarDto {
  motivo: string;
}

export interface FiltroResponsables {
  institucionId?: string;
  termino?: string;
  estado?: EstadoResponsable;
  page?: number;
  pageSize?: number;
}

export interface PaginatedResponsables {
  items: Responsable[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export class ResponsablesError extends Error {
  constructor(
    message: string,
    public readonly status: number
  ) {
    super(message);
    this.name = 'ResponsablesError';
  }
}

@Injectable({ providedIn: 'root' })
export class ResponsablesService {
  private readonly baseUrl = `${environment.apiUrl}/responsables`;

  constructor(private readonly http: HttpClient) {}

  /** Listado paginado y filtrado server-side (filtros en el backend). */
  listar(filtro: FiltroResponsables = {}): Observable<PaginatedResponsables> {
    const params = new Map<string, string>();
    if (filtro.institucionId) params.set('institucionId', filtro.institucionId);
    if (filtro.termino) params.set('termino', filtro.termino);
    if (filtro.estado) params.set('estado', filtro.estado);
    if (filtro.page) params.set('page', String(filtro.page));
    if (filtro.pageSize) params.set('pageSize', String(filtro.pageSize));
    return this.http
      .get<PaginatedResponsables>(this.baseUrl, { params: Object.fromEntries(params) })
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  obtenerPorId(id: string): Observable<Responsable> {
    return this.http
      .get<Responsable>(`${this.baseUrl}/${id}`)
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  listarDeAlumno(alumnoId: string): Observable<ResponsableVinculo[]> {
    return this.http
      .get<ResponsableVinculo[]>(`${this.baseUrl}/alumno/${alumnoId}`)
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  crear(dto: CrearResponsableDto): Observable<{ id: string }> {
    return this.http
      .post<{ id: string }>(this.baseUrl, dto)
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  crearParaPersona(dto: CrearResponsableParaPersonaDto): Observable<{ id: string }> {
    return this.http
      .post<{ id: string }>(`${this.baseUrl}/para-persona`, dto)
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  editar(id: string, dto: EditarResponsableDto): Observable<void> {
    return this.http
      .put<void>(`${this.baseUrl}/${id}`, dto)
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  cambiarEstado(id: string, dto: DesactivarDto): Observable<void> {
    return this.http
      .put<void>(`${this.baseUrl}/${id}/estado`, dto)
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  reactivar(id: string): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/${id}/reactivar`, {})
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  vincularAlumno(alumnoId: string, dto: VincularResponsableDto): Observable<{ id: string }> {
    return this.http
      .post<{ id: string }>(`${this.baseUrl}/alumno/${alumnoId}`, dto)
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  editarVinculo(vinculoId: string, dto: EditarVinculoDto): Observable<void> {
    return this.http
      .put<void>(`${this.baseUrl}/vinculo/${vinculoId}`, dto)
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  desactivarVinculo(vinculoId: string, dto: DesactivarDto): Observable<void> {
    return this.http
      .put<void>(`${this.baseUrl}/vinculo/${vinculoId}/desactivar`, dto)
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  reactivarVinculo(vinculoId: string): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/vinculo/${vinculoId}/reactivar`, {})
      .pipe(catchError(err => throwError(() => this.mapError(err))));
  }

  private mapError(err: unknown): ResponsablesError {
    let status = 0;
    let message = 'No se pudo completar la operación.';
    if (err instanceof HttpErrorResponse) {
      status = err.status;
      const body = err.error as { error?: string } | null;
      if (body?.error) message = body.error;
      switch (status) {
        case 400:
          message = body?.error ?? 'Datos inválidos. Revise e intente de nuevo.';
          break;
        case 403:
          message = 'No tienes permiso para realizar esta acción.';
          break;
        case 404:
          message = 'El recurso no existe o no pertenece a la institución actual.';
          break;
        case 409:
          message = body?.error ?? 'Ya existe un responsable con esta identificación.';
          break;
      }
    }
    return new ResponsablesError(message, status);
  }
}
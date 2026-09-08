import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { environment } from '../../environments/environment';

export interface Grado { id: string; nombre: string; orden: number; activo: boolean; }
export interface Jornada { id: string; nombre: string; activo: boolean; }
export interface Seccion {
  id: string; institucionId: string; cicloId: string; gradoId: string; gradoNombre: string;
  jornadaId: string | null; jornadaNombre: string | null; nombre: string; cupo: number | null;
  activo: boolean; fechaDesactivacion: string | null; motivoDesactivacion: string | null;
}
export interface GradoInput { nombre: string; orden: number; }
export interface JornadaInput { nombre: string; }
export interface SeccionInput { cicloId: string; gradoId: string; jornadaId: string | null; nombre: string; cupo: number | null; }

export class EstructuraAcademicaError extends Error {
  constructor(message: string, public readonly code: string) { super(message); this.name = 'EstructuraAcademicaError'; }
}

// Servicio migrado del acceso directo Supabase (Bloque 030E) a la API .NET.
// El backend ya devuelve las entidades en camelCase directamente (id,
// institucionId, cicloId, gradoId, gradoNombre, ...), por lo que los antiguos
// getters que mapeaban rows snake_case desaparecen. Solo cambia el transporte
// (HttpClient contra environment.apiUrl), preservando exactamente las
// interfaces, la clase EstructuraAcademicaError con codigo y las firmas de los
// metodos publicos para no cambiar callers.
@Injectable({ providedIn: 'root' })
export class EstructuraAcademicaService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/estructura-academica`;

  async listarGrados(institucionId?: string): Promise<Grado[]> {
    const params = institucionId ? { institucionId } : undefined;
    try {
      return (await this.http.get<Grado[]>(`${this.baseUrl}/grados`, { params }).toPromise()) ?? [];
    } catch (e) { throw this.mapError(e); }
  }
  async crearGrado(input: GradoInput, institucionId?: string): Promise<string> {
    const body: Record<string, unknown> = { nombre: input.nombre.trim(), orden: input.orden };
    if (institucionId) body.institucionId = institucionId;
    try {
      const r = await this.http.post<{ id: string }>(`${this.baseUrl}/grados`, body).toPromise();
      if (!r?.id) throw new EstructuraAcademicaError('No se pudo completar la operación.', 'UNKNOWN');
      return r.id;
    } catch (e) { throw this.mapError(e); }
  }
  async actualizarGrado(gradoId: string, input: GradoInput, institucionId?: string): Promise<void> {
    const params = institucionId ? { institucionId } : undefined;
    const body = { nombre: input.nombre.trim(), orden: input.orden };
    try {
      await this.http.put<void>(`${this.baseUrl}/grados/${gradoId}`, body, { params }).toPromise();
    } catch (e) { throw this.mapError(e); }
  }
  async desactivarGrado(gradoId: string, institucionId?: string): Promise<void> {
    try {
      await this.http.post<void>(`${this.baseUrl}/grados/${gradoId}/desactivar`, {}).toPromise();
    } catch (e) { throw this.mapError(e); }
  }
  async reactivarGrado(gradoId: string, institucionId?: string): Promise<void> {
    try {
      await this.http.post<void>(`${this.baseUrl}/grados/${gradoId}/reactivar`, {}).toPromise();
    } catch (e) { throw this.mapError(e); }
  }

  async listarJornadas(institucionId?: string): Promise<Jornada[]> {
    const params = institucionId ? { institucionId } : undefined;
    try {
      return (await this.http.get<Jornada[]>(`${this.baseUrl}/jornadas`, { params }).toPromise()) ?? [];
    } catch (e) { throw this.mapError(e); }
  }
  async crearJornada(input: JornadaInput, institucionId?: string): Promise<string> {
    const body: Record<string, unknown> = { nombre: input.nombre.trim() };
    if (institucionId) body.institucionId = institucionId;
    try {
      const r = await this.http.post<{ id: string }>(`${this.baseUrl}/jornadas`, body).toPromise();
      if (!r?.id) throw new EstructuraAcademicaError('No se pudo completar la operación.', 'UNKNOWN');
      return r.id;
    } catch (e) { throw this.mapError(e); }
  }
  async actualizarJornada(jornadaId: string, input: JornadaInput, institucionId?: string): Promise<void> {
    const params = institucionId ? { institucionId } : undefined;
    const body = { nombre: input.nombre.trim() };
    try {
      await this.http.put<void>(`${this.baseUrl}/jornadas/${jornadaId}`, body, { params }).toPromise();
    } catch (e) { throw this.mapError(e); }
  }
  async desactivarJornada(jornadaId: string, institucionId?: string): Promise<void> {
    try {
      await this.http.post<void>(`${this.baseUrl}/jornadas/${jornadaId}/desactivar`, {}).toPromise();
    } catch (e) { throw this.mapError(e); }
  }
  async reactivarJornada(jornadaId: string, institucionId?: string): Promise<void> {
    try {
      await this.http.post<void>(`${this.baseUrl}/jornadas/${jornadaId}/reactivar`, {}).toPromise();
    } catch (e) { throw this.mapError(e); }
  }

  async listarSecciones(cicloId: string, institucionId?: string): Promise<Seccion[]> {
    const params: { cicloId: string; institucionId?: string } = institucionId ? { cicloId, institucionId } : { cicloId };
    try {
      return (await this.http.get<Seccion[]>(`${this.baseUrl}/secciones`, { params }).toPromise()) ?? [];
    } catch (e) { throw this.mapError(e); }
  }
  async crearSeccion(input: SeccionInput, institucionId?: string): Promise<string> {
    const body: Record<string, unknown> = {
      cicloId: input.cicloId, gradoId: input.gradoId, jornadaId: input.jornadaId,
      nombre: input.nombre.trim(), cupo: input.cupo
    };
    if (institucionId) body.institucionId = institucionId;
    try {
      const r = await this.http.post<{ id: string }>(`${this.baseUrl}/secciones`, body).toPromise();
      if (!r?.id) throw new EstructuraAcademicaError('No se pudo completar la operación.', 'UNKNOWN');
      return r.id;
    } catch (e) { throw this.mapError(e); }
  }
  async actualizarSeccion(seccionId: string, input: SeccionInput, institucionId?: string): Promise<void> {
    const params = institucionId ? { institucionId } : undefined;
    const body = {
      cicloId: input.cicloId, gradoId: input.gradoId, jornadaId: input.jornadaId,
      nombre: input.nombre.trim(), cupo: input.cupo
    };
    try {
      await this.http.put<void>(`${this.baseUrl}/secciones/${seccionId}`, body, { params }).toPromise();
    } catch (e) { throw this.mapError(e); }
  }
  async desactivarSeccion(seccionId: string, motivo: string, institucionId?: string): Promise<void> {
    const body = { motivo: motivo.trim() };
    try {
      await this.http.post<void>(`${this.baseUrl}/secciones/${seccionId}/desactivar`, body).toPromise();
    } catch (e) { throw this.mapError(e); }
  }
  async reactivarSeccion(seccionId: string, institucionId?: string): Promise<void> {
    try {
      await this.http.post<void>(`${this.baseUrl}/secciones/${seccionId}/reactivar`, {}).toPromise();
    } catch (e) { throw this.mapError(e); }
  }

  private mapError(err: unknown): EstructuraAcademicaError {
    if (err instanceof EstructuraAcademicaError) return err;
    let status = 0; let message = 'No se pudo completar la operación.';
    if (err instanceof HttpErrorResponse) {
      status = err.status;
      const body = err.error as { error?: string } | null;
      switch (status) {
        case 401: message = 'No tienes permiso para realizar esta operación.'; break;
        case 403: message = 'No tienes permiso para realizar esta operación.'; break;
        case 404: message = 'El grado, jornada o seccion no existe.'; break;
        case 409: message = 'El nombre ya existe o el contexto no es valido.'; break;
        case 400: message = body?.error ?? 'Los datos ingresados no son válidos.'; break;
        default: message = body?.error ?? message; break;
      }
    }
    return new EstructuraAcademicaError(message, String(status));
  }
}

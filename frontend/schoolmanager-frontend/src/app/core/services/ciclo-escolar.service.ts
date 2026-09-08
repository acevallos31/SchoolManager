import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { environment } from '../../environments/environment';

export interface CicloEscolar { id:string; institucionId:string; nombre:string; fechaInicio:string; fechaFin:string; activo:boolean; motivoDesactivacion:string|null; }
export interface PeriodoMatricula { id:string; cicloId:string; nombre:string; tipo:string|null; fechaInicio:string; fechaFin:string; activo:boolean; }
export interface CicloInput { nombre:string; fechaInicio:string; fechaFin:string; }
export interface PeriodoInput { nombre:string; tipo:string|null; fechaInicio:string; fechaFin:string; }

export class CicloEscolarError extends Error {
  constructor(message:string, public readonly code:string) { super(message); this.name='CicloEscolarError'; }
}

// Servicio migrado del acceso directo Supabase (Bloque 030D) a la API .NET.
// El backend delega en las RPC 014 de ciclos/períodos; este servicio solo
// cambia el transporte (HttpClient contra environment.apiUrl), preservando
// exactamente los shapes y el comportamiento (CicloEscolarError con codigo)
// para no cambiar callers.
@Injectable({providedIn:'root'})
export class CicloEscolarService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/ciclos-escolares`;

  async listar(institucionId?:string):Promise<CicloEscolar[]> {
    const params = institucionId ? { institucionId } : undefined;
    try {
      return (await this.http.get<CicloEscolar[]>(this.baseUrl, { params }).toPromise()) ?? [];
    } catch (e) { throw this.mapError(e); }
  }

  async crear(input:CicloInput, institucionId?:string):Promise<string> {
    const body: Record<string, unknown> = {
      nombre: input.nombre.trim(),
      fechaInicio: input.fechaInicio,
      fechaFin: input.fechaFin
    };
    if (institucionId) body.institucionId = institucionId;
    try {
      const r = await this.http.post<{ id:string }>(this.baseUrl, body).toPromise();
      if (!r?.id) throw new CicloEscolarError('No se pudo completar la operación.','UNKNOWN');
      return r.id;
    } catch (e) { throw this.mapError(e); }
  }

  async actualizar(ciclo:CicloEscolar, input:CicloInput):Promise<void> {
    const body = { nombre: input.nombre.trim(), fechaInicio: input.fechaInicio, fechaFin: input.fechaFin };
    try {
      await this.http.put<void>(`${this.baseUrl}/${ciclo.id}`, body).toPromise();
    } catch (e) { throw this.mapError(e); }
  }

  async desactivar(id:string, motivo:string):Promise<void> {
    try {
      await this.http.post<void>(`${this.baseUrl}/${id}/desactivar`, { motivo: motivo.trim() }).toPromise();
    } catch (e) { throw this.mapError(e); }
  }

  async reactivar(id:string):Promise<void> {
    try {
      await this.http.post<void>(`${this.baseUrl}/${id}/reactivar`, {}).toPromise();
    } catch (e) { throw this.mapError(e); }
  }

  async listarPeriodos(cicloId:string):Promise<PeriodoMatricula[]> {
    try {
      return (await this.http.get<PeriodoMatricula[]>(`${this.baseUrl}/${cicloId}/periodos`).toPromise()) ?? [];
    } catch (e) { throw this.mapError(e); }
  }

  async crearPeriodo(cicloId:string, input:PeriodoInput):Promise<string> {
    const body = {
      nombre: input.nombre.trim(),
      tipo: this.blank(input.tipo),
      fechaInicio: input.fechaInicio,
      fechaFin: input.fechaFin
    };
    try {
      const r = await this.http.post<{ id:string }>(`${this.baseUrl}/${cicloId}/periodos`, body).toPromise();
      if (!r?.id) throw new CicloEscolarError('No se pudo completar la operación.','UNKNOWN');
      return r.id;
    } catch (e) { throw this.mapError(e); }
  }

  async actualizarPeriodo(periodo:PeriodoMatricula, input:PeriodoInput):Promise<void> {
    const body = {
      nombre: input.nombre.trim(),
      tipo: this.blank(input.tipo),
      fechaInicio: input.fechaInicio,
      fechaFin: input.fechaFin
    };
    try {
      await this.http.put<void>(`${this.baseUrl}/${periodo.cicloId}/periodos/${periodo.id}`, body).toPromise();
    } catch (e) { throw this.mapError(e); }
  }

  async desactivarPeriodo(cicloId:string, id:string):Promise<void> {
    try {
      await this.http.post<void>(`${this.baseUrl}/${cicloId}/periodos/${id}/desactivar`, {}).toPromise();
    } catch (e) { throw this.mapError(e); }
  }

  async reactivarPeriodo(cicloId:string, id:string):Promise<void> {
    try {
      await this.http.post<void>(`${this.baseUrl}/${cicloId}/periodos/${id}/reactivar`, {}).toPromise();
    } catch (e) { throw this.mapError(e); }
  }

  private blank(v:string|null):string|null { const x=v?.trim(); return x||null; }

  private mapError(err:unknown):CicloEscolarError {
    if (err instanceof CicloEscolarError) return err;
    let status = 0; let message = 'No se pudo completar la operación.';
    if (err instanceof HttpErrorResponse) {
      status = err.status;
      const body = err.error as { error?:string } | null;
      switch (status) {
        case 401: message='No tienes permiso para realizar esta operación.'; break;
        case 403: message='No tienes permiso para realizar esta operación.'; break;
        case 404: message='El ciclo o período no existe.'; break;
        case 409: message=body?.error ?? 'Ya existe un registro con ese nombre.'; break;
        case 400: message=body?.error ?? 'Las fechas o datos no son válidos.'; break;
        default: message=body?.error ?? message; break;
      }
    }
    return new CicloEscolarError(message, String(status));
  }
}

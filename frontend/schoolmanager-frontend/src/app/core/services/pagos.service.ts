import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

// Cabecera de un pago (rpc_listar_pagos_alumno / rpc_obtener_pago). 021: cada
// pago es un hecho transaccional aplicado a uno o varios cargos del alumno.
export interface Pago {
  id: string;
  institucionId: string;
  alumnoId: string;
  responsableId: string | null;
  numeroRecibo: number;
  montoTotal: number;
  fechaPago: string;
  metodoPago: string | null;
  referenciaExterna: string | null;
  estado: 'registrado' | 'anulado';
  registradoPor: string | null;
  fechaAnulacion: string | null;
  anuladoPor: string | null;
  motivoAnulacion: string | null;
  createdAt: string;
}

// Detalle de un pago sobre un cargo (rpc_obtener_aplicaciones_pago).
export interface AplicacionPago {
  aplicacionId: string;
  pagoId: string;
  cargoId: string;
  institucionId: string;
  montoAplicado: number;
  estado: 'vigente' | 'reversada';
  fechaReversion: string | null;
  cargoEstado: string;
  conceptoNombre: string | null;
  montoOriginal: number;
}

export interface AplicacionRequest {
  cargoId: string;
  monto: number;
}

export interface RegistrarPagoRequest {
  montoTotal: number;
  aplicaciones: AplicacionRequest[];
  responsableId?: string | null;
  metodoPago?: string | null;
  referenciaExterna?: string | null;
  fechaPago?: string | null;
}

export class PagoError extends Error {
  constructor(message: string) { super(message); this.name = 'PagoError'; }
}

@Injectable({ providedIn: 'root' })
export class PagosService {
  private readonly baseUrl = `${environment.apiUrl}/pagos`;

  constructor(private http: HttpClient) {}

  // Lista los pagos de un alumno. La institucion se resuelve del claim del
  // token en el backend; no se envia institucionId (patron de cargos).
  listarPagosAlumno(alumnoId: string): Promise<Pago[]> {
    return this.peticion(() =>
      this.http.get<Pago[]>(`${this.baseUrl}/alumno/${alumnoId}`).toPromise()
    );
  }

  obtenerPago(pagoId: string): Promise<Pago> {
    return this.peticion(() =>
      this.http.get<Pago>(`${this.baseUrl}/${pagoId}`).toPromise()
    );
  }

  obtenerAplicaciones(pagoId: string): Promise<AplicacionPago[]> {
    return this.peticion(() =>
      this.http.get<AplicacionPago[]>(`${this.baseUrl}/${pagoId}/aplicaciones`).toPromise()
    );
  }

  registrarPago(alumnoId: string, dto: RegistrarPagoRequest): Promise<{ id: string }> {
    return this.peticion(() =>
      this.http.post<{ id: string }>(`${this.baseUrl}/alumno/${alumnoId}`, dto).toPromise()
    );
  }

  anularPago(pagoId: string, motivo: string): Promise<void> {
    return this.http.post<void>(`${this.baseUrl}/${pagoId}/anular`, { motivo }).toPromise();
  }

  private async peticion<T>(accion: () => Promise<T | undefined | null>): Promise<T> {
    try {
      const resultado = await accion();
      if (resultado === undefined || resultado === null) {
        throw new PagoError('La API no devolvió datos.');
      }
      return resultado;
    } catch (e: unknown) { throw this.aError(e); }
  }

  private aError(e: unknown): PagoError {
    if (e instanceof PagoError) return e;
    const err = e as { status?: number; error?: { error?: string } };
    return new PagoError(err?.error?.error ?? 'No se pudo completar la operación.');
  }
}

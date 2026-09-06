import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

// Bloque 022 (SOLO LECTURA): portal del responsable financiero (padre). Consume
// la API .NET a traves de endpoints dedicados. Ningun metodo de este servicio
// escribe ni modifica datos; la institucion la resuelve el backend desde el
// claim del token, por lo que NO se envia institucionId.

// Hijos del usuario autenticado como responsable financiero activo.
export interface MisAlumno {
  id: string;
  institucionId: string;
  nombres: string | null;
  apellidos: string | null;
  parentesco: string | null;
  esPrincipal: boolean;
}

// Resumen financiero de un alumno (totales agregados).
export interface ResumenFinanciero {
  alumnoId: string;
  institucionId: string;
  totalObligaciones: number;
  totalMontoOriginal: number;
  totalPendiente: number;
  totalVencido: number;
  totalAnulado: number;
  totalAplicado: number;
}

// Cargo de un alumno.
export interface Cargo {
  id: string;
  matriculaId: string;
  alumnoId: string;
  planPagoId: string;
  orden: number;
  conceptoId: string | null;
  conceptoNombre: string | null;
  descripcion: string | null;
  montoOriginal: number;
  fechaVencimiento: string;
  estado: 'pendiente' | 'parcial' | 'pagado' | 'anulado';
  fechaGeneracion: string;
  fechaAnulacion: string | null;
  motivoAnulacion: string | null;
  esVencido: boolean;
  saldo: number;
  aplicado: number;
}

// Pago (cabecera) registrado para un alumno.
export interface Pago {
  id: string;
  institucionId: string;
  alumnoId: string;
  responsableId: string;
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

// Aplicacion de un pago sobre un cargo.
export interface AplicacionPago {
  aplicacionId: string;
  pagoId: string;
  cargoId: string;
  institucionId: string;
  montoAplicado: number;
  estado: string;
  fechaReversion: string | null;
  cargoEstado: string;
  conceptoNombre: string | null;
  montoOriginal: number;
}

export class PortalResponsableError extends Error {
  constructor(message: string) { super(message); this.name = 'PortalResponsableError'; }
}

@Injectable({ providedIn: 'root' })
export class PortalResponsableService {
  private readonly baseUrl = `${environment.apiUrl}/PortalResponsable`;

  constructor(private http: HttpClient) {}

  // Hijos del responsable financiero activo (puede venir vacio).
  misAlumnos(): Promise<MisAlumno[]> {
    return this.peticion(() =>
      this.http.get<MisAlumno[]>(`${this.baseUrl}/mis-alumnos`).toPromise()
    );
  }

  resumenAlumno(alumnoId: string): Promise<ResumenFinanciero> {
    return this.peticion(() =>
      this.http.get<ResumenFinanciero>(`${this.baseUrl}/alumnos/${alumnoId}/resumen`).toPromise()
    );
  }

  cargosAlumno(alumnoId: string): Promise<Cargo[]> {
    return this.peticion(() =>
      this.http.get<Cargo[]>(`${this.baseUrl}/alumnos/${alumnoId}/cargos`).toPromise()
    );
  }

  pagosAlumno(alumnoId: string): Promise<Pago[]> {
    return this.peticion(() =>
      this.http.get<Pago[]>(`${this.baseUrl}/alumnos/${alumnoId}/pagos`).toPromise()
    );
  }

  aplicacionesPago(pagoId: string): Promise<AplicacionPago[]> {
    return this.peticion(() =>
      this.http.get<AplicacionPago[]>(`${this.baseUrl}/pagos/${pagoId}/aplicaciones`).toPromise()
    );
  }

  private async peticion<T>(accion: () => Promise<T | undefined | null>): Promise<T> {
    try {
      const resultado = await accion();
      if (resultado === undefined || resultado === null) {
        throw new PortalResponsableError('La API no devolvió datos.');
      }
      return resultado;
    } catch (e: unknown) { throw this.aError(e); }
  }

  private aError(e: unknown): PortalResponsableError {
    if (e instanceof PortalResponsableError) return e;
    const err = e as { status?: number; error?: { error?: string } };
    return new PortalResponsableError(err?.error?.error ?? 'No se pudo completar la operación.');
  }
}

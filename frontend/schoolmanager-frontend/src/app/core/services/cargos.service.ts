import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

// Cargo/obligacion generado a partir de una cuota de un plan de pago.
// En 020 el estado es 'pendiente' | 'anulado'; el vencido es derivado por
// fecha (esVencido), NO persistido. pagado/parcial quedan para 021.
export interface Cargo {
  id: string;
  matriculaId: string;
  alumnoId: string;
  planPagoId: string | null;
  orden: number;
  conceptoId: string | null;
  conceptoNombre: string | null;
  descripcion: string | null;
  montoOriginal: number;
  fechaVencimiento: string; // yyyy-MM-dd
  estado: 'pendiente' | 'anulado';
  fechaGeneracion: string;
  fechaAnulacion: string | null;
  motivoAnulacion: string | null;
  esVencido: boolean;
}

// Resumen financiero de un alumno (rpc_resumen_financiero_alumno).
export interface ResumenFinanciero {
  alumnoId: string;
  institucionId: string;
  totalObligaciones: number;
  totalMontoOriginal: number;
  totalPendiente: number;
  totalVencido: number;
  totalAnulado: number;
}

export class CargoError extends Error {
  constructor(message: string) { super(message); this.name = 'CargoError'; }
}

@Injectable({ providedIn: 'root' })
export class CargosService {
  private readonly baseUrl = `${environment.apiUrl}/cargos`;

  constructor(private http: HttpClient) {}

  // Lista los cargos de un alumno (todas sus matrículas). La institución se
  // resuelve desde el claim del token en el backend; no se envía institucionId.
  listarCargosAlumno(alumnoId: string): Promise<Cargo[]> {
    return this.peticion(() =>
      this.http.get<Cargo[]>(`${this.baseUrl}/alumno/${alumnoId}`).toPromise()
    );
  }

  obtenerResumenAlumno(alumnoId: string): Promise<ResumenFinanciero> {
    return this.peticion(() =>
      this.http.get<ResumenFinanciero>(`${this.baseUrl}/alumno/${alumnoId}/resumen`).toPromise()
    );
  }

  private async peticion<T>(accion: () => Promise<T | undefined | null>): Promise<T> {
    try {
      const resultado = await accion();
      if (resultado === undefined || resultado === null) {
        throw new CargoError('La API no devolvió datos.');
      }
      return resultado;
    } catch (e: unknown) { throw this.aError(e); }
  }

  private aError(e: unknown): CargoError {
    if (e instanceof CargoError) return e;
    const err = e as { status?: number; error?: { error?: string } };
    return new CargoError(err?.error?.error ?? 'No se pudo completar la operación.');
  }
}

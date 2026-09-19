import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { Cargo, ResumenFinanciero } from './cargos.service';
import { Pago } from './pagos.service';

// Identidad de la institución emisora (estado de cuenta 047B).
export interface EstadoCuentaInstitucion {
  id: string;
  nombre: string;
  nombreCorto: string | null;
  direccion: string | null;
  telefono: string | null;
  correo: string | null;
  logoUrl: string | null;
}

// Identidad del alumno (estado de cuenta 047B).
export interface EstadoCuentaAlumno {
  id: string;
  nombreCompleto: string;
  rne: string | null;
  codigoInterno: string | null;
}

// DTO autoritativo de estado de cuenta compuesto por el backend (047B):
// resumen financiero + cargos + histórico de pagos + alumno + institución,
// leídos en una única transacción. El frontend solo lo presenta/imprime/
// descarga; no recalcula saldos ni aplica lógica financiera.
export interface EstadoCuenta {
  institucion: EstadoCuentaInstitucion;
  alumno: EstadoCuentaAlumno;
  resumen: ResumenFinanciero;
  cargos: Cargo[];
  pagos: Pago[];
}

export class EstadoCuentaError extends Error {
  constructor(message: string) { super(message); this.name = 'EstadoCuentaError'; }
}

@Injectable({ providedIn: 'root' })
export class EstadoCuentaService {
  private readonly baseUrl = `${environment.apiUrl}/estado-cuenta`;

  constructor(private http: HttpClient) {}

  // Obtiene el estado de cuenta completo de un alumno. La institución se
  // resuelve desde el claim del token en el backend; no se envía institucionId.
  obtenerEstadoCuenta(alumnoId: string): Promise<EstadoCuenta> {
    return this.peticion(() =>
      this.http.get<EstadoCuenta>(`${this.baseUrl}/alumno/${alumnoId}`).toPromise()
    );
  }

  private async peticion<T>(accion: () => Promise<T | undefined | null>): Promise<T> {
    try {
      const resultado = await accion();
      if (resultado === undefined || resultado === null) {
        throw new EstadoCuentaError('La API no devolvió datos.');
      }
      return resultado;
    } catch (e: unknown) { throw this.aError(e); }
  }

  private aError(e: unknown): EstadoCuentaError {
    if (e instanceof EstadoCuentaError) return e;
    const err = e as { status?: number; error?: { error?: string } };
    return new EstadoCuentaError(err?.error?.error ?? 'No se pudo completar la operación.');
  }
}

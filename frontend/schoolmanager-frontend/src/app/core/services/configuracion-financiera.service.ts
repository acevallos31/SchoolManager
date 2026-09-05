import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

// ===== Conceptos financieros =====
export interface ConceptoFinanciero {
  id: string;
  nombre: string;
  descripcion: string | null;
  monto: number;
  activo: boolean;
  fechaDesactivacion: string | null;
  motivoDesactivacion: string | null;
}

export interface ConceptoInput {
  nombre: string;
  monto: number;
  descripcion?: string | null;
}

// ===== Planes de pago =====
export interface PlanCuota {
  id?: string | null;
  orden: number;
  conceptoId: string | null;
  conceptoNombre?: string | null;
  descripcion: string | null;
  monto: number;
  vencimientoDias: number;
}

export interface PlanPago {
  id: string;
  nombre: string;
  descripcion: string | null;
  activo: boolean;
  fechaDesactivacion: string | null;
  motivoDesactivacion: string | null;
  totalCuotas: number;
  montoTotal: number;
}

export interface PlanPagoDetalle extends PlanPago {
  cuotas: PlanCuota[];
}

export interface PlanInput {
  nombre: string;
  descripcion?: string | null;
  cuotas: PlanCuota[];
}

export interface DesactivarInput {
  motivo: string;
}

// Error normalizado con el mensaje devuelto por la API.
export class ConfiguracionFinancieraError extends Error {
  constructor(message: string) { super(message); this.name = 'ConfiguracionFinancieraError'; }
}

@Injectable({ providedIn: 'root' })
export class ConfiguracionFinancieraService {
  private readonly baseUrl = `${environment.apiUrl}`;

  constructor(private http: HttpClient) {}

  // ===== Conceptos financieros =====
  listarConceptos(activo?: boolean): Promise<ConceptoFinanciero[]> {
    const params: string[] = [];
    if (activo !== undefined) params.push(`activo=${activo}`);
    const q = params.length ? `?${params.join('&')}` : '';
    return this.peticion(() => this.http.get<ConceptoFinanciero[]>(`${this.baseUrl}/conceptosfinancieros${q}`).toPromise());
  }

  crearConcepto(input: ConceptoInput): Promise<{ id: string }> {
    return this.peticion(() => this.http.post<{ id: string }>(`${this.baseUrl}/conceptosfinancieros`, input).toPromise());
  }

  actualizarConcepto(id: string, input: ConceptoInput): Promise<void> {
    return this.peticionSinBody(() => this.http.put<void>(`${this.baseUrl}/conceptosfinancieros/${id}`, input).toPromise());
  }

  desactivarConcepto(id: string, motivo: string): Promise<void> {
    return this.peticionSinBody(() => this.http.delete<void>(
      `${this.baseUrl}/conceptosfinancieros/${id}`, { body: { motivo } as DesactivarInput }).toPromise());
  }

  reactivarConcepto(id: string): Promise<void> {
    return this.peticionSinBody(() => this.http.post<void>(`${this.baseUrl}/conceptosfinancieros/${id}/reactivar`, {}).toPromise());
  }

  // ===== Planes de pago =====
  listarPlanes(activo?: boolean): Promise<PlanPago[]> {
    const params: string[] = [];
    if (activo !== undefined) params.push(`activo=${activo}`);
    const q = params.length ? `?${params.join('&')}` : '';
    return this.peticion(() => this.http.get<PlanPago[]>(`${this.baseUrl}/planesPago${q}`).toPromise());
  }

  obtenerPlan(id: string): Promise<PlanPagoDetalle> {
    return this.peticion(() => this.http.get<PlanPagoDetalle>(`${this.baseUrl}/planesPago/${id}`).toPromise());
  }

  crearPlan(input: PlanInput): Promise<{ id: string }> {
    return this.peticion(() => this.http.post<{ id: string }>(`${this.baseUrl}/planesPago`, input).toPromise());
  }

  // Reemplaza las cuotas del plan de forma atómica.
  actualizarPlan(id: string, input: PlanInput): Promise<void> {
    return this.peticionSinBody(() => this.http.put<void>(`${this.baseUrl}/planesPago/${id}`, input).toPromise());
  }

  desactivarPlan(id: string, motivo: string): Promise<void> {
    return this.peticionSinBody(() => this.http.delete<void>(
      `${this.baseUrl}/planesPago/${id}`, { body: { motivo } as DesactivarInput }).toPromise());
  }

  reactivarPlan(id: string): Promise<void> {
    return this.peticionSinBody(() => this.http.post<void>(`${this.baseUrl}/planesPago/${id}/reactivar`, {}).toPromise());
  }

  // Centraliza la conversión de errores HTTP al mensaje de la API.
      private async peticionSinBody(accion: () => Promise<unknown>): Promise<void> {
        try { await accion(); }
        catch (e: unknown) { throw this.aError(e); }
      }

      private async peticion<T>(accion: () => Promise<T | undefined | null>): Promise<T> {
        try {
          const resultado = await accion();
          if (resultado === undefined || resultado === null) {
            throw new ConfiguracionFinancieraError('La API no devolvió datos.');
          }
          return resultado;
        } catch (e: unknown) { throw this.aError(e); }
      }

      private aError(e: unknown): ConfiguracionFinancieraError {
        if (e instanceof ConfiguracionFinancieraError) return e;
        const err = e as { status?: number; error?: { error?: string } };
        return new ConfiguracionFinancieraError(err?.error?.error ?? 'No se pudo completar la operación.');
      }
}
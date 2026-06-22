import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface Mensualidad {
  id: string;
  alumnoId: string;
  mes: number;
  anio: number;
  monto: number;
  fechaVencimiento: string;
  estado: 'pendiente' | 'pagada' | 'vencida';
}

@Injectable({ providedIn: 'root' })
export class MensualidadService {
  private readonly baseUrl = `${environment.apiUrl}/mensualidades`;

  constructor(private http: HttpClient) {}

  listar(alumnoId?: string, estado?: string): Observable<Mensualidad[]> {
    let params: any = {};
    if (alumnoId) params.alumnoId = alumnoId;
    if (estado) params.estado = estado;
    return this.http.get<Mensualidad[]>(this.baseUrl, { params });
  }

  obtenerPorId(id: string): Observable<Mensualidad> {
    return this.http.get<Mensualidad>(`${this.baseUrl}/${id}`);
  }

  crear(mensualidad: Partial<Mensualidad>): Observable<Mensualidad> {
    return this.http.post<Mensualidad>(this.baseUrl, mensualidad);
  }

  generarAnioEscolar(alumnoId: string, anioEscolar: number, montoMensual: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/generar-anio`, { alumnoId, anioEscolar, montoMensual });
  }
}

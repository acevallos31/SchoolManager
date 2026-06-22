import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface Alumno {
  id: string;
  nombre: string;
  apellido: string;
  fechaNacimiento: string;
  grado: string;
  seccion?: string;
  activo: boolean;
}

@Injectable({ providedIn: 'root' })
export class AlumnoService {
  private readonly baseUrl = `${environment.apiUrl}/alumnos`;

  constructor(private http: HttpClient) {}

  listar(): Observable<Alumno[]> {
    return this.http.get<Alumno[]>(this.baseUrl);
  }

  obtenerPorId(id: string): Observable<Alumno> {
    return this.http.get<Alumno>(`${this.baseUrl}/${id}`);
  }

  crear(alumno: Partial<Alumno>): Observable<Alumno> {
    return this.http.post<Alumno>(this.baseUrl, alumno);
  }

  actualizar(id: string, alumno: Partial<Alumno>): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, alumno);
  }

  eliminar(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}

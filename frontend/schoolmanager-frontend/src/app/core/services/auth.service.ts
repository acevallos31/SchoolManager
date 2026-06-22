import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface SesionUsuario {
  accessToken: string;
  rol: 'admin' | 'padre';
  nombreCompleto: string;
  userId: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly storageKey = 'schoolmanager_sesion';
  private sesionSubject = new BehaviorSubject<SesionUsuario | null>(this.leerSesionGuardada());
  public sesion$ = this.sesionSubject.asObservable();

  constructor(private http: HttpClient) {}

  // Inicia sesión contra el endpoint de Auth de Supabase
  login(email: string, password: string): Observable<any> {
    const url = `${environment.supabaseUrl}/auth/v1/token?grant_type=password`;
    return this.http
      .post(url, { email, password }, { headers: { apikey: environment.supabaseAnonKey } })
      .pipe(tap((respuesta: any) => this.guardarSesion(respuesta)));
  }

  logout(): void {
    localStorage.removeItem(this.storageKey);
    this.sesionSubject.next(null);
  }

  getToken(): string | null {
    return this.sesionSubject.value?.accessToken ?? null;
  }

  getRol(): 'admin' | 'padre' | null {
    return this.sesionSubject.value?.rol ?? null;
  }

  estaAutenticado(): boolean {
    return !!this.getToken();
  }

  private guardarSesion(respuesta: any): void {
    // TODO: adaptar el mapeo según la respuesta real de Supabase Auth + tu API
    const sesion: SesionUsuario = {
      accessToken: respuesta.access_token,
      rol: respuesta.rol ?? 'padre',
      nombreCompleto: respuesta.nombre_completo ?? '',
      userId: respuesta.user?.id ?? ''
    };
    localStorage.setItem(this.storageKey, JSON.stringify(sesion));
    this.sesionSubject.next(sesion);
  }

  private leerSesionGuardada(): SesionUsuario | null {
    const data = localStorage.getItem(this.storageKey);
    return data ? JSON.parse(data) : null;
  }
}

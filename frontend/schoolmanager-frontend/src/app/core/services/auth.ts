import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';

const REQUEST_TIMEOUT_MS = 30000;

export type RolUsuario = 'admin' | 'operador' | 'usuario' | 'padre';

export interface UsuarioActual {
  id: string;
  supabaseUid: string;
  nombre: string;
  nombreCompleto?: string;
  correo?: string;
  rol: RolUsuario;
}

export interface SesionUsuario {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
  usuario: UsuarioActual;
}

export class AuthAppError extends Error {
  constructor(
    message: string,
    public readonly code:
      | 'INVALID_CREDENTIALS'
      | 'EMAIL_NOT_CONFIRMED'
      | 'SESSION_NOT_FOUND'
      | 'USER_PROFILE_NOT_FOUND'
      | 'USER_PROFILE_ERROR'
      | 'REQUEST_TIMEOUT'
      | 'UNKNOWN'
  ) {
    super(message);
    this.name = 'AuthAppError';
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly storageKey = 'schoolmanager_sesion';
  private sessionSubject = new BehaviorSubject<SesionUsuario | null>(this.leerSesionGuardada());
  session$ = this.sessionSubject.asObservable();

  async login(correo: string, password: string): Promise<UsuarioActual> {
    const email = correo.trim().toLowerCase();

    try {
      const response = await this.withTimeout(
        fetch(`${environment.apiUrl}/auth/login`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify({ correo: email, password })
        }),
        'La autenticacion esta tardando demasiado. Si el backend esta en Render Free, espera unos segundos e intenta otra vez.'
      );

      if (!response.ok) {
        throw await this.mapApiAuthError(response);
      }

      const session = (await response.json()) as SesionUsuario;

      if (!session.accessToken || !session.usuario) {
        throw new AuthAppError('No se recibio una sesion valida desde el backend.', 'SESSION_NOT_FOUND');
      }

      this.guardarSesion(session);
      return session.usuario;
    } catch (error) {
      if (error instanceof AuthAppError) {
        throw error;
      }

      console.error('Error inesperado durante el login:', error);
      throw new AuthAppError('No se pudo iniciar sesion. Intenta nuevamente.', 'UNKNOWN');
    }
  }

  async logout(): Promise<void> {
    localStorage.removeItem(this.storageKey);
    this.sessionSubject.next(null);
  }

  isLoggedIn(): boolean {
    return !!this.sessionSubject.value;
  }

  estaAutenticado(): boolean {
    return this.isLoggedIn();
  }

  getToken(): string | null {
    return this.sessionSubject.value?.accessToken ?? null;
  }

  getRol(): RolUsuario | null {
    return this.sessionSubject.value?.usuario.rol ?? null;
  }

  async apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
    const token = this.getToken();
    const headers = new Headers(init.headers);

    if (!headers.has('Content-Type') && init.body) {
      headers.set('Content-Type', 'application/json');
    }

    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    const response = await fetch(`${environment.apiUrl}${path}`, {
      ...init,
      headers
    });

    if (!response.ok) {
      throw new Error(await this.extraerErrorRespuesta(response));
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  async getUsuarioActual(): Promise<UsuarioActual> {
    const session = this.sessionSubject.value;

    if (!session) {
      throw new AuthAppError('No hay una sesion activa.', 'SESSION_NOT_FOUND');
    }

    return session.usuario;
  }

  private async withTimeout<T>(promise: PromiseLike<T>, timeoutMessage: string): Promise<T> {
    let timeoutId: ReturnType<typeof setTimeout> | undefined;

    const timeout = new Promise<never>((_, reject) => {
      timeoutId = setTimeout(() => {
        reject(new AuthAppError(timeoutMessage, 'REQUEST_TIMEOUT'));
      }, REQUEST_TIMEOUT_MS);
    });

    try {
      return await Promise.race([promise, timeout]);
    } finally {
      if (timeoutId) {
        clearTimeout(timeoutId);
      }
    }
  }

  private async mapApiAuthError(response: Response): Promise<AuthAppError> {
    const body = await response
      .json()
      .catch(() => ({ error: 'No se pudo iniciar sesion.' }));
    const message = String(body.error ?? body.message ?? 'No se pudo iniciar sesion.');
    const normalizedMessage = message.toLowerCase();

    if (response.status === 401) {
      return new AuthAppError('Correo o contrasena incorrectos.', 'INVALID_CREDENTIALS');
    }

    if (normalizedMessage.includes('confirm')) {
      return new AuthAppError('Debes confirmar tu correo antes de iniciar sesion.', 'EMAIL_NOT_CONFIRMED');
    }

    if (response.status === 403 && normalizedMessage.includes('registrada')) {
      return new AuthAppError(message, 'USER_PROFILE_NOT_FOUND');
    }

    if (response.status === 403) {
      return new AuthAppError(message, 'USER_PROFILE_ERROR');
    }

    if (response.status === 502) {
      return new AuthAppError(message, 'USER_PROFILE_ERROR');
    }

    return new AuthAppError(message, 'UNKNOWN');
  }

  private async extraerErrorRespuesta(response: Response): Promise<string> {
    const text = await response.text().catch(() => '');
    let body: any = null;

    if (text) {
      try {
        body = JSON.parse(text);
      } catch {
        body = { error: text };
      }
    }

    const message = this.extraerMensajeApi(body);

    if (message !== 'La API no pudo completar la solicitud.') {
      return message;
    }

    if (response.status === 401) {
      return 'Tu sesion expiro o no tienes acceso. Inicia sesion nuevamente.';
    }

    if (response.status === 403) {
      return 'No tienes permiso para realizar esta accion.';
    }

    if (response.status >= 500) {
      return 'El servidor no pudo completar la solicitud. Revisa los logs del backend.';
    }

    return `${message} Codigo HTTP ${response.status}.`;
  }

  private extraerMensajeApi(body: any): string {
    if (Array.isArray(body?.errors)) {
      return body.errors.map((item: unknown) => String(item)).join(' ');
    }

    if (body?.errors && typeof body.errors === 'object') {
      const mensajes = Object.values(body.errors)
        .flatMap((value: any) => Array.isArray(value) ? value : [value])
        .map((value: unknown) => String(value));

      if (mensajes.length > 0) {
        return mensajes.join(' ');
      }
    }

    return String(body?.error ?? body?.message ?? 'La API no pudo completar la solicitud.');
  }

  private guardarSesion(session: SesionUsuario): void {
    localStorage.setItem(this.storageKey, JSON.stringify(session));
    this.sessionSubject.next(session);
  }

  private leerSesionGuardada(): SesionUsuario | null {
    const data = localStorage.getItem(this.storageKey);

    if (!data) {
      return null;
    }

    try {
      return JSON.parse(data) as SesionUsuario;
    } catch {
      localStorage.removeItem(this.storageKey);
      return null;
    }
  }
}

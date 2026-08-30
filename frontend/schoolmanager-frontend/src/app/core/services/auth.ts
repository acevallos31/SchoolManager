import { inject, Injectable, InjectionToken } from '@angular/core';
import { createClient, SupabaseClient, Session } from '@supabase/supabase-js';
import { BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';

const REQUEST_TIMEOUT_MS = 30000;

export const SUPABASE_CLIENT = new InjectionToken<SupabaseClient>('SUPABASE_CLIENT', {
  providedIn: 'root',
  factory: () =>
    createClient(environment.supabaseUrl, environment.supabaseAnonKey, {
      auth: {
        persistSession: true,
        storageKey: 'schoolmanager-auth',
        storage: window.localStorage
      }
    })
});

export type RolUsuario = 'admin' | 'operador' | 'usuario' | 'padre';

export interface UsuarioActual {
  id: string;
  personaId: string;
  rol: RolUsuario;
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
  public supabase = inject(SUPABASE_CLIENT);
  private sessionSubject = new BehaviorSubject<Session | null>(null);
  private usuarioSubject = new BehaviorSubject<UsuarioActual | null>(null);
  session$ = this.sessionSubject.asObservable();
  usuarioActual$ = this.usuarioSubject.asObservable();

  constructor() {
    this.supabase.auth
      .getSession()
      .then(async ({ data }) => {
        await this.restaurarSesion(data.session);
      })
      .catch(async error => {
        console.error('No se pudo recuperar la sesion existente:', error);
        await this.limpiarSesionInvalida();
      });

    this.supabase.auth.onAuthStateChange((_, session) => {
      this.sessionSubject.next(session);
      if (!session) {
        this.usuarioSubject.next(null);
      }
    });
  }

  async login(correo: string, password: string): Promise<UsuarioActual> {
    const email = correo.trim().toLowerCase();

    try {
      const { data, error } = await this.withTimeout(
        this.supabase.auth.signInWithPassword({
          email,
          password
        }),
        'La autenticacion esta tardando demasiado. Revisa tu conexion e intenta otra vez.'
      );

      if (error) {
        throw this.mapSupabaseAuthError(error);
      }

      if (!data.session) {
        throw new AuthAppError('No se recibio una sesion valida desde Supabase.', 'SESSION_NOT_FOUND');
      }

      this.sessionSubject.next(data.session);
      const usuario = await this.getUsuarioActual(data.session);
      this.usuarioSubject.next(usuario);
      return usuario;
    } catch (error) {
      if (this.sessionSubject.value) {
        await this.limpiarSesionInvalida();
      }

      if (error instanceof AuthAppError) {
        throw error;
      }

      console.error('Error inesperado durante el login:', error);
      throw new AuthAppError('No se pudo iniciar sesion. Intenta nuevamente.', 'UNKNOWN');
    }
  }

  async logout() {
    try {
      await this.supabase.auth.signOut();
    } finally {
      this.sessionSubject.next(null);
      this.usuarioSubject.next(null);
    }
  }

  isLoggedIn(): boolean {
    return !!this.sessionSubject.value;
  }

  getToken(): string | null {
    return this.sessionSubject.value?.access_token ?? null;
  }

  getRol(): RolUsuario | null {
    return this.usuarioSubject.value?.rol ?? null;
  }

  async getUsuarioActual(sessionOverride?: Session): Promise<UsuarioActual> {
    const session = sessionOverride ?? this.sessionSubject.value;

    if (!session) {
      throw new AuthAppError('No hay una sesion activa.', 'SESSION_NOT_FOUND');
    }

    const response = await this.withTimeout(
      fetch(`${environment.apiUrl.replace(/\/$/, '')}/auth/me`, {
        method: 'GET',
        headers: { Authorization: `Bearer ${session.access_token}` }
      }),
      'La consulta del perfil esta tardando demasiado. Intenta otra vez.'
    );

    if (!response.ok) {
      throw new AuthAppError('No se pudo consultar tu perfil de usuario.', 'USER_PROFILE_ERROR');
    }

    const data = (await response.json()) as UsuarioActual;

    if (!data.id || !data.personaId || !this.esRolUsuario(data.rol)) {
      throw new AuthAppError('Tu usuario tiene un rol no reconocido.', 'USER_PROFILE_ERROR');
    }

    return data;
  }

  private async restaurarSesion(session: Session | null): Promise<void> {
    this.sessionSubject.next(session);
    if (!session) {
      this.usuarioSubject.next(null);
      return;
    }

    this.usuarioSubject.next(await this.getUsuarioActual(session));
  }

  private async limpiarSesionInvalida(): Promise<void> {
    try {
      await this.supabase.auth.signOut();
    } finally {
      this.sessionSubject.next(null);
      this.usuarioSubject.next(null);
    }
  }

  private esRolUsuario(rol: string): rol is RolUsuario {
    return rol === 'admin' || rol === 'operador' || rol === 'usuario' || rol === 'padre';
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

  private mapSupabaseAuthError(error: { message?: string; status?: number; code?: string }): AuthAppError {
    const message = (error.message ?? '').toLowerCase();

    if (message.includes('invalid login credentials') || error.status === 400) {
      return new AuthAppError('Correo o contrasena incorrectos.', 'INVALID_CREDENTIALS');
    }

    if (message.includes('email not confirmed')) {
      return new AuthAppError('Debes confirmar tu correo antes de iniciar sesion.', 'EMAIL_NOT_CONFIRMED');
    }

    return new AuthAppError(error.message ?? 'No se pudo iniciar sesion.', 'UNKNOWN');
  }
}

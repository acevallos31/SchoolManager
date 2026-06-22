import { Injectable } from '@angular/core';
import { createClient, SupabaseClient, Session } from '@supabase/supabase-js';
import { BehaviorSubject } from 'rxjs';

const SUPABASE_URL = 'https://nphvszugtwumeeegvahu.supabase.co';
const SUPABASE_KEY = 'sb_publishable_MYmRr445RYIKhQ6JK6Gv4Q_Q68L2Eas';
const REQUEST_TIMEOUT_MS = 12000;

export type RolUsuario = 'admin' | 'padre';

export interface UsuarioActual {
  id: string;
  supabase_uid: string;
  nombre: string;
  nombre_completo?: string;
  correo?: string;
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
  public supabase: SupabaseClient;
  private sessionSubject = new BehaviorSubject<Session | null>(null);
  session$ = this.sessionSubject.asObservable();

  constructor() {
    this.supabase = createClient(SUPABASE_URL, SUPABASE_KEY, {
      auth: {
        persistSession: true,
        storageKey: 'schoolmanager-auth',
        storage: window.localStorage
      }
    });

    this.supabase.auth
      .getSession()
      .then(({ data }) => {
        this.sessionSubject.next(data.session);
      })
      .catch(error => {
        console.error('No se pudo recuperar la sesion existente:', error);
        this.sessionSubject.next(null);
      });

    this.supabase.auth.onAuthStateChange((_, session) => {
      this.sessionSubject.next(session);
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
      return await this.getUsuarioActual(data.session);
    } catch (error) {
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
    }
  }

  isLoggedIn(): boolean {
    return !!this.sessionSubject.value;
  }

  getToken(): string | null {
    return this.sessionSubject.value?.access_token ?? null;
  }

  async getUsuarioActual(sessionOverride?: Session): Promise<UsuarioActual> {
    const session = sessionOverride ?? this.sessionSubject.value;

    if (!session) {
      throw new AuthAppError('No hay una sesion activa.', 'SESSION_NOT_FOUND');
    }

    const { data, error } = await this.withTimeout(
      this.supabase
        .from('usuarios')
        .select('id, supabase_uid, nombre_completo, correo, rol')
        .eq('supabase_uid', session.user.id)
        .maybeSingle(),
      'La consulta del perfil esta tardando demasiado. Intenta otra vez.'
    );

    if (error) {
      console.error('Error obteniendo usuario:', error);
      throw new AuthAppError('No se pudo consultar tu perfil de usuario.', 'USER_PROFILE_ERROR');
    }

    if (!data) {
      throw new AuthAppError(
        'Tu cuenta existe en Supabase Auth, pero no esta registrada en la tabla usuarios.',
        'USER_PROFILE_NOT_FOUND'
      );
    }

    if (data.rol !== 'admin' && data.rol !== 'padre') {
      throw new AuthAppError('Tu usuario tiene un rol no reconocido.', 'USER_PROFILE_ERROR');
    }

    return {
      ...(data as UsuarioActual),
      nombre: data.nombre_completo ?? data.correo ?? 'Usuario'
    };
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

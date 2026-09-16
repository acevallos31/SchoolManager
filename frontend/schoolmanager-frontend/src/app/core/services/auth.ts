import { inject, Injectable, InjectionToken } from '@angular/core';
import { createClient, Session, SupabaseClient } from '@supabase/supabase-js';
import { BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';

const REQUEST_TIMEOUT_MS = 30000;
const EDGE_SESSION_ENDPOINT = '/api/auth/session';

function getBrowserStorage(): Storage | undefined {
  try {
    const storage = window.localStorage;
    return typeof storage?.getItem === 'function' && typeof storage?.setItem === 'function'
      ? storage : undefined;
  } catch {
    return undefined;
  }
}

export const SUPABASE_CLIENT = new InjectionToken<SupabaseClient>('SUPABASE_CLIENT', {
  providedIn: 'root',
  factory: () => {
    const storage = getBrowserStorage();
    return createClient(environment.supabaseUrl, environment.supabaseAnonKey, {
      auth: { persistSession: storage !== undefined, storageKey: 'schoolmanager-auth', storage }
    });
  }
});

export interface AmbitoGlobalAcceso { roles: string[]; permisos: string[]; }
export interface InstitucionAcceso {
  id: string;
  nombre: string;
  nombreCorto: string | null;
  roles: string[];
  permisos: string[];
  activo?: boolean;
}
export interface UsuarioActual {
  id: string;
  personaId: string;
  roles: string[];
  permisos: string[];
  nombreCompleto?: string;
  ambitoGlobal?: AmbitoGlobalAcceso;
  instituciones?: InstitucionAcceso[];
  institucionesAdministrables?: InstitucionAcceso[];
}

export class AuthAppError extends Error {
  constructor(
    message: string,
    public readonly code:
      | 'INVALID_CREDENTIALS' | 'EMAIL_NOT_CONFIRMED' | 'SESSION_NOT_FOUND'
      | 'USER_PROFILE_NOT_FOUND' | 'USER_PROFILE_ERROR' | 'REQUEST_TIMEOUT' | 'UNKNOWN'
  ) {
    super(message);
    this.name = 'AuthAppError';
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  public supabase = inject(SUPABASE_CLIENT);
  private readonly sessionSubject = new BehaviorSubject<Session | null>(null);
  private readonly usuarioSubject = new BehaviorSubject<UsuarioActual | null>(null);
  readonly session$ = this.sessionSubject.asObservable();
  readonly usuarioActual$ = this.usuarioSubject.asObservable();
  private inicializacionPromise: Promise<void> | null = null;
  private mensajeSesionInvalida: string | null = null;

  constructor() {
    this.supabase.auth.onAuthStateChange((event, session) => {
      this.sessionSubject.next(session);
      if (!session) this.usuarioSubject.next(null);
      if (event === 'TOKEN_REFRESHED') {
        void this.sincronizarSesionEdge(session).catch(error =>
          console.error('No se pudo sincronizar la sesion edge:', error));
      } else if (event === 'SIGNED_OUT') {
        void this.limpiarSesionEdgeBestEffort();
      }
    });
  }

  async asegurarUsuarioInicial(): Promise<void> {
    if (!this.inicializacionPromise) {
      this.inicializacionPromise = this.restaurarSesionDesdeStorage();
    }
    return this.inicializacionPromise;
  }

  private async restaurarSesionDesdeStorage(): Promise<void> {
    try {
      const { data } = await this.supabase.auth.getSession();
      await this.restaurarSesion(data?.session ?? null);
    } catch (error) {
      console.error('No se pudo recuperar la sesion existente:', error);
      await this.limpiarSesionInvalida();
    }
  }

  async login(correo: string, password: string): Promise<UsuarioActual> {
    const email = correo.trim().toLowerCase();
    try {
      const { data, error } = await this.withTimeout(
        this.supabase.auth.signInWithPassword({ email, password }),
        'La autenticacion esta tardando demasiado. Revisa tu conexion e intenta otra vez.'
      );
      if (error) throw this.mapSupabaseAuthError(error);
      if (!data.session) {
        throw new AuthAppError('No se recibio una sesion valida desde Supabase.', 'SESSION_NOT_FOUND');
      }
      this.sessionSubject.next(data.session);
      const usuario = await this.getUsuarioActual(data.session);
      await this.sincronizarSesionEdge(data.session);
      this.usuarioSubject.next(usuario);
      return usuario;
    } catch (error) {
      if (this.sessionSubject.value) await this.limpiarSesionInvalida();
      if (error instanceof AuthAppError) throw error;
      console.error('Error inesperado durante el login:', error);
      throw new AuthAppError('No se pudo iniciar sesion. Intenta nuevamente.', 'UNKNOWN');
    }
  }

  async loginWithGoogle(): Promise<void> {
    await this.loginWithOAuth('google', 'Google');
  }

  async loginWithMicrosoft(): Promise<void> {
    await this.loginWithOAuth('azure', 'Microsoft', 'email');
  }

  private async loginWithOAuth(
    provider: 'google' | 'azure', etiqueta: 'Google' | 'Microsoft', scopes?: string
  ): Promise<void> {
    const { error } = await this.supabase.auth.signInWithOAuth({
      provider,
      options: {
        redirectTo: `${window.location.origin}/auth/callback`,
        ...(scopes ? { scopes } : {}),
        queryParams: { prompt: 'select_account' }
      }
    });
    if (error) {
      throw new AuthAppError(`No se pudo iniciar el acceso con ${etiqueta}.`, 'UNKNOWN');
    }
  }

  async logout(): Promise<void> {
    try {
      await this.supabase.auth.signOut();
    } finally {
      this.sessionSubject.next(null);
      this.usuarioSubject.next(null);
      this.mensajeSesionInvalida = null;
      await this.limpiarSesionEdgeBestEffort();
    }
  }

  isLoggedIn(): boolean { return !!this.sessionSubject.value && !!this.usuarioSubject.value; }
  usuarioActual(): UsuarioActual | null { return this.usuarioSubject.value; }
  ambitoGlobal(): AmbitoGlobalAcceso {
    return this.usuarioSubject.value?.ambitoGlobal ?? { roles: [], permisos: [] };
  }
  institucionesDisponibles(): readonly InstitucionAcceso[] {
    return this.usuarioSubject.value?.instituciones ?? [];
  }
  tieneRolEnInstitucion(rol: string, institucionId: string): boolean {
    return this.institucionesDisponibles().find(i => i.id === institucionId)?.roles.includes(rol) ?? false;
  }
  tienePermisoEnInstitucion(permiso: string, institucionId: string): boolean {
    return this.institucionesDisponibles().find(i => i.id === institucionId)?.permisos.includes(permiso) ?? false;
  }
  esSuperadministrador(): boolean {
    const global = this.usuarioSubject.value?.ambitoGlobal;
    return global ? global.roles.includes('platform_admin') : this.tieneRol('platform_admin');
  }
  consumirMensajeSesionInvalida(): string | null {
    const mensaje = this.mensajeSesionInvalida;
    this.mensajeSesionInvalida = null;
    return mensaje;
  }
  mensajeSesionInvalidaPendiente(): string | null { return this.mensajeSesionInvalida; }
  getToken(): string | null { return this.sessionSubject.value?.access_token ?? null; }
  tieneRol(rol: string): boolean { return this.usuarioSubject.value?.roles.includes(rol) ?? false; }
  tienePermiso(permiso: string): boolean { return this.usuarioSubject.value?.permisos.includes(permiso) ?? false; }

  async getUsuarioActual(sessionOverride?: Session): Promise<UsuarioActual> {
    const session = sessionOverride ?? this.sessionSubject.value;
    if (!session) throw new AuthAppError('No hay una sesion activa.', 'SESSION_NOT_FOUND');
    const response = await this.withTimeout(
      fetch(`${environment.apiUrl.replace(/\/$/, '')}/auth/me`, {
        method: 'GET', headers: { Authorization: `Bearer ${session.access_token}` }
      }),
      'La consulta del perfil esta tardando demasiado. Intenta otra vez.'
    );
    if (!response.ok) throw await this.mapearErrorPerfil(response);
    const data = (await response.json()) as UsuarioActual;
    if (!this.esUsuarioActualValido(data)) {
      throw new AuthAppError('El perfil de usuario recibido no es valido.', 'USER_PROFILE_ERROR');
    }
    return data;
  }

  private esUsuarioActualValido(data: UsuarioActual): boolean {
    const lista = (v: unknown): v is string[] => Array.isArray(v) && v.every(x => typeof x === 'string');
    const institucion = (i: InstitucionAcceso) => !!i && typeof i.id === 'string' && !!i.id
      && typeof i.nombre === 'string' && !!i.nombre
      && (i.nombreCorto === null || typeof i.nombreCorto === 'string')
      && lista(i.roles) && lista(i.permisos)
      && (i.activo === undefined || typeof i.activo === 'boolean');
    const globalOk = data.ambitoGlobal === undefined || (!!data.ambitoGlobal
      && lista(data.ambitoGlobal.roles) && lista(data.ambitoGlobal.permisos));
    const institucionesOk = data.instituciones === undefined
      || (Array.isArray(data.instituciones) && data.instituciones.every(institucion));
    const administrablesOk = data.institucionesAdministrables === undefined
      || (Array.isArray(data.institucionesAdministrables)
        && data.institucionesAdministrables.every(institucion));
    return !!data.id && !!data.personaId && lista(data.roles) && lista(data.permisos)
      && (data.nombreCompleto === undefined || typeof data.nombreCompleto === 'string')
      && globalOk && institucionesOk && administrablesOk;
  }

  private async mapearErrorPerfil(response: Response): Promise<AuthAppError> {
    if (response.status === 401) {
      return new AuthAppError('Tu sesion expiro o no es valida.', 'SESSION_NOT_FOUND');
    }
    if (response.status === 403) {
      const codigo = await this.leerCodigoDeError(response);
      if (codigo === 'IDENTIDAD_NO_VINCULADA') {
        return new AuthAppError(
          'Tu identidad externa no esta vinculada a un usuario de SchoolManager. Solicita al administrador que revise tu acceso.',
          'USER_PROFILE_NOT_FOUND'
        );
      }
      if (codigo === 'USUARIO_INACTIVO') {
        return new AuthAppError('Tu usuario esta inactivo. Contacta al administrador.', 'USER_PROFILE_NOT_FOUND');
      }
      return new AuthAppError('Tu cuenta no tiene un perfil de usuario habilitado.', 'USER_PROFILE_NOT_FOUND');
    }
    return new AuthAppError('No se pudo consultar tu perfil de usuario.', 'USER_PROFILE_ERROR');
  }

  private async leerCodigoDeError(response: Response): Promise<string | null> {
    try {
      const cuerpo = (await response.json()) as { codigo?: unknown };
      return typeof cuerpo.codigo === 'string' ? cuerpo.codigo : null;
    } catch {
      return null;
    }
  }

  private async restaurarSesion(session: Session | null): Promise<void> {
    this.sessionSubject.next(session);
    this.mensajeSesionInvalida = null;
    if (!session) {
      this.usuarioSubject.next(null);
      await this.limpiarSesionEdgeBestEffort();
      return;
    }
    try {
      this.usuarioSubject.next(await this.getUsuarioActual(session));
    } catch (error) {
      this.usuarioSubject.next(null);
      this.mensajeSesionInvalida = error instanceof AuthAppError
        ? error.message : 'No se pudo validar la sesion. Regresando al login...';
      return;
    }
    await this.sincronizarSesionEdge(session);
  }

  private async limpiarSesionInvalida(): Promise<void> {
    try {
      await this.supabase.auth.signOut();
    } finally {
      this.sessionSubject.next(null);
      this.usuarioSubject.next(null);
      await this.limpiarSesionEdgeBestEffort();
    }
  }

  private async limpiarSesionEdgeBestEffort(): Promise<void> {
    try {
      await this.sincronizarSesionEdge(null);
    } catch (error) {
      console.error('No se pudo limpiar la sesion edge:', error);
    }
  }

  private async sincronizarSesionEdge(session: Session | null): Promise<void> {
    // El build staging local no ejecuta las funciones Vercel ni su middleware.
    // Producción y desarrollo conservan la cookie edge; staging valida la sesión
    // directamente contra Supabase Auth + API .NET durante el E2E.
    if (!environment.edgeSessionEnabled) {
      return;
    }

    const response = await fetch(EDGE_SESSION_ENDPOINT, {
      method: session ? 'POST' : 'DELETE',
      headers: session ? { Authorization: `Bearer ${session.access_token}` } : undefined,
      credentials: 'same-origin', cache: 'no-store'
    });
    if (!response.ok) {
      throw new AuthAppError('No se pudo establecer la sesion segura del servidor.', 'SESSION_NOT_FOUND');
    }
  }

  private async withTimeout<T>(promise: PromiseLike<T>, message: string): Promise<T> {
    let timeoutId: ReturnType<typeof setTimeout> | undefined;
    const timeout = new Promise<never>((_, reject) => {
      timeoutId = setTimeout(() => reject(new AuthAppError(message, 'REQUEST_TIMEOUT')), REQUEST_TIMEOUT_MS);
    });
    try {
      return await Promise.race([promise, timeout]);
    } finally {
      if (timeoutId) clearTimeout(timeoutId);
    }
  }

  private mapSupabaseAuthError(error: { message?: string; status?: number }): AuthAppError {
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

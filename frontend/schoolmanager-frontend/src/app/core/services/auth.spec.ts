import { Session, SupabaseClient } from '@supabase/supabase-js';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthAppError, AuthService, SUPABASE_CLIENT } from './auth';

describe('AuthService', () => {
  let service: AuthService;
  let signOut: ReturnType<typeof vi.fn>;

  const session = {
    access_token: 'access-token-prueba',
    user: { id: 'auth-user-id' }
  } as unknown as Session;

  beforeEach(async () => {
    signOut = vi.fn().mockResolvedValue({ error: null });
    const supabase = {
      auth: {
        getSession: vi.fn().mockResolvedValue({ data: { session: null }, error: null }),
        onAuthStateChange: vi.fn().mockReturnValue({
          data: { subscription: { unsubscribe: vi.fn() } }
        }),
        signInWithPassword: vi.fn().mockResolvedValue({
          data: { session, user: session.user },
          error: null
        }),
        signOut
      }
    } as unknown as SupabaseClient;

    TestBed.configureTestingModule({
      providers: [AuthService, { provide: SUPABASE_CLIENT, useValue: supabase }]
    });
    service = TestBed.inject(AuthService);
    await Promise.resolve();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('usa RBAC devuelto por api/auth/me y no consulta public.usuarios', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          id: 'usuario-id',
          personaId: 'persona-id',
          roles: ['admin'],
          permisos: ['academico.alumnos.ver']
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );

    const usuario = await service.login('ADMIN@EJEMPLO.COM', 'password');

    expect(usuario).toEqual({
      id: 'usuario-id',
      personaId: 'persona-id',
      roles: ['admin'],
      permisos: ['academico.alumnos.ver']
    });
    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0][0]).toContain('/api/auth/me');
    expect(service.supabase.from).toBeUndefined();
  });

  it('envia el access token a api/auth/me', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          id: 'usuario-id',
          personaId: 'persona-id',
          roles: ['padre'],
          permisos: []
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );

    await service.login('padre@ejemplo.com', 'password');

    expect(fetchMock.mock.calls[0][1]).toMatchObject({
      headers: { Authorization: 'Bearer access-token-prueba' }
    });
  });

  it('cierra Supabase si api/auth/me falla', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 403 }));

    await expect(service.login('padre@ejemplo.com', 'password')).rejects.toBeInstanceOf(
      AuthAppError
    );
    expect(signOut).toHaveBeenCalledOnce();
    expect(service.getToken()).toBeNull();
    expect(service.tieneRol('padre')).toBe(false);
  });

  it('asegurarUsuarioInicial restaura sesión y carga /auth/me cuando hay sesión', async () => {
    vi.spyOn(service.supabase.auth, 'getSession').mockResolvedValue({
      data: { session },
      error: null
    } as never);
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          id: 'usuario-id',
          personaId: 'persona-id',
          roles: ['admin'],
          permisos: ['academico.responsables.ver']
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );

    await service.asegurarUsuarioInicial();

    expect(service.isLoggedIn()).toBe(true);
    expect(service.tienePermiso('academico.responsables.ver')).toBe(true);
    expect(fetchMock.mock.calls[0][0]).toContain('/api/auth/me');
  });

  it('asegurarUsuarioInicial deja el estado nulo cuando no hay sesión', async () => {
    vi.spyOn(service.supabase.auth, 'getSession').mockResolvedValue({
      data: { session: null },
      error: null
    } as never);
    const fetchMock = vi.spyOn(globalThis, 'fetch');

    await service.asegurarUsuarioInicial();

    expect(service.isLoggedIn()).toBe(false);
    expect(service.tienePermiso('academico.alumnos.ver')).toBe(false);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('un error de /auth/me en asegurarUsuarioInicial no bloquea el bootstrap', async () => {
    vi.spyOn(service.supabase.auth, 'getSession').mockResolvedValue({
      data: { session },
      error: null
    } as never);
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 500 }));

    await expect(service.asegurarUsuarioInicial()).resolves.toBeUndefined();

    expect(service.isLoggedIn()).toBe(false);
    expect(signOut).toHaveBeenCalled();
  });

  it('asegurarUsuarioInicial es idempotente y evita dobles cargas', async () => {
    vi.spyOn(service.supabase.auth, 'getSession').mockResolvedValue({
      data: { session },
      error: null
    } as never);
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          id: 'usuario-id',
          personaId: 'persona-id',
          roles: ['admin'],
          permisos: ['academico.matriculas.ver']
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );

    await Promise.all([service.asegurarUsuarioInicial(), service.asegurarUsuarioInicial()]);

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(service.tienePermiso('academico.matriculas.ver')).toBe(true);
  });
});

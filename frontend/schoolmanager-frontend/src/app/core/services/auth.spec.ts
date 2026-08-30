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
});

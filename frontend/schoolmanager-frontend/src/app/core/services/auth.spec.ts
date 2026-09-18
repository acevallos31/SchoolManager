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

  const perfil = {
    id: 'usuario-id',
    personaId: 'persona-id',
    roles: ['admin'],
    permisos: ['academico.alumnos.ver']
  };

  const perfilContextual = {
    id: 'usuario-contextual',
    personaId: 'persona-contextual',
    roles: ['platform_admin', 'secretaria'],
    permisos: ['platform.roles.ver', 'academico.alumnos.ver'],
    ambitoGlobal: {
      roles: ['platform_admin'],
      permisos: ['platform.roles.ver']
    },
    instituciones: [
      {
        id: 'institucion-1',
        nombre: 'Colegio Prueba',
        nombreCorto: 'CP',
        roles: ['secretaria'],
        permisos: ['academico.alumnos.ver']
      }
    ]
  };

  function esSesionEdge(input: RequestInfo | URL): boolean {
    return String(input) === '/api/auth/session';
  }

  function llamadasA(fetchMock: ReturnType<typeof vi.spyOn>, fragmento: string) {
    const calls = fetchMock.mock.calls as unknown as Array<[RequestInfo | URL, RequestInit?]>;
    return calls.filter(call => String(call[0]).includes(fragmento));
  }

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

  it('usa RBAC devuelto por api/auth/me y sincroniza la sesión edge', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      return new Response(JSON.stringify(perfil), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      });
    });

    const usuario = await service.login('ADMIN@EJEMPLO.COM', 'password');

    expect(usuario).toEqual(perfil);
    expect(llamadasA(fetchMock, '/api/auth/me')).toHaveLength(1);
    expect(llamadasA(fetchMock, '/api/auth/session')).toHaveLength(1);
    expect(llamadasA(fetchMock, '/api/auth/session')[0][1]).toMatchObject({
      method: 'POST',
      headers: { Authorization: 'Bearer access-token-prueba' }
    });
    expect(service.supabase.from).toBeUndefined();
  });

  it('expone ámbito global y permisos de la institución sin mezclarlos', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      return new Response(JSON.stringify(perfilContextual), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      });
    });

    await service.login('admin@ejemplo.com', 'password');

    expect(service.ambitoGlobal().roles).toEqual(['platform_admin']);
    expect(service.esSuperadministrador()).toBe(true);
    expect(service.institucionesDisponibles()).toHaveLength(1);
    expect(service.tieneRolEnInstitucion('secretaria', 'institucion-1')).toBe(true);
    expect(service.tienePermisoEnInstitucion('academico.alumnos.ver', 'institucion-1')).toBe(true);
    expect(service.tienePermisoEnInstitucion('platform.roles.ver', 'institucion-1')).toBe(false);
    expect(service.tienePermisoEnInstitucion('academico.alumnos.ver', 'otra-institucion')).toBe(false);
  });

  it('envia el access token a api/auth/me', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      return new Response(
        JSON.stringify({
          id: 'usuario-id',
          personaId: 'persona-id',
          roles: ['padre'],
          permisos: []
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      );
    });

    await service.login('padre@ejemplo.com', 'password');

    const llamadaAuthMe = llamadasA(fetchMock, '/api/auth/me')[0];
    expect(llamadaAuthMe[1]).toMatchObject({
      headers: { Authorization: 'Bearer access-token-prueba' }
    });
  });

  it('cierra Supabase y limpia estado si api/auth/me falla', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      return new Response(null, { status: 403 });
    });

    await expect(service.login('padre@ejemplo.com', 'password')).rejects.toBeInstanceOf(
      AuthAppError
    );
    expect(signOut).toHaveBeenCalledOnce();
    expect(service.getToken()).toBeNull();
    expect(service.tieneRol('padre')).toBe(false);
  });

  it('si falla la sincronización edge del login, invalida la sesión local', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 500 });
      }
      return new Response(JSON.stringify(perfil), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      });
    });

    await expect(service.login('admin@ejemplo.com', 'password')).rejects.toBeInstanceOf(
      AuthAppError
    );
    expect(signOut).toHaveBeenCalledOnce();
    expect(service.isLoggedIn()).toBe(false);
    expect(service.getToken()).toBeNull();
  });

  it('asegurarUsuarioInicial restaura sesión, carga /auth/me y sincroniza edge', async () => {
    vi.spyOn(service.supabase.auth, 'getSession').mockResolvedValue({
      data: { session },
      error: null
    } as never);
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      return new Response(
        JSON.stringify({
          id: 'usuario-id',
          personaId: 'persona-id',
          roles: ['admin'],
          permisos: ['academico.responsables.ver']
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      );
    });

    await service.asegurarUsuarioInicial();

    expect(service.isLoggedIn()).toBe(true);
    expect(service.tienePermiso('academico.responsables.ver')).toBe(true);
    expect(llamadasA(fetchMock, '/api/auth/me')).toHaveLength(1);
    expect(llamadasA(fetchMock, '/api/auth/session')).toHaveLength(1);
  });

  it('asegurarUsuarioInicial deja estado nulo y limpia cookie edge cuando no hay sesión', async () => {
    vi.spyOn(service.supabase.auth, 'getSession').mockResolvedValue({
      data: { session: null },
      error: null
    } as never);
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }));

    await service.asegurarUsuarioInicial();

    expect(service.isLoggedIn()).toBe(false);
    expect(service.tienePermiso('academico.alumnos.ver')).toBe(false);
    expect(llamadasA(fetchMock, '/api/auth/me')).toHaveLength(0);
    expect(llamadasA(fetchMock, '/api/auth/session')).toHaveLength(1);
    expect(llamadasA(fetchMock, '/api/auth/session')[0][1]).toMatchObject({ method: 'DELETE' });
  });

  it('un error de /auth/me en asegurarUsuarioInicial no bloquea el bootstrap ni destruye la sesión', async () => {
    vi.spyOn(service.supabase.auth, 'getSession').mockResolvedValue({
      data: { session },
      error: null
    } as never);
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      return new Response(null, { status: 500 });
    });

    await expect(service.asegurarUsuarioInicial()).resolves.toBeUndefined();

    expect(service.isLoggedIn()).toBe(false);
    expect(signOut).not.toHaveBeenCalled();
    expect(service.getToken()).toBe('access-token-prueba');
  });

  it('conserva la sesión y explica el motivo cuando la identidad no está vinculada', async () => {
    vi.spyOn(service.supabase.auth, 'getSession').mockResolvedValue({
      data: { session },
      error: null
    } as never);
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      return new Response(JSON.stringify({ codigo: 'IDENTIDAD_NO_VINCULADA', mensaje: 'x' }), {
        status: 403,
        headers: { 'Content-Type': 'application/json' }
      });
    });

    await service.asegurarUsuarioInicial();

    expect(service.isLoggedIn()).toBe(false);
    expect(signOut).not.toHaveBeenCalled();
    expect(service.getToken()).toBe('access-token-prueba');
    expect(service.mensajeSesionInvalidaPendiente()).toContain('no esta vinculada');
    expect(service.consumirMensajeSesionInvalida()).toContain('no esta vinculada');
    expect(service.mensajeSesionInvalidaPendiente()).toBeNull();
  });

  it('conserva el código diferencial del usuario inactivo', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      return new Response(JSON.stringify({ codigo: 'USUARIO_INACTIVO' }), {
        status: 403,
        headers: { 'Content-Type': 'application/json' }
      });
    });

    await expect(service.login('padre@ejemplo.com', 'password')).rejects.toMatchObject({
      code: 'USUARIO_INACTIVO'
    });

    expect(service.isLoggedIn()).toBe(false);
  });

  it('conserva el código IDENTIDAD_NO_VINCULADA en login', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      return new Response(JSON.stringify({ codigo: 'IDENTIDAD_NO_VINCULADA' }), {
        status: 403,
        headers: { 'Content-Type': 'application/json' }
      });
    });

    await expect(service.login('padre@ejemplo.com', 'password')).rejects.toMatchObject({
      code: 'IDENTIDAD_NO_VINCULADA'
    });
    expect(signOut).toHaveBeenCalledOnce();
  });

  it('distingue el perfil de persona incompleto de la identidad no vinculada', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      return new Response(JSON.stringify({ codigo: 'PERFIL_INCOMPLETO' }), {
        status: 403,
        headers: { 'Content-Type': 'application/json' }
      });
    });

    await expect(service.login('padre@ejemplo.com', 'password')).rejects.toMatchObject({
      code: 'PERFIL_INCOMPLETO'
    });
  });

  it('un 403 de autorización sin código no se presenta como identidad no vinculada', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      return new Response(null, { status: 403 });
    });

    const error = await service
      .login('padre@ejemplo.com', 'password')
      .then(() => null)
      .catch(e => e as { code: string; message: string });

    expect(error?.code).toBe('PERFIL_NO_HABILITADO');
    expect(error?.message).not.toContain('no esta vinculada');
  });

  it('un fallo al limpiar la cookie edge no deja sesión local activa', async () => {
    vi.spyOn(service.supabase.auth, 'getSession').mockResolvedValue({
      data: { session },
      error: null
    } as never);
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 500 });
      }
      return new Response(
        JSON.stringify({
          id: 'usuario-id',
          personaId: 'persona-id',
          roles: ['padre'],
          permisos: []
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      );
    });

    await expect(service.asegurarUsuarioInicial()).resolves.toBeUndefined();

    expect(service.isLoggedIn()).toBe(false);
    expect(service.getToken()).toBeNull();
  });

  it('asegurarUsuarioInicial es idempotente y evita dobles cargas', async () => {
    vi.spyOn(service.supabase.auth, 'getSession').mockResolvedValue({
      data: { session },
      error: null
    } as never);
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      return new Response(
        JSON.stringify({
          id: 'usuario-id',
          personaId: 'persona-id',
          roles: ['admin'],
          permisos: ['academico.matriculas.ver']
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      );
    });

    await Promise.all([service.asegurarUsuarioInicial(), service.asegurarUsuarioInicial()]);

    expect(llamadasA(fetchMock, '/api/auth/me')).toHaveLength(1);
    expect(llamadasA(fetchMock, '/api/auth/session')).toHaveLength(1);
    expect(service.tienePermiso('academico.matriculas.ver')).toBe(true);
  });

  it('no re-muestra un mensaje previo de identidad ni bloquea cuando /auth/me luego responde 200', async () => {
    vi.spyOn(service.supabase.auth, 'getSession').mockResolvedValue({
      data: { session },
      error: null
    } as never);

    let identidadResuelta = false;
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      if (!identidadResuelta) {
        return new Response(JSON.stringify({ codigo: 'IDENTIDAD_NO_VINCULADA', mensaje: 'x' }), {
          status: 403,
          headers: { 'Content-Type': 'application/json' }
        });
      }
      return new Response(JSON.stringify(perfil), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      });
    });

    // 1) Primera restauración: /auth/me responde 403 → queda un mensaje previo pendiente.
    await service.asegurarUsuarioInicial();
    expect(service.mensajeSesionInvalidaPendiente()).toContain('no esta vinculada');
    expect(service.consumirMensajeSesionInvalida()).toContain('no esta vinculada');
    expect(service.isLoggedIn()).toBe(false);

    // 2) El backend ya devuelve 200 con un usuario válido (evidencia de producción:
    //    dos GET /api/auth/me exitosos con UserId válido y autenticado).
    identidadResuelta = true;

    // 3) Un nuevo intento de bootstrap (p.ej. el auth-callback tras OAuth) NO debe
    //    re-servir el fallo memoizado sino re-consultar /auth/me.
    await service.asegurarUsuarioInicial();

    // El mensaje anterior no debe volver a mostrarse ni provocar redirección/bloqueo.
    expect(service.mensajeSesionInvalidaPendiente()).toBeNull();
    expect(service.consumirMensajeSesionInvalida()).toBeNull();
    expect(service.isLoggedIn()).toBe(true);
    expect(service.tienePermiso('academico.alumnos.ver')).toBe(true);
  });

  it('un login válido descarta un mensaje previo de identidad pendiente', async () => {
    vi.spyOn(service.supabase.auth, 'getSession').mockResolvedValue({
      data: { session },
      error: null
    } as never);

    // /auth/me devuelve 403 con un mensaje previo la primera vez (estado previo real).
    let identidadResuelta = false;
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (esSesionEdge(input)) {
        return new Response(null, { status: 204 });
      }
      if (!identidadResuelta) {
        return new Response(JSON.stringify({ codigo: 'IDENTIDAD_NO_VINCULADA', mensaje: 'x' }), {
          status: 403,
          headers: { 'Content-Type': 'application/json' }
        });
      }
      return new Response(JSON.stringify(perfil), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      });
    });

    // Estado previo: una restauración fallida deja el mensaje de identidad pendiente.
    await service.asegurarUsuarioInicial();
    expect(service.mensajeSesionInvalidaPendiente()).toContain('no esta vinculada');
    expect(service.isLoggedIn()).toBe(false);

    // El backend ya responde 200 para un usuario válido y el usuario completa el login.
    identidadResuelta = true;
    await service.login('admin@ejemplo.com', 'password');

    // Un login válido debe descartar el mensaje previo y no dejar estado bloqueado.
    expect(service.mensajeSesionInvalidaPendiente()).toBeNull();
    expect(service.consumirMensajeSesionInvalida()).toBeNull();
    expect(service.isLoggedIn()).toBe(true);
    expect(service.tienePermiso('academico.alumnos.ver')).toBe(true);
  });
});

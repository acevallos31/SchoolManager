import { afterEach, describe, expect, it, vi } from 'vitest';
import { SESSION_COOKIE, tokenIsValid } from '../../../../edge/auth-shared';

describe('auth-shared edge', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.restoreAllMocks();
  });

  function configurarSupabase(url = 'https://example.supabase.co') {
    vi.stubEnv('SUPABASE_URL', url);
    vi.stubEnv('SUPABASE_PUBLISHABLE_KEY', 'publishable-test');
  }

  it('mantiene un nombre de cookie Host-only para la sesión edge', () => {
    expect(SESSION_COOKIE).toBe('__Host-schoolmanager-session');
  });

  it('rechaza la validación cuando falta la URL de Supabase', async () => {
    vi.stubEnv('SUPABASE_URL', '');
    vi.stubEnv('SUPABASE_PUBLISHABLE_KEY', 'publishable-test');
    const fetchMock = vi.spyOn(globalThis, 'fetch');
    vi.spyOn(console, 'error').mockImplementation(() => undefined);

    await expect(tokenIsValid('token-prueba', 'test')).resolves.toBe(false);

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('rechaza la validación cuando falta la clave publicable', async () => {
    vi.stubEnv('SUPABASE_URL', 'https://example.supabase.co');
    vi.stubEnv('SUPABASE_PUBLISHABLE_KEY', '');
    const fetchMock = vi.spyOn(globalThis, 'fetch');
    vi.spyOn(console, 'error').mockImplementation(() => undefined);

    await expect(tokenIsValid('token-prueba', 'test')).resolves.toBe(false);

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('valida el token contra Supabase y normaliza la barra final de la URL', async () => {
    configurarSupabase('https://example.supabase.co/');
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(null, { status: 200 }));

    await expect(tokenIsValid('token-valido', 'middleware')).resolves.toBe(true);

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock).toHaveBeenCalledWith(
      'https://example.supabase.co/auth/v1/user',
      {
        method: 'GET',
        headers: {
          apikey: 'publishable-test',
          Authorization: 'Bearer token-valido'
        },
        cache: 'no-store'
      }
    );
  });

  it('acepta SUPABASE_ANON_KEY como nombre compatible de clave publicable', async () => {
    vi.stubEnv('SUPABASE_URL', 'https://example.supabase.co');
    vi.stubEnv('SUPABASE_PUBLISHABLE_KEY', '');
    vi.stubEnv('SUPABASE_ANON_KEY', 'anon-key-prueba');
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(null, { status: 200 }));

    await expect(tokenIsValid('token-valido', 'test')).resolves.toBe(true);

    expect(fetchMock).toHaveBeenCalledWith(
      'https://example.supabase.co/auth/v1/user',
      expect.objectContaining({
        headers: {
          apikey: 'anon-key-prueba',
          Authorization: 'Bearer token-valido'
        }
      })
    );
  });

  it('devuelve false cuando Supabase rechaza el token', async () => {
    configurarSupabase();
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 401 }));

    await expect(tokenIsValid('token-invalido', 'api/auth/session')).resolves.toBe(false);
  });

  it('devuelve false y registra el contexto cuando Supabase no responde', async () => {
    configurarSupabase();
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new Error('sin conexión'));
    const errorMock = vi.spyOn(console, 'error').mockImplementation(() => undefined);

    await expect(tokenIsValid('token-prueba', 'middleware')).resolves.toBe(false);

    expect(errorMock).toHaveBeenCalledWith(
      'No se pudo validar la sesion Supabase en middleware:',
      expect.any(Error)
    );
  });
});

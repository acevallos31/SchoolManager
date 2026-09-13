import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import sessionHandler from './session';

describe('api/auth/session', () => {
  beforeEach(() => {
    process.env['SUPABASE_URL'] = 'https://proyecto.supabase.co/';
    process.env['SUPABASE_PUBLISHABLE_KEY'] = 'publishable-key-prueba';
  });

  afterEach(() => {
    delete process.env['SUPABASE_URL'];
    delete process.env['SUPABASE_PUBLISHABLE_KEY'];
    vi.restoreAllMocks();
  });

  it('expone el Web Handler predeterminado que carga Vercel', () => {
    expect(sessionHandler).toEqual({
      fetch: expect.any(Function)
    });
  });

  it('emite la cookie segura cuando Supabase valida el bearer token', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ id: 'auth-user-id' }), { status: 200 })
    );

    const response = await sessionHandler.fetch(
      new Request('https://schoolmanager.test/api/auth/session', {
        method: 'POST',
        headers: { Authorization: 'Bearer access-token-prueba' }
      })
    );

    expect(response.status).toBe(204);
    expect(fetchMock).toHaveBeenCalledWith(
      'https://proyecto.supabase.co/auth/v1/user',
      expect.objectContaining({
        method: 'GET',
        headers: {
          apikey: 'publishable-key-prueba',
          Authorization: 'Bearer access-token-prueba'
        },
        cache: 'no-store'
      })
    );
    expect(response.headers.get('set-cookie')).toContain(
      '__Host-schoolmanager-session=access-token-prueba'
    );
    expect(response.headers.get('set-cookie')).toContain('HttpOnly');
    expect(response.headers.get('set-cookie')).toContain('Secure');
    expect(response.headers.get('set-cookie')).toContain('SameSite=Lax');
  });

  it('rechaza solicitudes sin bearer token y no consulta Supabase', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch');

    const response = await sessionHandler.fetch(
      new Request('https://schoolmanager.test/api/auth/session', {
        method: 'POST'
      })
    );

    expect(response.status).toBe(401);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('rechaza el token cuando Supabase no lo valida', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 401 }));

    const response = await sessionHandler.fetch(
      new Request('https://schoolmanager.test/api/auth/session', {
        method: 'POST',
        headers: { Authorization: 'Bearer token-invalido' }
      })
    );

    expect(response.status).toBe(401);
    expect(response.headers.get('set-cookie')).toBeNull();
  });

  it('elimina la cookie en DELETE', async () => {
    const response = await sessionHandler.fetch(
      new Request('https://schoolmanager.test/api/auth/session', {
        method: 'DELETE'
      })
    );

    expect(response.status).toBe(204);
    expect(response.headers.get('set-cookie')).toContain(
      '__Host-schoolmanager-session='
    );
    expect(response.headers.get('set-cookie')).toContain('Max-Age=0');
  });

  it('responde 405 para métodos no admitidos', async () => {
    const response = await sessionHandler.fetch(
      new Request('https://schoolmanager.test/api/auth/session', {
        method: 'GET'
      })
    );

    expect(response.status).toBe(405);
    expect(response.headers.get('allow')).toBe('POST, DELETE');
  });
});

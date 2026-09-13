import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import handler from './session';

interface MockResponse {
  headers: Map<string, string>;
  statusCode: number;
  ended: boolean;
  setHeader(name: string, value: string): void;
  status(code: number): MockResponse;
  end(): void;
}

function responseMock(): MockResponse {
  return {
    headers: new Map<string, string>(),
    statusCode: 200,
    ended: false,
    setHeader(name, value) {
      this.headers.set(name.toLowerCase(), value);
    },
    status(code) {
      this.statusCode = code;
      return this;
    },
    end() {
      this.ended = true;
    }
  };
}

async function invoke(method: string, authorization?: string): Promise<MockResponse> {
  const response = responseMock();
  await handler(
    {
      method,
      headers: authorization ? { authorization } : {}
    },
    response
  );
  return response;
}

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

  it('expone el handler Node predeterminado que carga Vercel', () => {
    expect(handler).toEqual(expect.any(Function));
  });

  it('emite la cookie segura cuando Supabase valida el bearer token', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ id: 'auth-user-id' }), { status: 200 })
    );

    const response = await invoke('POST', 'Bearer access-token-prueba');

    expect(response.statusCode).toBe(204);
    expect(response.ended).toBe(true);
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

    const response = await invoke('POST');

    expect(response.statusCode).toBe(401);
    expect(response.ended).toBe(true);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('rechaza el token cuando Supabase no lo valida', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 401 }));

    const response = await invoke('POST', 'Bearer token-invalido');

    expect(response.statusCode).toBe(401);
    expect(response.headers.get('set-cookie')).toBeUndefined();
  });

  it('acepta authorization como arreglo del runtime Node', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 401 }));
    const response = responseMock();

    await handler(
      {
        method: 'POST',
        headers: { authorization: ['Bearer token-invalido'] }
      },
      response
    );

    expect(response.statusCode).toBe(401);
  });

  it('elimina la cookie en DELETE', async () => {
    const response = await invoke('DELETE');

    expect(response.statusCode).toBe(204);
    expect(response.headers.get('set-cookie')).toContain(
      '__Host-schoolmanager-session='
    );
    expect(response.headers.get('set-cookie')).toContain('Max-Age=0');
  });

  it('responde 405 para métodos no admitidos', async () => {
    const response = await invoke('GET');

    expect(response.statusCode).toBe(405);
    expect(response.headers.get('allow')).toBe('POST, DELETE');
  });
});

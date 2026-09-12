declare const process: {
  env: Record<string, string | undefined>;
};

const SESSION_COOKIE = '__Host-schoolmanager-session';
const DEFAULT_MAX_AGE_SECONDS = 60 * 60;

function authConfig(): { url: string; key: string } | null {
  const rawUrl = process.env['SUPABASE_URL'];
  const key = process.env['SUPABASE_PUBLISHABLE_KEY'];

  if (!rawUrl || !key) {
    return null;
  }

  const url = rawUrl.endsWith('/') ? rawUrl.slice(0, -1) : rawUrl;
  return { url, key };
}

async function tokenIsValid(accessToken: string): Promise<boolean> {
  const config = authConfig();
  if (!config) {
    console.error('Faltan SUPABASE_URL/SUPABASE_PUBLISHABLE_KEY en Vercel.');
    return false;
  }

  try {
    const response = await fetch(`${config.url}/auth/v1/user`, {
      method: 'GET',
      headers: {
        apikey: config.key,
        Authorization: `Bearer ${accessToken}`
      },
      cache: 'no-store'
    });

    return response.ok;
  } catch (error) {
    console.error('No se pudo validar la sesion Supabase en api/auth/session:', error);
    return false;
  }
}

function cookie(value: string, maxAge: number): string {
  return [
    `${SESSION_COOKIE}=${encodeURIComponent(value)}`,
    'Path=/',
    `Max-Age=${maxAge}`,
    'HttpOnly',
    'Secure',
    'SameSite=Lax'
  ].join('; ');
}

function bearerToken(authorization: string): string | null {
  const prefix = 'Bearer ';
  if (authorization.length <= prefix.length || authorization.slice(0, prefix.length).toLowerCase() !== prefix.toLowerCase()) {
    return null;
  }

  const token = authorization.slice(prefix.length).trim();
  return token || null;
}

export async function POST(request: Request): Promise<Response> {
  const authorization = request.headers.get('authorization') ?? '';
  const accessToken = bearerToken(authorization);

  if (!accessToken || !(await tokenIsValid(accessToken))) {
    return new Response(null, {
      status: 401,
      headers: {
        'Cache-Control': 'no-store'
      }
    });
  }

  return new Response(null, {
    status: 204,
    headers: {
      'Set-Cookie': cookie(accessToken, DEFAULT_MAX_AGE_SECONDS),
      'Cache-Control': 'private, no-store',
      Vary: 'Cookie'
    }
  });
}

export function DELETE(): Response {
  return new Response(null, {
    status: 204,
    headers: {
      'Set-Cookie': cookie('', 0),
      'Cache-Control': 'private, no-store',
      Vary: 'Cookie'
    }
  });
}

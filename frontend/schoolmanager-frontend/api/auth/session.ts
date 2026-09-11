const SESSION_COOKIE = '__Host-schoolmanager-session';
const DEFAULT_MAX_AGE_SECONDS = 60 * 60;

function authConfig(): { url: string; key: string } | null {
  const url = process.env['SUPABASE_URL']?.replace(/\/$/, '');
  const key = process.env['SUPABASE_PUBLISHABLE_KEY'];

  if (!url || !key) {
    return null;
  }

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
    console.error('No se pudo validar la sesion Supabase para edge:', error);
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

export async function POST(request: Request): Promise<Response> {
  const authorization = request.headers.get('authorization') ?? '';
  const match = /^Bearer\s+(.+)$/i.exec(authorization);
  const accessToken = match?.[1]?.trim();

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

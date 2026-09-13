declare const process: {
  env: Record<string, string | undefined>;
};

type HeaderValue = string | string[] | undefined;

interface VercelRequestLike {
  method?: string;
  headers: Record<string, HeaderValue>;
}

interface VercelResponseLike {
  setHeader(name: string, value: string): void;
  status(code: number): VercelResponseLike;
  end(body?: string): void;
}

const SESSION_COOKIE = '__Host-schoolmanager-session';
const DEFAULT_MAX_AGE_SECONDS = 60 * 60;

function authConfig(): { url: string; key: string } | null {
  const rawUrl = process.env['SUPABASE_URL'];
  // Keep compatibility with the name used by the staging setup guide. Both
  // values are publishable/anon keys; never use service_role here.
  const key =
    process.env['SUPABASE_PUBLISHABLE_KEY'] || process.env['SUPABASE_ANON_KEY'];

  if (!rawUrl || !key) {
    return null;
  }

  const url = rawUrl.endsWith('/') ? rawUrl.slice(0, -1) : rawUrl;
  return { url, key };
}

async function tokenIsValid(accessToken: string): Promise<boolean> {
  const config = authConfig();
  if (!config) {
    console.error(
      'Faltan SUPABASE_URL y SUPABASE_PUBLISHABLE_KEY (o SUPABASE_ANON_KEY) en Vercel.'
    );
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

    if (!response.ok) {
      console.error(
        `Supabase rechazo la validacion del token: HTTP ${response.status} en ${config.url}/auth/v1/user.`
      );
    }

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

function firstHeader(value: HeaderValue): string {
  return Array.isArray(value) ? (value[0] ?? '') : (value ?? '');
}

function bearerToken(authorization: string): string | null {
  const prefix = 'Bearer ';
  if (
    authorization.length <= prefix.length ||
    authorization.slice(0, prefix.length).toLowerCase() !== prefix.toLowerCase()
  ) {
    return null;
  }

  const token = authorization.slice(prefix.length).trim();
  return token || null;
}

function finish(
  response: VercelResponseLike,
  status: number,
  headers: Record<string, string>
): void {
  for (const [name, value] of Object.entries(headers)) {
    response.setHeader(name, value);
  }
  response.status(status).end();
}

export default async function handler(
  request: VercelRequestLike,
  response: VercelResponseLike
): Promise<void> {
  switch ((request.method ?? 'GET').toUpperCase()) {
    case 'DELETE':
      finish(response, 204, {
        'Set-Cookie': cookie('', 0),
        'Cache-Control': 'private, no-store',
        Vary: 'Cookie'
      });
      return;

    case 'POST': {
      const authorization = firstHeader(request.headers.authorization);
      const accessToken = bearerToken(authorization);

      if (!accessToken) {
        console.error('POST /api/auth/session recibio un Authorization Bearer ausente o invalido.');
        finish(response, 401, { 'Cache-Control': 'no-store' });
        return;
      }

      if (!(await tokenIsValid(accessToken))) {
        finish(response, 401, { 'Cache-Control': 'no-store' });
        return;
      }

      finish(response, 204, {
        'Set-Cookie': cookie(accessToken, DEFAULT_MAX_AGE_SECONDS),
        'Cache-Control': 'private, no-store',
        Vary: 'Cookie'
      });
      return;
    }

    default:
      finish(response, 405, {
        Allow: 'POST, DELETE',
        'Cache-Control': 'no-store'
      });
  }
}

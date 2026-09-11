const SESSION_COOKIE = '__Host-schoolmanager-session';

function getCookie(request: Request, name: string): string | null {
  const header = request.headers.get('cookie') ?? '';
  const cookies = header.split(';');

  for (const item of cookies) {
    const [rawName, ...rawValue] = item.trim().split('=');
    if (rawName === name) {
      return decodeURIComponent(rawValue.join('='));
    }
  }

  return null;
}

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
    console.error('No se pudo validar la sesion Supabase en middleware:', error);
    return false;
  }
}

function redirectToLogin(request: Request): Response {
  const url = new URL('/login', request.url);
  return Response.redirect(url, 302);
}

async function serveAppShell(request: Request): Promise<Response> {
  const indexUrl = new URL('/index.html', request.url);
  const upstream = await fetch(indexUrl, {
    method: 'GET',
    cache: 'no-store'
  });

  const headers = new Headers(upstream.headers);
  headers.set('Cache-Control', 'private, no-store');
  headers.set('Vary', 'Cookie');

  return new Response(upstream.body, {
    status: upstream.status,
    statusText: upstream.statusText,
    headers
  });
}

export const config = {
  matcher: [
    '/dashboard',
    '/alumnos',
    '/matriculas',
    '/configuracion/:path*',
    '/responsables',
    '/cargos',
    '/pagos',
    '/portal-padre',
    '/home'
  ]
};

export default async function middleware(request: Request): Promise<Response> {
  const accessToken = getCookie(request, SESSION_COOKIE);

  if (!accessToken || !(await tokenIsValid(accessToken))) {
    return redirectToLogin(request);
  }

  return serveAppShell(request);
}

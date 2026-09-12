import { SESSION_COOKIE, tokenIsValid } from './edge/auth-shared';

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

function redirectToLogin(request: Request): Response {
  const url = new URL('/login', request.url);
  return Response.redirect(url, 302);
}

async function serveAppShell(request: Request): Promise<Response> {
  const indexUrl = new URL('/', request.url);
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

  if (!accessToken || !(await tokenIsValid(accessToken, 'middleware'))) {
    return redirectToLogin(request);
  }

  return serveAppShell(request);
}

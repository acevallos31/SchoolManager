import { SESSION_COOKIE, tokenIsValid } from '../../edge/auth-shared';

const DEFAULT_MAX_AGE_SECONDS = 60 * 60;

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

  if (!accessToken || !(await tokenIsValid(accessToken, 'api/auth/session'))) {
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

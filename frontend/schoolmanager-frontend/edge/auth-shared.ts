export const SESSION_COOKIE = '__Host-schoolmanager-session';

function authConfig(): { url: string; key: string } | null {
  const rawUrl = process.env['SUPABASE_URL'];
  const key = process.env['SUPABASE_PUBLISHABLE_KEY'];

  if (!rawUrl || !key) {
    return null;
  }

  const url = rawUrl.endsWith('/') ? rawUrl.slice(0, -1) : rawUrl;
  return { url, key };
}

export async function tokenIsValid(accessToken: string, context: string): Promise<boolean> {
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
    console.error(`No se pudo validar la sesion Supabase en ${context}:`, error);
    return false;
  }
}

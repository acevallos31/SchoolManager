declare const process: {
  env: Record<string, string | undefined>;
};

export const SESSION_COOKIE = '__Host-schoolmanager-session';

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

export async function tokenIsValid(accessToken: string, context: string): Promise<boolean> {
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

    return response.ok;
  } catch (error) {
    console.error(`No se pudo validar la sesion Supabase en ${context}:`, error);
    return false;
  }
}

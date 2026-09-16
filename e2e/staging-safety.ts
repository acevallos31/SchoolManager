const FORBIDDEN_PRODUCTION_HOSTS = new Set([
  'schoolmanager.vercel.app',
  'schoolmanager.nocpbx.com',
  'schoolmanager-xdxx.onrender.com',
  'pzhcpdznjoyukbhhodjz.supabase.co',
  'db.pzhcpdznjoyukbhhodjz.supabase.co'
]);

const DEFAULT_ALLOWED_STAGING_HOSTS = new Set(['localhost', '127.0.0.1', '::1']);

function allowedHosts(): Set<string> {
  const configured = (process.env.E2E_ALLOWED_HOSTS ?? '')
    .split(',')
    .map(value => value.trim().toLowerCase())
    .filter(Boolean);

  return new Set([...DEFAULT_ALLOWED_STAGING_HOSTS, ...configured]);
}

export function assertAllowedStagingUrl(rawUrl: string, label: string): string {
  let parsed: URL;

  try {
    parsed = new URL(rawUrl);
  } catch {
    throw new Error(`[E2E GUARDRAIL] ${label} no contiene una URL válida.`);
  }

  const host = parsed.hostname.toLowerCase();

  if (FORBIDDEN_PRODUCTION_HOSTS.has(host)) {
    throw new Error(
      `[E2E GUARDRAIL] ${label} apunta al host de producción prohibido '${host}'.`
    );
  }

  if (!allowedHosts().has(host)) {
    throw new Error(
      `[E2E GUARDRAIL] ${label} apunta a '${host}', fuera de la allowlist de staging. ` +
        'Define E2E_ALLOWED_HOSTS solo para hosts de staging controlados.'
    );
  }

  if (!['http:', 'https:'].includes(parsed.protocol)) {
    throw new Error(`[E2E GUARDRAIL] ${label} debe usar http o https.`);
  }

  if (parsed.username || parsed.password) {
    throw new Error(`[E2E GUARDRAIL] ${label} no debe incluir credenciales en la URL.`);
  }

  return rawUrl;
}

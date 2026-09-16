import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDir, '..');
const environmentsDir = resolve(projectRoot, 'src/app/environments');

const required = {
  E2E_SUPABASE_URL: process.env.E2E_SUPABASE_URL,
  E2E_SUPABASE_PUBLISHABLE_KEY: process.env.E2E_SUPABASE_PUBLISHABLE_KEY,
  E2E_API_URL: process.env.E2E_API_URL
};

const missing = Object.entries(required)
  .filter(([, value]) => !value?.trim())
  .map(([name]) => name);

if (missing.length > 0) {
  throw new Error(
    `Faltan variables para generar el entorno staging: ${missing.join(', ')}`
  );
}

const forbiddenProductionHosts = new Set([
  'schoolmanager.vercel.app',
  'schoolmanager.nocpbx.com',
  'schoolmanager-xdxx.onrender.com',
  'pzhcpdznjoyukbhhodjz.supabase.co',
  'db.pzhcpdznjoyukbhhodjz.supabase.co'
]);

const defaultAllowedHosts = new Set(['localhost', '127.0.0.1', '::1']);
const configuredAllowedHosts = (process.env.E2E_ALLOWED_HOSTS ?? '')
  .split(',')
  .map(value => value.trim().toLowerCase())
  .filter(Boolean);
const allowedHosts = new Set([...defaultAllowedHosts, ...configuredAllowedHosts]);

function validateUrl(name, rawValue) {
  const value = rawValue.trim();
  let parsed;

  try {
    parsed = new URL(value);
  } catch {
    throw new Error(`${name} no contiene una URL válida.`);
  }

  const host = parsed.hostname.toLowerCase();
  if (forbiddenProductionHosts.has(host)) {
    throw new Error(`${name} apunta a un host de producción prohibido: ${host}`);
  }

  if (!allowedHosts.has(host)) {
    throw new Error(
      `${name} apunta a '${host}', que no está en la allowlist E2E. ` +
        'Agrégalo explícitamente a E2E_ALLOWED_HOSTS solo si es un host de staging.'
    );
  }

  if (!['http:', 'https:'].includes(parsed.protocol)) {
    throw new Error(`${name} debe usar http o https.`);
  }

  if (parsed.username || parsed.password) {
    throw new Error(`${name} no debe incluir credenciales en la URL.`);
  }

  return value.replace(/\/$/, '');
}

const supabaseUrl = validateUrl('E2E_SUPABASE_URL', required.E2E_SUPABASE_URL);
const apiUrl = validateUrl('E2E_API_URL', required.E2E_API_URL);
const publishableKey = required.E2E_SUPABASE_PUBLISHABLE_KEY.trim();

mkdirSync(environmentsDir, { recursive: true });

const environmentPath = resolve(environmentsDir, 'environment.staging.ts');
const runtimeManifestPath = resolve(environmentsDir, 'e2e-runtime.staging.json');

const environmentSource = `// Generado por scripts/generate-staging-environment.mjs. NO versionar.\n` +
  `export const environment = {\n` +
  `  production: false,\n` +
  `  supabaseUrl: ${JSON.stringify(supabaseUrl)},\n` +
  `  supabaseAnonKey: ${JSON.stringify(publishableKey)},\n` +
  `  apiUrl: ${JSON.stringify(apiUrl)}\n` +
  `};\n`;

const runtimeManifest = {
  environment: 'staging',
  production: false,
  supabaseUrl,
  apiUrl
};

writeFileSync(environmentPath, environmentSource, 'utf8');
writeFileSync(runtimeManifestPath, `${JSON.stringify(runtimeManifest, null, 2)}\n`, 'utf8');

console.log('Entorno Angular staging generado con guardrails E2E.');

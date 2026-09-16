import type { FullConfig } from '@playwright/test';
import { assertAllowedStagingUrl } from './staging-safety';

interface StagingRuntimeManifest {
  environment: string;
  production: boolean;
  supabaseUrl: string;
  apiUrl: string;
}

export default async function globalSetup(config: FullConfig): Promise<void> {
  if (process.env.E2E_STAGING !== '1') {
    return;
  }

  const baseURL = config.projects[0]?.use?.baseURL;
  if (typeof baseURL !== 'string') {
    throw new Error('[E2E GUARDRAIL] No se pudo resolver baseURL para staging.');
  }

  const manifestUrl = new URL('/e2e-runtime.staging.json', baseURL).toString();
  const response = await fetch(manifestUrl, { signal: AbortSignal.timeout(5000) });

  if (!response.ok) {
    throw new Error(
      `[E2E GUARDRAIL] El frontend staging no expone ${manifestUrl} ` +
        `(HTTP ${response.status}). Ejecuta npm run prepare:staging antes del build.`
    );
  }

  const manifest = (await response.json()) as Partial<StagingRuntimeManifest>;

  if (manifest.environment !== 'staging' || manifest.production !== false) {
    throw new Error(
      '[E2E GUARDRAIL] El manifest del frontend no declara un entorno staging seguro.'
    );
  }

  if (!manifest.supabaseUrl || !manifest.apiUrl) {
    throw new Error(
      '[E2E GUARDRAIL] El manifest staging no contiene supabaseUrl y apiUrl.'
    );
  }

  assertAllowedStagingUrl(manifest.supabaseUrl, 'Supabase del frontend staging');
  assertAllowedStagingUrl(manifest.apiUrl, 'API del frontend staging');
}

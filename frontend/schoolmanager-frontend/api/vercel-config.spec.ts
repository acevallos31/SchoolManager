import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

describe('vercel.json', () => {
  it('sirve la SPA Angular en /auth/callback antes del catch-all 404', () => {
    const config = JSON.parse(
      readFileSync(resolve(process.cwd(), 'vercel.json'), 'utf8')
    ) as {
      routes: Array<{ src?: string; dest?: string; status?: number }>;
    };

    const callbackIndex = config.routes.findIndex(
      route => route.src === '^/auth/callback/?$'
    );
    const catchAllIndex = config.routes.findIndex(
      route => route.src === '/.*' && route.status === 404
    );

    expect(callbackIndex).toBeGreaterThanOrEqual(0);
    expect(config.routes[callbackIndex]).toMatchObject({
      dest: '/index.html'
    });
    expect(catchAllIndex).toBeGreaterThan(callbackIndex);
  });
});

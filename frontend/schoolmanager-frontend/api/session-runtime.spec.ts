import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import ts from 'typescript';
import { describe, expect, it } from 'vitest';

// Vitest transpila los imports y no reproduce la selección ESM/CommonJS de Node.
function runCompiledHandler(packageType?: string) {
  const directory = mkdtempSync(join(tmpdir(), 'schoolmanager-session-runtime-'));
  try {
    const root = process.cwd();
    const manifest = JSON.parse(readFileSync(resolve(root, 'package.json'), 'utf8'));
    if (packageType) manifest.type = packageType;
    writeFileSync(join(directory, 'package.json'), JSON.stringify(manifest));
    const config = ts.readConfigFile(resolve(root, 'tsconfig.json'), ts.sys.readFile);
    const { options } = ts.convertCompilerOptionsFromJson(config.config.compilerOptions, root);
    const source = readFileSync(resolve(root, 'api/auth/session.ts'), 'utf8');
    const compiled = ts.transpileModule(source, { compilerOptions: options }).outputText;
    writeFileSync(join(directory, 'session.js'), compiled);
    writeFileSync(join(directory, 'check.mjs'), `
      import assert from 'node:assert/strict';
      import handler from './session.js';
      delete process.env.SUPABASE_URL;
      delete process.env.SUPABASE_PUBLISHABLE_KEY;
      globalThis.fetch = () => { throw new Error('Unexpected network call'); };
      console.error = () => {};
      for (const [method, expected] of [['DELETE', 204], ['POST', 401], ['GET', 405]]) {
        const headers = {};
        const response = {
          statusCode: 0, ended: false,
          setHeader(name, value) { headers[name] = value; },
          status(code) { this.statusCode = code; return this; },
          end() { this.ended = true; }
        };
        await handler({ method, headers: {} }, response);
        assert.equal(response.statusCode, expected);
        assert.equal(response.ended, true);
        if (method === 'DELETE') {
          assert.match(headers['Set-Cookie'], /Max-Age=0/);
          assert.match(headers['Set-Cookie'], /HttpOnly; Secure; SameSite=Lax/);
        }
      }
      console.log('runtime-session-ok');
    `);
    return spawnSync(process.execPath, ['--no-experimental-detect-module', '--no-experimental-require-module', 'check.mjs'], {
      cwd: directory,
      encoding: 'utf8',
      timeout: 10000,
      env: { ...process.env, NODE_OPTIONS: '' }
    });
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
}

describe('carga Node del endpoint compilado', () => {
  it('reproduce el error de export ESM cargado como CommonJS', () => {
    const result = runCompiledHandler('commonjs');
    expect(result.status).not.toBe(0);
    expect(result.stderr).toContain("Unexpected token 'export'");
  });

  it('carga session.js con el package real y responde DELETE/POST/GET', () => {
    const result = runCompiledHandler();
    expect(result.stderr).toBe('');
    expect(result.status).toBe(0);
    expect(result.stdout).toContain('runtime-session-ok');
  });
});

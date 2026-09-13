import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';
import { JSDOM } from 'jsdom';

const buildRoot = new URL('../dist/schoolmanager-frontend/browser/', import.meta.url);

// Inspeccionar el artefacto publicado: los tests de componentes no ejecutan
// la optimización de CSS que puede introducir handlers bloqueados por CSP.
for (const entry of ['index.csr.html', 'login/index.html']) {
  test(`${entry}: el diseño global se carga sin JavaScript inline`, async () => {
    const html = await readFile(new URL(entry, buildRoot), 'utf8');
    const dom = new JSDOM(html);
    try {
      const links = [...dom.window.document.querySelectorAll('link[rel="stylesheet"]')];
      const globalStyles = links.find(link => /^styles-[\w-]+\.css$/.test(link.getAttribute('href') ?? ''));

      assert.ok(globalStyles, 'Falta el CSS global en el HTML publicado');
      assert.ok(
        ['', 'all', 'screen'].includes(globalStyles.media),
        `El CSS global no se aplica a la pantalla: media=${globalStyles.media}`,
      );
      assert.equal(globalStyles.hasAttribute('onload'), false, 'script-src self bloquea el handler inline');

      const css = await readFile(new URL(globalStyles.getAttribute('href'), buildRoot), 'utf8');
      for (const selector of ['.sm-btn', '.sm-input', '.sm-table']) {
        assert.ok(css.includes(selector), `Falta ${selector} en el archivo CSS publicado`);
      }
    } finally {
      dom.window.close();
    }
  });
}

test('login prerenderizado: ambos botones tienen texto antes de arrancar Angular', async () => {
  const html = await readFile(new URL('login/index.html', buildRoot), 'utf8');
  const dom = new JSDOM(html);
  try {
    const document = dom.window.document;
    assert.equal(document.querySelector('button[type="submit"]')?.textContent?.trim(), 'Entrar al sistema');
    assert.equal(document.querySelector('.google-button')?.textContent?.trim(), 'Continuar con Google');
  } finally {
    dom.window.close();
  }
});

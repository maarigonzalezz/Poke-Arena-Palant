import { test, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import ts from 'typescript';

// Ejecuta el cliente real con fetch controlado, sin credenciales ni servidor externo.
const apiModule = 'data:text/javascript,' + encodeURIComponent('export const API_BASE_URL = "http://localhost:5003";');
const source = readFileSync(new URL('../src/services/playerApi.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, {
  compilerOptions: { target: ts.ScriptTarget.ES2023, module: ts.ModuleKind.ESNext },
}).outputText.replace('"./api"', JSON.stringify(apiModule));
const api = await import(`data:text/javascript;base64,${Buffer.from(compiled).toString('base64')}`);
const originalFetch = globalThis.fetch;
afterEach(() => { globalThis.fetch = originalFetch; api.signOutPlayer(); });

test('envía token, filtros combinados y cursor; resuelve la imagen grande contra la API', async () => {
  api.setPlayerAccessToken('platform-token');
  const controller = new AbortController();
  globalThis.fetch = async (url, options) => {
    const parsed = new URL(url);
    assert.equal(parsed.pathname, '/api/player/catalog');
    assert.equal(parsed.searchParams.get('type'), 'Fuego');
    assert.equal(parsed.searchParams.get('cost'), '0');
    assert.equal(parsed.searchParams.get('ownedOnly'), 'true');
    assert.equal(parsed.searchParams.get('after'), '25');
    assert.equal(parsed.searchParams.get('pageSize'), '12');
    assert.equal(options.headers.get('Authorization'), 'Bearer platform-token');
    assert.equal(options.signal, controller.signal);
    return Response.json({ items: [{ code: 'F1', quantity: 3, imageUrl: '/files/original.png' }], totalCount: 1, nextPosition: null });
  };
  const page = await api.getPlayerCatalog({ type: 'Fuego', cost: 0, ownedOnly: true, after: 25, pageSize: 12 }, controller.signal);
  assert.equal(page.items[0].imageUrl, 'http://localhost:5003/files/original.png');
  assert.equal(page.items[0].quantity, 3);
  assert.equal(page.nextPosition, null);
});

test('colección usa su endpoint y conserva respuestas vacías', async () => {
  globalThis.fetch = async url => {
    assert.equal(new URL(url).pathname, '/api/player/collection');
    assert.equal(new URL(url).searchParams.get('after'), '42');
    return Response.json({ items: [], totalCount: 0, nextPosition: null });
  };
  assert.deepEqual(await api.getPlayerCollection(12, 42), { items: [], totalCount: 0, nextPosition: null });
});

test('cerrar sesión elimina el encabezado de autenticación', async () => {
  api.setPlayerAccessToken('old-token');
  api.signOutPlayer();
  globalThis.fetch = async (_url, options) => {
    assert.equal(options.headers.has('Authorization'), false);
    return Response.json({ items: [], totalCount: 0, nextPosition: null });
  };
  await api.getPlayerCatalog();
});

test('los errores de validación del servidor se conservan para mostrarlos', async () => {
  globalThis.fetch = async () => Response.json({ errors: { cost: 'Debe estar entre 0 y 10.' } }, { status: 400 });
  await assert.rejects(api.getPlayerCatalog({ cost: 11 }), error => error.status === 400 && error.message.includes('0 y 10'));
});

test('una solicitud cancelada conserva AbortError', async () => {
  const controller = new AbortController();
  controller.abort();
  globalThis.fetch = async () => { throw new DOMException('Cancelled', 'AbortError'); };
  await assert.rejects(api.getPlayerCatalog({}, controller.signal), { name: 'AbortError' });
});

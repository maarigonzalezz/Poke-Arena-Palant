// Pruebas del catálogo simulado: filtros, sesiones, persistencia y reintentos.
import { test, beforeEach, after } from 'node:test';
import assert from 'node:assert/strict';
import ts from 'typescript';
import { createRequire } from 'node:module';
import { readFileSync, writeFileSync, mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname, resolve, basename } from 'node:path';

// Ejecuta los mocks TypeScript con Node, sin Playwright, API ni bases de datos.
const temp = mkdtempSync(join(tmpdir(), 'pokearena-mocks-'));
writeFileSync(join(temp, 'package.json'), '{"type":"commonjs"}');
for (const file of ['types/catalogs', 'services/imageValidation', 'mocks/catalogData', 'mocks/mockImage', 'mocks/mockCatalogApi']) {
  const target = join(temp, `${file}.js`);
  mkdirSync(dirname(target), { recursive: true });
  const source = readFileSync(new URL(`../src/${file}.ts`, import.meta.url), 'utf8');
  writeFileSync(target, ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2023, module: ts.ModuleKind.CommonJS },
  }).outputText);
}
const require = createRequire(join(temp, 'package.json'));
// El renderizado real de la miniatura se comprueba en navegador; aquí se prueba el catálogo.
require(join(temp, 'mocks/mockImage.js')).createMockThumbnail = async () => 'data:image/webp;base64,c2ltdWxhZG8=';
const api = require(join(temp, 'mocks/mockCatalogApi.js'));
const { buildMockCards } = require(join(temp, 'mocks/catalogData.js'));
const data = new Map();
globalThis.localStorage = {
  getItem: key => data.get(key) ?? null,
  setItem: (key, value) => data.set(key, String(value)),
  removeItem: key => data.delete(key),
};
beforeEach(async () => { await api.resetMockCatalog(); data.clear(); });
after(() => {
  assert.equal(dirname(resolve(temp)), resolve(tmpdir()));
  assert.ok(basename(temp).startsWith('pokearena-mocks-'));
  rmSync(temp, { recursive: true, force: true });
});

const cardInput = () => ({
  name: 'Carta de prueba', cost: 2, power: 8, type: 'Fuego', rarity: 'Rara', abilityCode: 'HAB-001',
  image: new File(['image-a'], 'card.png', { type: 'image/png' }),
});
const locationInput = () => ({
  name: 'Ubicación de prueba', description: 'Solo simulación', maxCards: 4,
  energyMode: 'Fixed', extraEnergy: 3, effectType: 'None', effectParameters: {},
  image: new File(['image-a'], 'location.png', { type: 'image/png' }),
});

test('catálogo reproducible, HAB-000 y paginación sin duplicados', async () => {
  const abilities = await api.mockGetAbilities();
  assert.ok(abilities.some(a => a.code === 'HAB-000'));
  const first = await api.mockSearchCards({});
  assert.equal(first.totalCount, 21);
  assert.equal(first.items.length, 20);
  const next = await api.mockSearchCards({ searchId: first.searchId, after: first.nextPosition });
  assert.equal(next.items.length, 1);
  assert.equal(next.nextPosition, null);
  assert.equal(new Set([...first.items, ...next.items].map(c => c.code)).size, 21);
});

const filters = [
  [{ cost: 2 }, c => c.cost === 2], [{ minCost: 5 }, c => c.cost >= 5], [{ maxCost: 2 }, c => c.cost <= 2],
  [{ power: -20 }, c => c.power === -20], [{ minPower: 10 }, c => c.power >= 10], [{ maxPower: 0 }, c => c.power <= 0],
  [{ type: 'Fuego' }, c => c.type === 'Fuego'], [{ rarity: 'Rara' }, c => c.rarity === 'Rara'],
  [{ abilityCode: 'HAB-001' }, c => c.abilityCode === 'HAB-001'],
  [{ abilityTrigger: 'AlRevelarse' }, c => c.abilityTrigger === 'AlRevelarse'],
  [{ abilityEffect: 'AumentarPoder' }, c => c.abilityEffect === 'AumentarPoder'],
  [{ type: 'Fuego', minCost: 1, maxCost: 3 }, c => c.type === 'Fuego' && c.cost >= 1 && c.cost <= 3],
  [{ type: 'Agua', rarity: 'Rara', minPower: 10 }, c => c.type === 'Agua' && c.rarity === 'Rara' && c.power >= 10],
  [{ name: 'char', type: 'Fuego', abilityCode: 'HAB-001' }, c => c.name.toLowerCase().includes('char') && c.type === 'Fuego' && c.abilityCode === 'HAB-001'],
  [{ type: 'Fuego', cost: 2, power: 4, rarity: 'Común' }, c => c.type === 'Fuego' && c.cost === 2 && c.power === 4 && c.rarity === 'Común'],
];
for (const [filter, match] of filters) {
  test(`filtros AND: ${JSON.stringify(filter)}`, () => {
    const cards = buildMockCards();
    assert.deepEqual(api.filterMockCards(cards, filter), cards.filter(match));
    assert.ok(cards.filter(match).length > 0);
  });
}
test('nombre sin tildes/mayúsculas, código exacto, espacios y cero resultados', async () => {
  assert.equal((await api.mockSearchCards({ name: ' VOLCAN ' })).items[0].name, 'Volcán Ágil');
  assert.equal((await api.mockSearchCards({ code: 'PKA-FUE-001' })).items[0].name, 'Charmander');
  assert.ok((await api.mockSearchCards({ abilityCode: ' HAB-001 ' })).items.length > 0);
  assert.equal((await api.mockSearchCards({ name: 'NO EXISTE' })).totalCount, 0);
});

for (const filter of [
  { cost: -1 }, { cost: 11 }, { power: -21 }, { power: 21 }, { cost: 1.5 },
  { minCost: 3, maxCost: 1 }, { minPower: 3, maxPower: 1 },
  { cost: 0, minCost: 0 }, { power: 0, maxPower: 2 },
  { pageSize: 0 }, { pageSize: 101 }, { after: -1 }, { after: 1 },
  { type: 'Inválido' }, { rarity: 'Inválida' }, { searchId: 'no-existe', name: 'char' },
]) {
  test(`rechaza criterios inválidos: ${JSON.stringify(filter)}`, async () => {
    await assert.rejects(api.mockSearchCards(filter));
  });
}

test('la búsqueda mantiene su instantánea al crear una carta local', async () => {
  const first = await api.mockSearchCards({ pageSize: 5 });
  const created = await api.mockCreateCard(cardInput(), 'new-card');
  const next = await api.mockSearchCards({ searchId: first.searchId, after: 5, pageSize: 100 });
  assert.equal(next.totalCount, 21);
  assert.ok(!next.items.some(c => c.code === created.code));
  assert.equal((await api.mockSearchCards({ pageSize: 100 })).totalCount, 22);
  assert.ok((await api.mockSearchCards({ code: created.code })).items[0].imageUrl.startsWith('data:image/'));
});

test('sesiones vencidas y cambio de filtros al paginar son rechazados', async () => {
  const first = await api.mockSearchCards({});
  await assert.rejects(api.mockSearchCards({ searchId: first.searchId, type: 'Fuego' }));
  const now = Date.now;
  try {
    Date.now = () => now() + 30 * 60 * 1000;
    await assert.rejects(api.mockSearchCards({ searchId: first.searchId, after: 20 }), /expirado/);
  } finally { Date.now = now; }
});

test('idempotencia concurrente, contenido de imagen y operación independiente', async () => {
  const input = cardInput();
  const [a,b] = await Promise.all([api.mockCreateCard(input, 'same'), api.mockCreateCard(input, 'same')]);
  assert.deepEqual(a,b);
  assert.equal((await api.mockSearchCards({})).totalCount, 22);
  await assert.rejects(api.mockCreateCard({ ...input, image: new File(['image-b'], 'card.png', { type: 'image/png' }) }, 'same'), /otros datos/);
  const location = await api.mockCreateLocation(locationInput(), 'same');
  assert.equal(location.status, 'CreatedMock');
  assert.equal(JSON.parse(data.get('pokearena_mock_locations_v1')).length, 1);
});

test('validaciones de creación: nombres, tipo de habilidad, energía y efectos', async () => {
  await assert.rejects(api.mockCreateCard({ ...cardInput(), name: ' ' }, 'a'), /nombre/);
  await assert.rejects(api.mockCreateCard({ ...cardInput(), type: 'Agua' }, 'a'), /tipo/);
  await assert.rejects(api.mockCreateLocation({ ...locationInput(), description: '' }, 'b'), /descripción/);
  await assert.rejects(api.mockCreateLocation({ ...locationInput(), maxCards: 6 }, 'b'), /Límite/);
  await assert.rejects(api.mockCreateLocation({ ...locationInput(), energyMode: 'PerCard', extraEnergy: 3 }, 'b'), /Energía/);
  await assert.rejects(api.mockCreateLocation({ ...locationInput(), effectType: 'TypeBonus', effectParameters: { power: 6, type: 'Fuego' } }, 'b'), /modificador/);
  await assert.rejects(api.mockCreateLocation({ ...locationInput(), effectType: 'TurnRestriction', effectParameters: { turns: [7] } }, 'b'), /turnos/);
});

test('recupera almacenamiento corrupto y restablece solo el catálogo simulado', async () => {
  data.set('dato-ajeno', 'conservar');
  data.set('pokearena_mock_cards_v1', '[null]');
  assert.equal((await api.mockSearchCards({})).totalCount, 21);
  await api.resetMockCatalog();
  assert.equal(data.get('dato-ajeno'), 'conservar');
  assert.equal(data.has('pokearena_mock_cards_v1'), false);
});

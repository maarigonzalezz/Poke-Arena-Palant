import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { createRequire } from 'node:module';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';
import ts from 'typescript';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';

const require = createRequire(import.meta.url);
const cache = new Map();

// Compila los componentes reales para probar su HTML sin sustituir servicios ni React.
function moduleUrl(path) {
  if (cache.has(path)) return cache.get(path);
  let source = readFileSync(path, 'utf8');
  source = source.replaceAll('import.meta.env', '({})');
  let compiled = ts.transpileModule(source, { compilerOptions: {
    target: ts.ScriptTarget.ES2023, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText;
  compiled = compiled.replace(/from (["'])([^"']+)\1/g, (_, quote, specifier) => {
    if (!specifier.startsWith('.')) return 'from ' + JSON.stringify(pathToFileURL(require.resolve(specifier)).href);
    const base = resolve(dirname(path), specifier);
    const dependency = [base, base + '.tsx', base + '.ts'].find(existsSync);
    return 'from ' + JSON.stringify(moduleUrl(dependency));
  });
  const url = 'data:text/javascript;base64,' + Buffer.from(compiled).toString('base64');
  cache.set(path, url);
  return url;
}
const { default: PlayerCard } = await import(moduleUrl(fileURLToPath(new URL('../src/components/PlayerCard.tsx', import.meta.url))));
const card = {
  code: 'PKA-ELE-001', name: 'Pikachu', type: 'Eléctrico', rarity: 'Común',
  cost: 2, power: 5, abilityName: 'Sin Habilidad', quantity: 1,
  imageUrl: '/files/cards/original.png', imageSmallUrl: '/files/cards/small.png',
};

test('asignación usa la miniatura generada y conserva un nombre accesible', () => {
  const html = renderToStaticMarkup(createElement(PlayerCard, { card, variant: 'mini' }));
  assert.match(html, /src="http:\/\/localhost:5003\/files\/cards\/small.png"/);
  assert.match(html, /alt="Carta Pikachu"/);
  assert.doesNotMatch(html, /original.png|player-card-symbol/);
});

test('catálogo y colección reutilizan la carta grande con datos y pertenencia', () => {
  const html = renderToStaticMarkup(createElement(PlayerCard, { card }));
  assert.match(html, /original.png/);
  assert.match(html, /En tu colección/);
  assert.match(html, /PKA-ELE-001/);
});

test('miniatura ausente no muestra un icono ni descarga el original como reemplazo', () => {
  const html = renderToStaticMarkup(createElement(PlayerCard, { card: { ...card, imageSmallUrl: null }, variant: 'mini' }));
  assert.match(html, /Imagen no disponible/);
  assert.doesNotMatch(html, /<img|original.png/);
});

test('la misma carta permite quitar una selección desde el constructor de mazos', () => {
  const html = renderToStaticMarkup(createElement(PlayerCard, { card, selected: true, canAdd: false, onDeckToggle() {} }));
  assert.match(html, /Quitar del mazo/);
  assert.doesNotMatch(html, /disabled/);
});

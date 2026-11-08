import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import ts from 'typescript';

const source = readFileSync(new URL('../src/pages/playerCart.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, {
  compilerOptions: { target: ts.ScriptTarget.ES2023, module: ts.ModuleKind.ESNext },
}).outputText;
const { playerCartReducer: reduce, EMPTY_PLAYER_CART: empty } = await import(
  `data:text/javascript;base64,${Buffer.from(compiled).toString('base64')}`);
const cards = [{ code: 'owned', quantity: 1 }, { code: 'a', quantity: 0 }, { code: 'b', quantity: 0 }];
const toggle = (state, code) => reduce(state, { type: 'toggle', code, cards });
const buy = (state, currentCards = cards) => reduce(state, { type: 'purchase', cards: currentCards });

test('las cartas existentes pueden agregarse aunque ya se posean', () => {
  assert.deepEqual(toggle(empty, 'owned').quantities, { owned: 1 });
  assert.deepEqual(toggle(empty, 'missing'), empty);
  assert.deepEqual(toggle(empty, 'a').quantities, { a: 1 });
});

test('quitar y volver a agregar no duplica cartas ni afecta las demás', () => {
  const selected = toggle(toggle(empty, 'a'), 'b');
  const removed = toggle(selected, 'a');
  assert.deepEqual(removed.quantities, { b: 1 });
  assert.deepEqual(toggle(removed, 'a').quantities, { b: 1, a: 1 });
  assert.deepEqual(empty, { quantities: {}, purchasedCodes: [] });
});

test('comprar vacía el carrito y permite comprar otra copia después', () => {
  const purchased = buy(toggle(toggle(empty, 'a'), 'b'));
  assert.deepEqual(purchased, { quantities: {}, purchasedCodes: ['a', 'b'] });
  assert.deepEqual(buy(toggle(purchased, 'a')).purchasedCodes, ['a', 'b', 'a']);
  assert.deepEqual(buy(purchased), purchased);
});

test('la compra revalida cartas eliminadas y acepta cartas ya adquiridas', () => {
  const selected = toggle(toggle(empty, 'a'), 'b');
  assert.deepEqual(buy(selected, [{ code: 'a', quantity: 1 }]), { quantities: {}, purchasedCodes: ['a'] });
});

test('añadir varias veces mantiene una sola carta en el carrito', () => {
  const action = { type: 'add', code: 'owned', cards };
  const selected = reduce(empty, action);
  assert.deepEqual(reduce(selected, action).quantities, { owned: 1 });
  assert.deepEqual(buy(selected).purchasedCodes, ['owned']);
});

test('salir descarta el carrito y las compras de demostración', () => {
  const state = toggle(buy(toggle(empty, 'a')), 'b');
  assert.deepEqual(reduce(state, { type: 'reset' }), empty);
});

test('comprar varias copias respeta la cantidad de cada carta y permite repetir', () => {
  const selected = reduce(toggle(empty, 'b'), { type: 'quantity', code: 'owned', quantity: 3, cards });
  const purchased = buy(selected);
  assert.deepEqual(purchased.quantities, {});
  assert.equal(purchased.purchasedCodes.filter(code => code === 'owned').length, 3);
  assert.equal(purchased.purchasedCodes.filter(code => code === 'b').length, 1);
  assert.equal(buy(toggle(purchased, 'owned')).purchasedCodes.filter(code => code === 'owned').length, 4);
});

test('cantidad cero elimina la carta y permite añadirla de nuevo', () => {
  const selected = toggle(toggle(empty, 'a'), 'b');
  const removed = reduce(selected, { type: 'quantity', code: 'a', quantity: 0, cards });
  assert.deepEqual(removed.quantities, { b: 1 });
  assert.deepEqual(toggle(removed, 'a').quantities, { b: 1, a: 1 });
  assert.deepEqual(buy(removed).purchasedCodes, ['b']);
});

test('cantidades negativas, fraccionarias o inválidas no modifican el carrito', () => {
  const selected = toggle(empty, 'a');
  for (const quantity of [-1, 1.5, NaN, Infinity]) {
    assert.deepEqual(reduce(selected, { type: 'quantity', code: 'a', quantity, cards }), selected);
  }
});

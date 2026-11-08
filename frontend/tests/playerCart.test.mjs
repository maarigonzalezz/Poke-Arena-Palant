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

test('solo las cartas existentes que no se poseen pueden agregarse', () => {
  assert.deepEqual(toggle(empty, 'owned'), empty);
  assert.deepEqual(toggle(empty, 'missing'), empty);
  assert.deepEqual(toggle(empty, 'a').cartCodes, ['a']);
});

test('quitar y volver a agregar no duplica cartas ni afecta las demás', () => {
  const selected = toggle(toggle(empty, 'a'), 'b');
  const removed = toggle(selected, 'a');
  assert.deepEqual(removed.cartCodes, ['b']);
  assert.deepEqual(toggle(removed, 'a').cartCodes, ['b', 'a']);
  assert.deepEqual(empty, { cartCodes: [], purchasedCodes: [] });
});

test('comprar vacía el carrito e impide volver a comprar las cartas adquiridas', () => {
  const purchased = buy(toggle(toggle(empty, 'a'), 'b'));
  assert.deepEqual(purchased, { cartCodes: [], purchasedCodes: ['a', 'b'] });
  assert.deepEqual(toggle(purchased, 'a'), purchased);
  assert.deepEqual(buy(purchased), purchased);
});

test('la compra revalida cartas eliminadas o ya adquiridas antes de finalizar', () => {
  const selected = toggle(toggle(empty, 'a'), 'b');
  assert.deepEqual(buy(selected, [{ code: 'a', quantity: 1 }]), empty);
});

test('salir descarta el carrito y las compras de demostración', () => {
  const state = toggle(buy(toggle(empty, 'a')), 'b');
  assert.deepEqual(reduce(state, { type: 'reset' }), empty);
});

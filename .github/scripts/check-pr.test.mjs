import { test } from 'node:test';
import assert from 'node:assert/strict';
import { validatePullRequest } from './check-pr.mjs';

const body = `## Description
Permite buscar una partida con el deck del jugador.
## Changes Made
Valida saldo y cartas; conserva la cola en PostgreSQL.
## How to Test
Ejecutar dotnet test y buscar desde dos cuentas con decks propios.
## Related Issues
US Buscar Partida, sprint 3.
`;
const pr = (base, head) => ({ base: { ref: base }, head: { ref: head }, title: 'feat(matchmaking): buscar partida', body });

test('historias y correcciones se integran en develop; entregas y urgencias van a main', () => {
  for (const [base, head] of [['develop','feature/search_match'], ['develop','release/v1.0.0'],
    ['develop','hotfix/acceso'], ['develop','main'], ['main','release/v1.0.0'], ['main','hotfix/acceso']])
    assert.deepEqual(validatePullRequest(pr(base, head)), []);
});

test('rechaza historias directas a main y ramas sin el flujo definido', () => {
  assert.ok(validatePullRequest(pr('main','feature/search_match')).some(error => error.includes('main solo')));
  assert.ok(validatePullRequest(pr('develop','cambio-rapido')).length);
});

test('la plantilla sin completar no se considera evidencia de revisión', () => {
  assert.equal(validatePullRequest({ ...pr('develop','feature/search_match'), body: '## Description\n<!-- texto -->\n-\n## How to Test\n1.\n2.' }).length, 4);
});

test('los títulos de integración deben ser claros y seguir Conventional Commits', () => {
  assert.ok(validatePullRequest({ ...pr('develop','feature/search_match'), title: 'cambio rapido' }).length);
  assert.deepEqual(validatePullRequest({ ...pr('develop','feature/search_match'), title: 'fix(api)!: ajustar contrato de ingreso' }), []);
});

test('el borrador se vuelve a comprobar al pasar a revisión', () => {
  const incomplete = { ...pr('main','feature/search_match'), title: '', body: '' };
  assert.deepEqual(validatePullRequest({ ...incomplete, draft: true }), []);
  assert.ok(validatePullRequest({ ...incomplete, draft: false }).length);
});

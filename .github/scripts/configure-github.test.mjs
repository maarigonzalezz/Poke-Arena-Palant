import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { combineRules } from '../../scripts/configure-github.mjs';

const desired = JSON.parse(readFileSync(new URL('../rulesets/main.json', import.meta.url), 'utf8'));

test('las protecciones preparadas mantienen dos aprobaciones y los tres checks reales', () => {
  assert.equal(desired.rules.find(rule => rule.type === 'pull_request').parameters.required_approving_review_count, 2);
  assert.deepEqual(desired.rules.find(rule => rule.type === 'required_status_checks').parameters.required_status_checks.map(check => check.context),
    ['PR policy', 'Frontend', 'Backend']);
});

test('actualizar no elimina reglas previas ni reduce una revisión más exigente', () => {
  const current = structuredClone(desired);
  current.rules.find(rule => rule.type === 'pull_request').parameters.required_approving_review_count = 3;
  current.rules.push({ type: 'required_signatures' });
  const updated = combineRules(current, desired);
  assert.equal(updated.rules.find(rule => rule.type === 'pull_request').parameters.required_approving_review_count, 3);
  assert.ok(updated.rules.some(rule => rule.type === 'required_signatures'));
});

test('no sobrescribe reglas de otras ramas ni excepciones de administración', () => {
  assert.throws(() => combineRules({ ...desired, conditions: { ref_name: { include: ['refs/heads/otra'], exclude: [] } } }, desired), /otras referencias/);
  assert.throws(() => combineRules({ ...desired, bypass_actors: [{ actor_id: 1 }] }, desired), /excepciones/);
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const script = fileURLToPath(new URL('./create-release.mjs', import.meta.url));

// Los casos inválidos deben detenerse antes de necesitar credenciales o llamar a GitHub.
test('una entrega no puede publicarse desde una rama de trabajo', () => {
  const result = spawnSync(process.execPath, [script], {
    encoding: 'utf8', env: { ...process.env, GITHUB_REF: 'refs/heads/feature/search_match', RELEASE_VERSION: 'v1.0.0' },
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /desde main/);
});

test('el nombre de versión rechaza entradas inválidas antes de publicar', () => {
  for (const version of ['1.0.0', 'v01.0.0', 'v1.0', 'v1.0.0;comando']) {
    const result = spawnSync(process.execPath, [script], {
      encoding: 'utf8', env: { ...process.env, GITHUB_REF: 'refs/heads/main', RELEASE_VERSION: version },
    });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /vMAJOR.MINOR.PATCH/);
  }
});

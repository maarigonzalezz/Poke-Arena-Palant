// Publica exactamente el commit revisado y probado, sin reemplazar etiquetas existentes.
const version = process.env.RELEASE_VERSION ?? '';
if (process.env.GITHUB_REF !== 'refs/heads/main'
    || !/^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.test(version)) {
  throw new Error('Solo se admiten versiones vMAJOR.MINOR.PATCH publicadas desde main.');
}
const repository = process.env.GITHUB_REPOSITORY;
const token = process.env.GITHUB_TOKEN;
const commit = process.env.GITHUB_SHA;
if (!repository || !token || !/^[a-f0-9]{40}$/.test(commit ?? '')) {
  throw new Error('Falta el contexto de ejecución autenticado de GitHub Actions.');
}
const headers = { Accept: 'application/vnd.github+json', Authorization: `Bearer ${token}`,
  'X-GitHub-Api-Version': '2022-11-28' };
const base = `https://api.github.com/repos/${repository}`;
const existing = await fetch(`${base}/git/ref/tags/${encodeURIComponent(version)}`, { headers });
if (existing.status !== 404) {
  throw new Error(existing.ok ? 'La etiqueta ya existe; seleccione una versión nueva.'
    : `No se pudo verificar la etiqueta (HTTP ${existing.status}).`);
}
const result = await fetch(`${base}/releases`, {
  method: 'POST', headers: { ...headers, 'Content-Type': 'application/json' },
  body: JSON.stringify({ tag_name: version, target_commitish: commit, name: version,
    generate_release_notes: true, draft: false, prerelease: false }),
});
if (!result.ok) throw new Error(`No se pudo publicar la entrega (HTTP ${result.status}).`);
console.log(`Release creada: ${(await result.json()).html_url}`);

import { readFileSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { dirname, join, resolve } from 'node:path';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const names = ['main', 'develop', 'version-tags'];

// Conserva exigencias existentes más estrictas y evita sobrescribir reglas de otras ramas.
export function combineRules(current, desired) {
  if (!current) return desired;
  const refs = value => JSON.stringify({
    include: [...(value.conditions?.ref_name?.include ?? [])].sort(),
    exclude: [...(value.conditions?.ref_name?.exclude ?? [])].sort(),
  });
  if (current.target !== desired.target || refs(current) !== refs(desired))
    throw new Error(`La regla ${current.name} protege otras referencias; requiere revisión manual.`);
  if (current.bypass_actors?.length)
    throw new Error(`La regla ${current.name} contiene excepciones; revíselas antes de reemplazarla.`);
  const rules = new Map((current.rules ?? []).map(rule => [rule.type, rule]));
  for (const rule of desired.rules) {
    const old = rules.get(rule.type);
    let merged = rule;
    if (old && rule.type === 'pull_request') {
      const before = old.parameters, after = rule.parameters;
      if (before.allowed_merge_methods && !before.allowed_merge_methods.includes('squash'))
        throw new Error('La regla existente prohíbe Squash; requiere revisión manual.');
      const parameters = { ...before, ...after,
        required_approving_review_count: Math.max(before.required_approving_review_count, after.required_approving_review_count) };
      for (const key of ['dismiss_stale_reviews_on_push', 'require_code_owner_review', 'require_last_push_approval', 'required_review_thread_resolution'])
        parameters[key] = Boolean(before[key] || after[key]);
      merged = { ...rule, parameters };
    }
    if (old && rule.type === 'required_status_checks') {
      const checks = new Map((old.parameters.required_status_checks ?? []).map(check => [check.context, check]));
      for (const check of rule.parameters.required_status_checks) {
        const previous = checks.get(check.context);
        if (previous?.integration_id && previous.integration_id !== check.integration_id)
          throw new Error(`El check ${check.context} tiene otro proveedor; requiere revisión manual.`);
        checks.set(check.context, check);
      }
      merged = { ...rule, parameters: { ...old.parameters, ...rule.parameters,
        required_status_checks: [...checks.values()] } };
    }
    rules.set(rule.type, merged);
  }
  return { ...desired, rules: [...rules.values()] };
}

async function main() {
  const args = process.argv.slice(2);
  const apply = args.includes('--apply');
  const index = args.indexOf('--repo');
  const repository = index < 0 ? 'maarigonzalezz/Poke-Arena-Palant' : args[index + 1];
  if (!/^[a-z0-9_.-]+\/[a-z0-9_.-]+$/i.test(repository ?? '')
      || args.some((arg, i) => !['--check', '--apply', '--repo'].includes(arg) && !(index >= 0 && i === index + 1)))
    throw new Error('Uso: node scripts/configure-github.mjs [--check|--apply] [--repo propietario/repositorio].');
  if (args.includes('--check') && apply) throw new Error('Seleccione --check o --apply.');
  const token = process.env.GH_TOKEN || process.env.GITHUB_TOKEN;
  if (apply && !token) throw new Error('Para aplicar se necesita GH_TOKEN o GITHUB_TOKEN de una cuenta administradora.');
  const headers = { Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28',
    ...(token ? { Authorization: `Bearer ${token}` } : {}) };
  async function api(path, method = 'GET', body) {
    const response = await fetch(`https://api.github.com/repos/${repository}${path}`, {
      method, headers: { ...headers, 'Content-Type': 'application/json' },
      ...(body ? { body: JSON.stringify(body) } : {}),
    });
    if (!response.ok) throw new Error(`GitHub respondió HTTP ${response.status} al consultar o configurar ${path}. No se imprimen credenciales.`);
    return await response.json();
  }
  const repo = await api('');
  const existing = await api('/rulesets?per_page=100');
  const desired = names.map(name => JSON.parse(readFileSync(join(root, '.github/rulesets', name + '.json'), 'utf8')));
  if (!apply) {
    console.log(JSON.stringify({ repository, mode: 'check', admin: repo.permissions?.admin ?? null,
      current: existing.map(({ id, name, enforcement }) => ({ id, name, enforcement })),
      planned: desired.map(({ name, target }) => ({ name, target })) }, null, 2));
    return;
  }
  if (!repo.permissions?.admin) throw new Error('La cuenta o el token no pueden administrar este repositorio. No se aplicaron cambios.');
  const runs = await api('/actions/runs?status=success&per_page=100');
  const ci = runs.workflow_runs.find(run => run.path === '.github/workflows/ci.yml');
  if (!ci) throw new Error('Publique primero CI y ejecute un PR correctamente antes de exigir sus checks.');
  const jobs = await api(`/actions/runs/${ci.id}/jobs?per_page=100`);
  for (const name of ['PR policy', 'Frontend', 'Backend']) {
    if (!jobs.jobs.some(job => job.name === name && job.conclusion === 'success'))
      throw new Error(`No se encontró un check exitoso llamado ${name}; no se activaron reglas.`);
  }
  // Prepara todas las actualizaciones antes de enviar la primera mutación.
  const changes = [];
  for (const ruleset of desired) {
    const found = existing.find(item => item.name === ruleset.name);
    const current = found ? await api(`/rulesets/${found.id}`) : null;
    changes.push({ path: found ? `/rulesets/${found.id}` : '/rulesets',
      method: found ? 'PUT' : 'POST', body: combineRules(current, ruleset) });
  }
  for (const change of changes) {
    await api(change.path, change.method, change.body);
    console.log(`Regla activa: ${change.body.name}`);
  }
  await api('', 'PATCH', { allow_squash_merge: true, allow_merge_commit: false, allow_rebase_merge: false,
    allow_update_branch: true, delete_branch_on_merge: true });
  console.log('Configuración aplicada. Verifique los PR y rulesets en GitHub.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href)
  main().catch(error => { console.error(error.message); process.exitCode = 1; });

import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const titlePattern = /^(feat|fix|refactor|docs|test|chore|ci|perf|build|revert)(\([a-z0-9._/-]+\))?!?:\s+\S.+$/i;
const topicBranch = /^(feature|release|hotfix)\/[a-z0-9][a-z0-9._/-]*$/i;

// Valida metadatos; las aprobaciones y conversaciones las exige GitHub mediante rulesets.
export function validatePullRequest(pr) {
  if (pr.draft) return [];
  const errors = [];
  const base = pr.base?.ref ?? '';
  const head = pr.head?.ref ?? '';
  if (base === 'main' && !/^(release|hotfix)\//.test(head))
    errors.push('main solo recibe release/* o hotfix/*; las historias se integran primero en develop.');
  else if (base === 'develop' && !(head === 'main' || topicBranch.test(head)))
    errors.push('develop recibe feature/*, release/*, hotfix/* o main para sincronización.');
  else if (!['main', 'develop'].includes(base))
    errors.push('El destino debe ser main o develop.');
  if (!titlePattern.test(pr.title ?? ''))
    errors.push('Use un título como feat(matchmaking): buscar partida o ci: verificar los PR.');
  const body = (pr.body ?? '').replace(/<!--[\s\S]*?-->/g, '').replace(/\r/g, '');
  const sections = [
    ['Description', /description|descripci[oó]n|resumen/i],
    ['Changes Made', /changes made|cambios realizados|cambios/i],
    ['How to Test', /how to test|c[oó]mo probar|validaci[oó]n|pruebas/i],
    ['Related Issues', /related issues|historia de usuario|referencias|relacionados/i],
  ];
  const headings = [...body.matchAll(/^##\s+(.+)$/gm)];
  for (const [name, pattern] of sections) {
    const index = headings.findIndex(match => pattern.test(match[1].trim()));
    const content = index < 0 ? '' : body.slice(headings[index].index + headings[index][0].length,
      headings[index + 1]?.index ?? body.length);
    if (!content.replace(/^[\s\d.*\-\[\]x]+$/gm, '').trim())
      errors.push(`Complete la sección "${name}" con información concreta.`);
  }
  return errors;
}

function main() {
  if (process.env.GITHUB_EVENT_NAME !== 'pull_request') {
    console.log('No hay metadatos de PR que validar en este evento.');
    return;
  }
  const eventPath = process.env.GITHUB_EVENT_PATH;
  if (!eventPath) throw new Error('Falta el archivo del evento de GitHub.');
  const event = JSON.parse(readFileSync(eventPath, 'utf8'));
  if (!event.pull_request) throw new Error('El evento no contiene un Pull Request.');
  const errors = validatePullRequest(event.pull_request);
  // Escapa controles para que el contenido del PR no pueda inyectar anotaciones.
  for (const error of errors) console.error('::error::' + error.replaceAll('%', '%25').replaceAll('\r', '%0D').replaceAll('\n', '%0A'));
  if (errors.length) process.exitCode = 1;
  else console.log(event.pull_request.draft ? 'PR en borrador; se validará al solicitar revisión.' : 'Flujo de ramas, título y evidencia del PR correctos.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) main();

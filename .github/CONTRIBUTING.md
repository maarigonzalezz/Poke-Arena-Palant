# Desarrollo y control de versiones

## Ramas

| Rama | Origen | Destino del PR | Uso |
| --- | --- | --- | --- |
| main | — | — | Versión estable y revisada. |
| develop | main | — | Integración de historias. |
| feature/descripcion | develop | develop | Una historia, corrección o mejora concreta. |
| release/vX.Y.Z | develop | main y develop si contiene ajustes nuevos | Preparación de una entrega. |
| hotfix/descripcion | main | main y develop | Corrección urgente de la versión estable. |

No se trabaja directamente en main ni develop. Los cambios se integran mediante PR.
Las ramas anteriores del proyecto no se renombran ni se reescribe su historial.

## Trabajo diario

```bash
git switch develop
git pull --ff-only origin develop
git switch -c feature/descripcion
# Desarrollar y ejecutar las comprobaciones aplicables.
git add ruta/al/archivo
git commit -m "feat(modulo): describir el cambio"
git push -u origin feature/descripcion
```

Abrir un PR hacia develop y completar la plantilla. Usar Draft mientras falten cambios.
El título sigue Conventional Commits: feat, fix, refactor, docs, test, chore, ci, perf, build o revert.
Ejemplos: `feat(matchmaking): buscar partida` y `fix(auth): validar cuenta deshabilitada`.
La integración utiliza Squash and merge para conservar un commit descriptivo e historial lineal.

## Revisión

- Dos personas distintas del autor deben aprobar el PR.
- Al menos una aprobación debe satisfacer CODEOWNERS.
- Los cambios nuevos descartan aprobaciones anteriores.
- Deben resolverse las conversaciones y aprobarse el último cambio revisable.
- PR policy, Frontend y Backend deben estar verdes y la rama actualizada.
- La autoevaluación del autor no reemplaza GitHub Review.

Los responsables iniciales son @maarigonzalezz, @diegosalasov, @SpaceBa13 y @fredeos.
GitHub solicita a los propietarios; el autor coordina que al menos dos revisen efectivamente.
Véase [la guía de revisión](CODE_REVIEW.md).

## Comprobaciones

```bash
node --test .github/scripts/check-pr.test.mjs
cd frontend
npm ci
npm run lint
npm run test:cards
npm run build
```

Backend, desde la raíz y con Docker iniciado:

```powershell
$env:POKEARENA_INTEGRATION_TESTS = "1"
dotnet test backend/tests/PokeArena.Tests/PokeArena.Tests.csproj -c Release
```

En Linux: `POKEARENA_INTEGRATION_TESTS=1 dotnet test backend/tests/PokeArena.Tests/PokeArena.Tests.csproj -c Release`.
CI utiliza PostgreSQL temporal real. No necesita las contraseñas ni los datos locales del equipo.
La prueba positiva de Google requiere un token real vigente y se verifica manualmente cuando no esté disponible.

## Entregas y hotfixes

Crear release/vX.Y.Z desde develop, revisar sus ajustes y abrir el PR a main.
Si hay ajustes nuevos en release, devolverlos a develop mediante otro PR.
Para una urgencia, crear hotfix/descripcion desde main y abrir PR a main y a develop.
No dar por cerrado el hotfix hasta propagarlo a develop.

Después de integrar en main, ejecutar Actions → Release → Run workflow, seleccionando main y una versión nueva como v1.0.0.
El workflow verifica nuevamente CI y crea la etiqueta y la Release del commit verificado.
MAJOR cambia compatibilidad, MINOR agrega funcionalidad compatible y PATCH corrige errores compatibles.
Una etiqueta publicada no se mueve ni se elimina; una corrección usa una versión nueva.
Las notas de la Release se generan a partir de los PR integrados.

La activación de las protecciones está explicada en [SETUP.md](SETUP.md).

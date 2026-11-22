# Activación en GitHub

## Estado comprobado

- main tiene una regla activa que exige dos aprobaciones e historial lineal.
- develop no tenía protección al revisar el repositorio.
- La conexión utilizada puede subir código, pero no administrar las reglas.
- Los archivos locales no activan reglas ni workflows hasta publicarlos en GitHub.

## Publicar la configuración

1. Subir estos cambios dentro de un PR a develop y obtener las revisiones requeridas.
2. Comprobar en Actions que aparezcan y terminen PR policy, Frontend y Backend.
3. Integrar los archivos también en main mediante el flujo de release.
4. CODEOWNERS debe existir en la rama destino para solicitar revisiones automáticamente.

La configuración requiere que cada rama integrada contenga los archivos actuales de backend y frontend.
No se deben crear reglas que exijan checks inexistentes antes de publicar y ejecutar CI.

## Activar protecciones con una cuenta administradora

Opción desde GitHub:

1. Abrir Settings → Rules → Rulesets.
2. Actualizar la regla main existente usando rulesets/main.json como referencia.
3. Importar rulesets/develop.json y rulesets/version-tags.json.
4. Dejar las reglas en Active y seleccionar GitHub Actions como origen de los checks.
5. En Settings → General → Pull Requests, permitir Squash merge y desactivar Merge commits y Rebase merge.
6. Activar la eliminación automática de ramas temporales.
7. Verificar que los cuatro CODEOWNERS tengan permiso de escritura.

Los rulesets mantienen dos aprobaciones, exigen revisión de propietarios y del último cambio, resuelven conversaciones y bloquean eliminación y force push.
Los checks obligatorios son PR policy, Frontend y Backend. No se configuran bypasses.
Las etiquetas v* quedan protegidas contra reemplazo y eliminación.

Opción automatizada, con Node 22 y un token de la cuenta administradora en GH_TOKEN o GITHUB_TOKEN:

```bash
# Consulta pública: no modifica el repositorio.
node scripts/configure-github.mjs --check
# Aplica reglas y opciones de integración después de que CI esté disponible.
node scripts/configure-github.mjs --apply
```

El token necesita Administration: write; no se guarda en archivos ni se imprime.
El script comprueba los permisos y que los checks hayan pasado antes de activar nuevas exigencias.

## Verificar que se cumple la estrategia

- Un feature/* con destino main debe fallar PR policy.
- Un PR con la plantilla vacía debe fallar PR policy.
- No debe poder integrarse sin dos aprobaciones o con conversaciones pendientes.
- Un cambio posterior debe exigir nueva revisión.
- Una prueba fallida debe bloquear la integración.
- Un push directo, force push o eliminación de main/develop debe rechazarse.
- Release solo debe ejecutarse desde main con una versión vMAJOR.MINOR.PATCH nueva.

## Evidencia para la entrega

Conservar enlaces a un PR aprobado, sus resultados de Actions, los rulesets activos y una Release publicada.
La preparación local no reemplaza esa evidencia de ejecución en GitHub.

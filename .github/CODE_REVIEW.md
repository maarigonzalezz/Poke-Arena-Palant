# Revisión de Pull Requests

## Autor

1. Crear una rama conforme al flujo y mantener el PR centrado en una historia.
2. Revisar el diff completo y completar descripción, cambios, pruebas e historia relacionada.
3. Ejecutar las comprobaciones aplicables y adjuntar resultados o pasos reproducibles.
4. Pasar de Draft a Ready for review y coordinar dos revisores distintos del autor.
5. Responder observaciones, corregir y solicitar nueva revisión.

## Revisor

Revisar el comportamiento y las reglas de la historia antes que el estilo:

- Se cumplen los criterios de éxito, errores y alternos.
- Las validaciones del negocio están en Application o Domain.
- Los repositorios concentran persistencia, consultas y transacciones.
- Se comprueban identidad, propiedad de recursos y ausencia de credenciales.
- Los reintentos, la concurrencia y la reversión no duplican efectos.
- Los contratos, migraciones y pasos de ejecución son compatibles y están documentados.
- Las pruebas verifican comportamiento relevante y no requieren datos privados del autor.
- Las constantes y componentes compartidos evitan reglas y código duplicados.

Usar comentarios en Files changed para identificar el archivo y la línea.
Marcar como bloqueante una corrección necesaria y como sugerencia una mejora opcional.
Registrar Request changes si quedan defectos bloqueantes; Comment para observaciones y Approve cuando esté listo.

## Integración

Solo integrar un PR listo, con dos aprobaciones válidas, conversaciones resueltas y CI verde.
GitHub exige estas condiciones al activar los rulesets; una plantilla marcada no constituye una aprobación.
Usar Squash and merge y conservar un título descriptivo.
Eliminar la rama temporal después de integrar y completar cualquier propagación a develop.
No mezclar cambios urgentes sin revisión ni utilizar force push en main, develop o etiquetas publicadas.

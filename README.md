# Poke-Arena-Palant

El flujo de ramas, las entregas y las revisiones se describen en [.github/CONTRIBUTING.md](.github/CONTRIBUTING.md).
La activación de protecciones requiere los pasos de [.github/SETUP.md](.github/SETUP.md).

## Probar partidas desde la interfaz

1. Inicie PostgreSQL local y la API configurada para usarlo. El puerto publicado por Compose es `55432`; MongoDB usa `27018` para evitar conflictos con una instalación local.
2. Desde PowerShell, ejecute `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\prepare-matchmaking-ui-demo.ps1`. El script aplica las migraciones de matchmaking, prepara cartas reutilizando imágenes locales existentes y crea tres ubicaciones de demostración sin borrar datos.
3. Inicie la API con `dotnet run --project backend/src/PokeArena.Api --urls http://localhost:5003` y la interfaz con `npm.cmd --prefix frontend run dev`; abra `http://localhost:5173`. Inicie sesión con Google; la primera sesión recibe cartas para construir un deck.
4. En la sección **Mazos**, guarde un deck de 12 cartas distintas. Luego, en **Buscar partida**, seleccione ese deck.
5. Puede esperar a otro jugador o, después de 60 segundos de búsqueda activa, aceptar la oferta del bot. El tablero permite jugar cartas en ubicaciones, descartarlas al cementerio y muestra el robo, energía y temporizador de cada turno de 30 segundos.
6. Al terminar el turno 6, gana quien venza en más ubicaciones comparando el poder de sus cartas en cada una. Si empatan en ubicaciones ganadas, desempata la energía restante y luego el menor cementerio; si persiste el empate, se devuelve la apuesta. En clasificatoria cada jugador aporta 4 cubos y el ganador recibe el pozo de 8.

El catálogo de demostración no reemplaza ubicaciones ni cartas existentes. También se pueden crear ubicaciones propias desde el estudio administrativo; la preparación solo elige ubicaciones activas, con imagen lista, y garantiza tres distintas, al menos dos jugables y sin restricción de turno.

# Identidad provisional

## Nombre

**Aldune** es el nombre de la aplicación, de los proyectos de código fuente (`Aldune`, `Aldune.Core`, `Aldune.SyncServer`), namespaces, datos locales e instalador.

Se mantiene compatibilidad transparente con datos anteriores (`%LOCALAPPDATA%\Fanote` se migra automáticamente si existe) y con variables de entorno o tokens heredados (`FANOTE_*`).

## Ruta de datos local

La aplicación usa `%LOCALAPPDATA%\Aldune`. La primera ejecución de la versión Aldune
migra automáticamente la carpeta anterior `%LOCALAPPDATA%\Fanote` si todavía no existe la nueva.

## Icono actual

El original vectorial del icono está en `src/Aldune/Assets/aldune-logo.svg`. El ICO actualizado usa
tres tarjetas horizontales de colores pastel, desplazadas sobre un fondo oscuro. Es coherente con
el dock y se reconoce mejor que un símbolo genérico de nota a tamaño pequeño.

Si se elige otro nombre para la versión pública, se rediseñará el símbolo junto con la marca, la
pantalla de instalación y los recursos de la tienda.

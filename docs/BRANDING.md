# Identidad provisional

## Nombre

**Aldune** pasa a ser el nombre de la aplicación, de los datos locales y del instalador. `Fanote`
queda como nombre interno del repositorio, los namespaces y los identificadores del protocolo para
conservar compatibilidad.

Antes de una publicación comercial conviene hacer una búsqueda completa de marca, dominios y
nombres en las tiendas. Ya existe al menos una aplicación con el nombre muy parecido `FanNote`.

El nombre no se considera libre de forma definitiva hasta comprobar marca, dominios, tiendas y
posibles conflictos en los idiomas objetivo.

## Ruta de datos local

La identidad visible usa ahora `%LOCALAPPDATA%\\Aldune`. La primera ejecución de la versión Aldune
migra automáticamente la carpeta anterior `%LOCALAPPDATA%\\Fanote` si todavía no existe la nueva.
`Fanote` se conserva en namespaces, nombres de proyecto y protocolo para no romper compatibilidad.

## Icono actual

El original vectorial del icono está en `src/Fanote/Assets/aldune-logo.svg`. El ICO actualizado usa
tres tarjetas horizontales de colores pastel, desplazadas sobre un fondo oscuro. Es coherente con
el dock y se reconoce mejor que un símbolo genérico de nota a tamaño pequeño.

Si se elige otro nombre para la versión pública, se rediseñará el símbolo junto con la marca, la
pantalla de instalación y los recursos de la tienda.

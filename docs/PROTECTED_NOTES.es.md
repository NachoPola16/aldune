# Notas protegidas

Una nota protegida se cifra con AES-GCM usando una clave derivada de su contraseña mediante PBKDF2
SHA-256. Aldune no guarda la contraseña ni una copia utilizable de ella.

Mientras está bloqueada, el dock y Gestionar notas muestran únicamente `Nota protegida`, salvo que le pongas un **nombre visible**
(opcional, por nota: al protegerla o desde «Nombre visible…»). Ese nombre es un campo aparte, en claro, de una sola línea y hasta 60
caracteres; no sale del contenido y cualquiera que vea tu pantalla lo ve. Viaja por la sincronización (campo `protectedTitle` del
sobre, cifrado con la clave del perfil como las etiquetas), sin subir el formato 4: las versiones anteriores a la 1.8.0 de Windows y
a la 0.2.0 de Android lo ignoran y lo pierden si reenvían esa nota, así que conviene actualizar todos los dispositivos. Quitar la
protección lo borra. Al abrirla
se pide la contraseña; queda desbloqueada durante esa sesión de edición y vuelve a quedar bloqueada
al cerrarla.

La protección forma parte del sobre de sincronización: el servidor y la carpeta compartida solo
reciben datos cifrados, y cada dispositivo debe introducir la contraseña para leer la nota. Las
versiones anteriores al formato de sincronización 3 rechazan el sobre para no reemplazar de forma
silenciosa una nota protegida.

La contraseña no se puede recuperar. Conviene probar primero con una nota de prueba y conservar la
contraseña en un gestor de contraseñas.

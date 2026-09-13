# Notas protegidas

Una nota protegida se cifra con AES-GCM usando una clave derivada de su contraseña mediante PBKDF2
SHA-256. Aldune no guarda la contraseña ni una copia utilizable de ella.

Mientras está bloqueada, el dock y Gestionar notas muestran únicamente `Nota protegida`. Al abrirla
se pide la contraseña; queda desbloqueada durante esa sesión de edición y vuelve a quedar bloqueada
al cerrarla.

La protección forma parte del sobre de sincronización: el servidor y la carpeta compartida solo
reciben datos cifrados, y cada dispositivo debe introducir la contraseña para leer la nota. Las
versiones anteriores al formato de sincronización 3 rechazan el sobre para no reemplazar de forma
silenciosa una nota protegida.

La contraseña no se puede recuperar. Conviene probar primero con una nota de prueba y conservar la
contraseña en un gestor de contraseñas.

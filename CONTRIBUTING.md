# Contribuir

Los **issues** (fallos, dudas, ideas) son bienvenidos: di qué versión usas, qué hiciste y qué esperabas. Para preguntas e ideas abiertas,
usa [Discussions](https://github.com/NachoPola16/aldune/discussions).

Antes de enviar una Pull Request, abre un issue para hablarlo: el proyecto tiene decisiones de diseño documentadas en [docs/](docs/)
(`ROADMAP.md` recoge lo que se descartó y por qué; `STATUS.md`, lo que hay hecho) y reglas para quien toque el código en [AGENTS.md](AGENTS.md).
Los cambios de interfaz llevan textos en los cinco idiomas, y la lógica nueva, tests.

## Licencia de lo que envíes

Al enviar código aceptas que se publique bajo [GPL-3.0](LICENSE) y que quien mantiene el proyecto pueda **redistribuirlo también bajo otras
licencias** (por ejemplo, para las tiendas de aplicaciones). Firma tus commits con `Signed-off-by` (Developer Certificate of Origin,
<https://developercertificate.org>): con ello confirmas que el código es tuyo o que tienes derecho a enviarlo.

## Lo que no se acepta sin hablarlo antes

- Cambios en el formato de sincronización (el formato es el 4; las versiones instaladas rechazan lo que no entienden).
- Código que envíe datos fuera del dispositivo o añada telemetría, anuncios o rastreadores.

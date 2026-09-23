# WPF dock regression smoke

Windows interactivo y SDK .NET 10. Sin framework de tests ni paquetes adicionales: consola STA WPF que referencia la aplicación y reutiliza sus dependencias. Exit code 0 = PASS; 1 = fallo o limpieza fallida.

```powershell
dotnet run --project "C:\Users\nacho\Proyectos\aldune\tests\Aldune.Ui.SmokeTests\Aldune.Ui.SmokeTests.csproj" --configuration Debug
```

## Escenario prioritario ejecutado

1. DB SQLite nueva en un directorio temporal GUID; clave AES efímera; dos notas, una etiquetada.
2. `EdgeDockWindow` real y `AppCoordinator`, monitor ficticio (100000, 100000), sin activar ventana ni mover cursor.
3. Abre `OpenDockViewPopup` por reflexión; verifica el evento WPF `Opened` real.
4. Lanza `ButtonBase.ClickEvent` sobre el botón de etiqueta construido por producción. Pasa por `OnDockTagChoiceClick`, el coordinator y `Refresh` (dos notas -> una y cambio de geometría).
5. Comprueba gracia exactamente de cuatro segundos, renovada respecto de la apertura.
6. Espera `Closed` real: el popup debe cerrarse, pero el dock seguir expandido con el mismo deadline y el temporizador de colapso parado.
7. Bombea dispatcher durante la gracia real, muestreando estado/timer; después de vencer verifica colapso por poll y timer reales con cursor fuera.
8. Cierra dock/desinstala hook mediante coordinator, detiene todos los `DispatcherTimer`, dispone coordinator/cipher, cierra pools SQLite y borra el directorio temporal.

Se carga únicamente `Application.Resources` del `App.xaml` de producción embebido por enlace; nunca se instancia `Aldune.App` ni se ejecuta su startup. No se leen settings, DB, claves DPAPI o sincronización del usuario.

## Segundo escenario: el "⋯" de la nota con clics físicos

Crea una nota real en (300, 200) y la pulsa con `SetCursorPos` + `mouse_event`: abrir el menú, volver a
pulsar "⋯" (debe cerrarse, no reabrirse), reabrir y cerrar pulsando fuera. Este sí mueve el ratón y
necesita la ventana en primer plano: no tocar el ratón mientras corre. Existe porque el fallo del
toggle volvió tres veces: con eventos enrutados no se reproduce, solo con un clic real (ver
`PopupToggle`).

## Límites

Esta entrega se limitó por prioridad a **una** prueba de la causa concreta: cierre del selector después de cambiar etiqueta y refrescar geometría. No sustituye ni repite únicamente pruebas de `FanStateMachine`.

No cubre aún crear nota directa, `CreateNoteFromMenu` con texto, selección de otra vista, ni expiración con cursor dentro. No toca el clipboard. La renovación probada es apertura -> selección de etiqueta.

Se usan HWND, XAML, popup, eventos y timers WPF reales; el clic se dispara como evento enrutado, no como entrada física Win32. No verifica hit-testing del ratón, aspecto visual, DPI multimonitor ni fullscreen. La detección fullscreen se detiene para evitar depender de la aplicación activa. El popup real conserva sus eventos pero su contenido se hace transparente porque Windows puede recolocarlo en un monitor real. Puede capturar brevemente el ratón mientras está abierto (unos 100 ms), aunque el harness no mueve el cursor ni genera clics físicos. Requiere escritorio interactivo; mantener sin pulsar botones del ratón durante la prueba, pues producción ignora hover durante arrastres.

La prueba depende de nombres privados por reflexión: cambios de implementación pueden requerir actualizarla. Usa tiempo real de pared, por lo que pausas de depurador, saltos de reloj o carga extrema pueden producir fallos. No se añadió a la solución.

Validado con `dotnet run` Debug: PASS, 75 muestras durante la gracia y limpieza completa. La primera compilación emitió advertencias CA1416 existentes de `DatabaseKeyProvider` en Core; no errores de compilación.

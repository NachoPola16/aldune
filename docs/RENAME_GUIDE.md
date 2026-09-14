# Guía de Identidad de Marca y Renombrado Futuro

Esta guía explica cómo está estructurada la identidad de marca en **Aldune** y cómo cambiar el nombre de la aplicación de manera inmediata o integral si decides cambiarlo más adelante.

---

## 1. Cambio solo del nombre visible (Rápido - 1 minuto)

Si únicamente deseas cambiar el nombre que ve el usuario en la interfaz, los títulos de las ventanas y las notificaciones:

1. Abre `src/Aldune.Core/BrandIdentity.cs`.
2. Modifica la constante `AppName`:
   ```csharp
   public const string AppName = "MiNuevoNombre";
   ```
3. Compila la aplicación. ¡Listo! Todas las ventanas, avisos y títulos de la aplicación tomarán este nombre de inmediato a través de `Strings.AppName` y `AppInfo`.

---

## 2. Cambio integral de proyectos, carpetas y namespaces (Automatizado)

Si deseas hacer un cambio completo de identidad (renombrando proyectos `.csproj`, carpetas `src/`, namespaces de C#, solución `.slnx`, instalador y Docker):

Hemos incluido un script automatizado que realiza todo el proceso de forma segura:

```powershell
./scripts/rename-brand.ps1 -NewName "TuNuevoNombre"
```

El script:
- Renombra las carpetas y archivos con `git mv` para preservar el historial de Git.
- Actualiza namespaces en todos los archivos C# y XAML.
- Actualiza las referencias de la solución `.slnx` y de los `.csproj`.
- Actualiza los scripts de compilación, el Dockerfile y el instalador de Inno Setup.

Tras ejecutar el script, solo necesitas verificar:
```powershell
dotnet test TuNuevoNombre.slnx
```

---

## 3. Puntos de Retrocompatibilidad (No tocar)

Para evitar que los usuarios pierdan sus notas al actualizar:
- `ResolveAppDataDirectory()` en `App.xaml.cs` migra automáticamente las carpetas de datos previas si no existe la nueva.
- `StartupRegistration.cs` migra la clave de arranque del registro de Windows.
- Los tokens y puertos de sincronización aceptan tanto las variables de entorno nuevas como las legadas para no romper servidores existentes.

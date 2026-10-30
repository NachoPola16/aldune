# Study: Aldune in the Microsoft Store as MSIX (2026-10-27)

Feasibility study, no prototype run. What comes from Microsoft's documentation carries its link; the rest is mine and is marked
**(unverified)**: it has to be checked with a prototype before committing.

## Why MSIX

With an unpackaged EXE/MSI installer the Store **does not sign it** (you have to pay for a certificate from a CA trusted by Microsoft). With **MSIX**
signing is free, Microsoft hosts the distribution and you can use the Store's billing or your own or a third party's. It is the only free
signing route for selling in the Store. Source: table in
[How to distribute your Win32 app through Microsoft Store](https://learn.microsoft.com/en-us/windows/apps/distribute-through-store/how-to-distribute-your-win32-app-through-microsoft-store).

An individual Store account has been free since 2025 (verification with an ID document and a selfie); a company one costs $99.

## What changes in Aldune (from most to least serious)

| Aspect | Today | Packaged as MSIX | Work |
|---|---|---|---|
| **Data** (`%LOCALAPPDATA%\Aldune`: `notes.db`, `settings.json`) | The user's real folder | Windows 10 1903+: **new** `AppData` files are written to a private location of the package (they look merged) and **are deleted on uninstall**; files that **already exist** in the real folder are read and modified there. That is why a package “picks up where the unpackaged version left off” ([source](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes)) | A **new** Store user would have their notes in the private store: uninstalling the app deletes them. Mitigate with the local copy (`LocalBackup`) and sync, warn on uninstall, or de-virtualize `AppData` with a restricted capability **(unverified)** |
| **Start with Windows** | `Run` value in `HKCU` (`StartupRegistration`) | Writes to `HKCU` are copied to a private registry: **the `Run` value would have no effect** | Declare `windows.startupTask` in the manifest and activate it with the `StartupTask` API (requires TFM `net10.0-windows10.0.19041` or higher). The user can remove it from Task Manager and the app can no longer reactivate it by itself: the checkbox text changes **(details unverified)** |
| **“Open with” for `.md`, `.markdown`, `.txt`** | Keys in `HKCU\Software\Classes` written by the installer | It goes in the manifest (`uap:FileTypeAssociation`), not in the registry | Declare it; the app already receives the path as an argument **(unverified that it can be only “Open with” without being the default)** |
| **New-version notices** (`UpdateNotifier`, `ReleaseUpdateChecker`) | Queries GitHub `releases/latest` | Updates are done by the Store every 24 h | Turn it off when the app runs packaged (detectable with `GetCurrentPackageFullName`) **(the Store policy probably requires it; unverified)** |
| **Linked files** | Reads and writes the user's `.md` | A full-trust app writes outside the package if the user has permission ([source](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes)) | None; requires declaring `runFullTrust` |
| Global shortcut, single instance (mutex), DPAPI, tray | Normal Win32 | Should work the same in full trust | Test |
| **Installation** | `%LOCALAPPDATA%\Programs\Aldune` (installer) or portable folder | Read-only `C:\Program Files\WindowsApps\<package>` | Check that nothing writes next to the `.exe` (logs already go to `AppData`) |
| Look, dock, notes, sync | — | — | None |

## What would have to be built

1. **`AppxManifest.xml`** (identity, `runFullTrust`, `startupTask`, file associations, logos). The identity (`Name` and `Publisher`) is given by
   Partner Center when reserving the name.
2. **Package images** (44, 150, 50 px…): they come from `scripts/branding/generar-iconos.py` (not touched up by hand, see `docs/BRANDING.md`).
3. **Packaging** with `makeappx.exe` (it is on this PC, Windows SDK 10.0.26100) over the folder with `aldune.exe`. The Store signs the package that
   is uploaded; to test it locally you need one signed with a test certificate and developer mode **(detail unverified)**.
4. **A new job in `release.yml`** that produces the `.msix` next to the installer. Uploading to the Store would be manual in Partner Center (or with
   the `msstore` CLI, which needs an application registered in Azure AD; optional).
5. **Store listing:** privacy policy (a public address), screenshots, descriptions in the 5 languages (texts already exist), age rating,
   and the declaration of the restricted capability `runFullTrust`.
6. **Code:** `StartupRegistration` and the file registration come to depend on whether there is a package identity; the version notice is turned off when packaged;
   and a setting to change the data folder (`ALDUNE_DATA_DIR`), which also allows testing without touching real notes.

## Risks

- **Uninstalling deletes a new Store user's notes** (see Data). It is the only risk that affects user data.
- **Two copies of Aldune** (the GitHub one and the Store one) share data folder, mutex and shortcut: they must not run at the same time. Today the mutex
  already prevents two instances, but it would have to be decided what happens to whoever installs the second over the first.
- **Store certification:** it takes days and can reject the package because of the `runFullTrust` capability or the version notice.
- **Maintenance:** every release also has to be uploaded to Partner Center.

## Effort (my estimate)

- Local prototype (manifest, packaging, startup, associations, data): **1 long session**.
- Final code, CI work and tests of the two ways of installing: **1 to 2 more sessions**.
- Store listing, privacy policy and certification: **calendar days**, little work of mine.

## Recommendation

It is worth it **only if it is going to be sold or if signing without paying for a certificate is wanted**. If the goal is only to avoid the SmartScreen notice, it is
cheaper to apply to SignPath Foundation again (free for open source) when the project has more visibility. Do not start the prototype without first
deciding the charging model (see `_notas\ALDUNE-MERCADO-Y-NEGOCIO.md`), because it conditions what is declared in the listing.

## Before starting a prototype (check)

- Whether `AppData` can be de-virtualized and with which capability (and whether the Store allows it without extra justification).
- How “Open with” ends up without becoming the default, and how `StartupTask` is handled from .NET 10 with TFM `windows10.0.19041`.
- Whether the Store policy allows own billing inside a free app and what commission (if any) Microsoft keeps.
- What exactly is required in an individual's listing (privacy, address, merchant status).

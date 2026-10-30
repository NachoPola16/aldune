# Aldune — MSIX package for the Microsoft Store (design spec)

Decided with the user on 2026-09-28, after SignPath Foundation rejected the free signing request
(see `STATUS.md`). Rule that stays: free paths only.

## Problem

The GitHub binaries are unsigned and SmartScreen warns about an "unknown publisher". The only free path
left is the Microsoft Store: individual developer registration is free and the Store signs the
MSIX packages with Microsoft's certificate. Uploading an `.exe`/`.msi` to the Store does not work: it requires the
installer to already come signed with a trusted certificate.

## Goal and limits

- Aldune can be installed from the Store, signed, without warnings.
- The GitHub download (installer and portable) does not change and stays unsigned.
- A single codebase: the app detects at run time whether it is running inside the package and adapts. No
  separate branch or project.
- x64 only, self-contained, as today.
- Out of scope: uploading to the Store automatically (submission API), ARM64, the Store listing
  (texts and screenshots; done in Partner Center).

## Decisions

### Build: hand-written manifest + `makeappx`

Ruled out: the Visual Studio packaging project (`.wapproj`: it needs VS's MSBuild and does not
build with `dotnet build`) and the Windows App SDK single-project MSIX (a whole dependency
just to package).

- `installer/msix/AppxManifest.xml`: template with placeholders for version and identity.
- `installer/msix/identity.json`: the three values given by Partner Center (`Name`, `Publisher`,
  `PublisherDisplayName`). Until the account exists it carries test values (`CN=Aldune Dev`); once
  it is there they are changed only here.
- `installer/msix/Assets/`: PNGs of the mandatory logos (Square44x44, StoreLogo 50x50,
  Square150x150, Wide310x150; scales 100 and 200; Square44x44 also `targetsize-*` without plate),
  generated once from `src/Aldune/Assets/aldune-logo*.svg` and kept in the repository so that
  the build does not depend on an SVG converter.
- `scripts/build-msix.ps1`: same self-contained x64 `dotnet publish` as `build-installer.ps1` (extracted
  into a common function if the code is repeated), copies publish + manifest + logos to a
  working folder, fills in the version and the identity, and runs `makeappx pack` →
  `dist/Aldune-<version>.msix`. It looks for `makeappx`/`signtool` in `Windows Kits\10\bin\<latest>\x64`.
- Package version: the one from the `.csproj` with a fourth number `0` (`1.4.1.0`); the Store requires it.
- `-Sign`: creates (or reuses) a self-signed certificate in `Cert:\CurrentUser\My` with the same
  `Publisher` as `identity.json` and signs the package to install it locally. To install it you have to
  trust that certificate (the machine's Trusted People store; the script explains it, it does not do it by itself
  because it needs administrator). It is uploaded to the Store unsigned: Microsoft signs it.
- Release workflow: generates the unsigned `.msix` and stores it as a **run artifact**, not
  as a file of the GitHub release (without a trusted signature it cannot be installed). The user uploads it by hand to
  Partner Center.

### Package detection

`Interop/PackageContext.cs`: `IsPackaged` = `GetCurrentPackageFullName` (kernel32, P/Invoke) returns
something other than `APPMODEL_ERROR_NO_PACKAGE`. It is computed once. No WinRT for this.

In Core, `DistributionChannel` (`GitHub` | `Store`) and `DistributionFeatures.For(channel)`: which
startup is used and whether the version check is offered. Small, but with a test, and the app does not scatter
`if (IsPackaged)` everywhere.

### Data: the same real folder

Decided by the user: the Store version uses `%LOCALAPPDATA%\Aldune`, the same as the GitHub one.
Whoever switches keeps their notes and uninstalling from the Store does not delete them (as with Inno Setup).

- Manifest: restricted capability `rescap:unvirtualizedResources` and, in `Properties`,
  `desktop6:FileSystemWriteVirtualization` and `desktop6:RegistryWriteVirtualization` set to `disabled`.
  The registry as well, to be able to delete the GitHub version's `Run` entry (below).
- `ResolveAppDataDirectory` does not change.
- The `AlduneSingleInstance` mutex is not isolated for full-trust packaged desktop apps:
  the two versions cannot run at the same time on the same folder. It is verified in the probe.
- Microsoft reviews restricted capabilities. Justification text prepared in
  `installer/msix/README.md`: the user's data must survive uninstalling and be the same
  as those of the downloadable version.
- **Plan B, not built now**: if it is rejected, the capability and properties are removed; the data goes to
  the package's private folder and the user must be warned (listing and Settings) that uninstalling deletes the notes.

### Start with Windows

The `Run` key is no use from the package: it would point to `C:\Program Files\WindowsApps\...`, which cannot
be launched directly. Inside the package the `desktop:StartupTask` extension is used
(`TaskId="AlduneStartup"`, `Enabled="false"`) and the `Windows.ApplicationModel.StartupTask` API.

- `StartupRegistration` becomes an interface with two implementations: `RunKeyStartup` (the current code)
  and `PackagedStartup`. `App` chooses according to `DistributionFeatures`.
- The API is WinRT: the `Aldune` project moves to `net10.0-windows10.0.19041.0` (Core does not change).
  Check that no new warnings appear and that the publish size does not change noticeably.
- `StartupTask` states: `Enabled`, `Disabled` (can be enabled; the first time Windows shows its own
  dialog), `DisabledByUser` (disabled in Task Manager: the app **cannot** re-enable
  it), `DisabledByPolicy`. In the last two the Settings checkbox is disabled with a
  text explaining where it is changed. New text with `Strings.T` in the five languages.
- The API is asynchronous: the checkbox is updated when it finishes, without blocking the UI thread.
- When enabling startup from the package, the GitHub version's `Run` entry is deleted if it exists
  (`AlduneStartup` and the legacy one): otherwise Windows would launch both at sign-in and the
  first would win at random.

### New-version notice

Inside the package GitHub is not queried (`UpdateNotifier` does not start) and the option does not appear in
Settings → About. The Store updates, and its rules do not allow updating through another channel. The
`CheckForUpdatesAutomatically` setting stays as it is in `settings.json` (the GitHub version uses it if
the user goes back to it).

### Data folder for tests

To test the package without touching the real notes another data folder is needed, and a packaged
app is not easily given arguments. `ALDUNE_DATA_DIR`: if it exists, `App` uses it
instead of `%LOCALAPPDATA%\Aldune` (without migrating the legacy folder). It is not documented for users.

## Tests

- Core (TDD): `DistributionFeatures.For` per channel; reading and validating `identity.json`
  (`Publisher` of the form `CN=...`, `Name` without spaces) and the conversion of the version to four numbers
  with the last one at 0. If the validation lives in the script, it is tested from Core with the same rule.
- Build: `build-msix.ps1 -Sign` produces a package that `Add-AppxPackage` installs.
- Probe (outside the repo, with a temporary `ALDUNE_DATA_DIR`): the app installed from the MSIX writes to the
  indicated folder and not to a virtualized one; enabling startup shows the Windows dialog and ends up
  `Enabled`; a test `Run` entry disappears; About does not show the version check;
  opening the GitHub version at the same time does not create a second instance; uninstalling leaves the data.
- Full `dotnet test` and smoke test unchanged (the GitHub version must not notice anything).

## What the user does

1. Install the Windows SDK (`winget install Microsoft.WindowsSDK.10.0.26100`).
2. Create the individual developer account in Partner Center (free, with identity
   verification) and reserve the name "Aldune".
3. Pass the three identity values (Product management → Product identity) for
   `identity.json`.
4. Upload the first `.msix`, with the `unvirtualizedResources` justification prepared.
5. Fill in the listing: description, screenshots, age rating and the privacy policy URL
   (the README's privacy paragraph, published on a page).

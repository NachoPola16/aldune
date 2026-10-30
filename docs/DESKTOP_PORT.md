# Aldune — macOS, Linux and web: is there a port? Study and proposal

> **Status: macOS, Linux and web are still undecided; the mobile client already has a decided plan**
> (Android first, on tablet: `MOBILE_PORT.md` §0.1). In none of the cases is there any code written.
> Date: 2026-09-28. Sibling document of
> [`MOBILE_PORT.md`](MOBILE_PORT.md) (Android and iOS). That one answers "what about the phone?"; this one answers
> "what about the Mac, Linux and the browser?".
>
> Reading rule: this document deliberately separates **what has been verified** (with a link) from **what has to be
> checked in a spike**. Not a single line of code has been written for any of these systems.

## 0. Summary for those in a hurry

| System | Do we do it? | Why |
|---|---|---|
| **Windows** | Already done | It is the product: 1.4.1, edge dock, installer and MSIX on the way |
| **Windows on ARM64** | **Yes, and it is cheap** (1 session) | Today only `win-x64` is published; the installer **does** accept ARM64 (`x64compatible` includes Windows 11 ARM), but it runs **emulated**. One more RID adds no maintenance: §9.1 |
| **Server on ARM** (Raspberry Pi, NAS) | Already works | The `Dockerfile` starts from a multi-architecture image and builds on the target: §9.2 |
| **Android** | **Yes, first: decided on 2026-09-28** | It is where the port makes sense and the whole of Core is reused (see `MOBILE_PORT.md` §0.1: no Mac, no licenses, tablet as the reference) |
| **iOS** | Yes, after Android | Same code, another toolchain and an extension in Swift |
| **macOS** | **No, for now** | It is the only system where **the concept already exists and is taken** (the apps that inspired Aldune) and, on top of that, it is where Aldune's mechanics (windows clipped to the edge) would have to be redone in a different way |
| **Linux** | **Not as an app**; yes as a server | The edge dock is not portable to Wayland without being a desktop extension (GNOME does not support `wlr-layer-shell`); and the `SyncServer` already runs on Linux, which is what a Linux user needs to sync |
| **Web / WASM** | **No** | No widgets, no reliable notifications, and the text would end up in the browser. Already ruled out in `MOBILE_PORT.md` §11 |
| **ChromeOS** | Covered by Android | ChromeOS runs Play Store apps, so the Android client (§`MOBILE_PORT.md`) works; and its Linux (Crostini) is the Linux case from §3 |

**What we should do for the missing systems, and it is cheap:** leave the **Core ready for any client** (the
so-called "seams", `MOBILE_PORT.md` §5). With that, the day macOS or Linux becomes interesting, the logic does not
need to be touched, only the interface has to be written. This is the part that is recommended to do
**once and soon**, because the mobile port needs it just the same.

## 1. What is already cross-platform today, without touching anything

| Piece | Status | Note |
|---|---|---|
| `Aldune.Core` (69 files, 6,663 lines, `net10.0`) | Portable except **one** file | The only Windows-specific piece is DPAPI (`DatabaseKeyProvider`) |
| Sync format (JSON + AES-256-GCM, format 4) | Portable by design | `SYNC.md` says so explicitly: "do not depend on Windows, WPF, SQLite or DPAPI" |
| `Aldune.SyncServer` (ASP.NET Core minimal API) | **Already runs on Linux** | It is the `docker-compose.sync.yml` documented in `SYNC.md` |
| `tests/Aldune.Core.Tests` (850) | Portable | Valid for any client that links Core |
| UI: `src/Aldune` (WPF, 15,829 lines) | **Not portable** | It is the only thing that would have to be rewritten for each system |

That is: **Aldune already has the brain and the server cross-platform; what it lacks is a body.** This whole
document is about how much it costs to give a body to macOS, Linux or web, and whether it is worth it.

## 2. macOS: the system where the concept **already exists**

### 2.1 The problem is not technical, it is about the market

Aldune's concept was born on macOS and is still there (`ROADMAP.md` §1): the apps that inspired it already cover it. Aldune was made
precisely to bring it to Windows, where it did not exist.

Porting Aldune to macOS reverses that argument: it would enter as the **fourth** in a market with three players,
one of them free and with its code in the open, and with the app that inspired the design. On Windows the gap
is empty; on macOS it is full.

### 2.2 The technical side: the dock mechanics would have to be redone entirely

The central piece of Windows 1.0 is that **the dock window is never resized**: it is measured once
and the only thing that changes per frame is the **clipped region** (`SetWindowRgn` + `CombineRgn`). On macOS
that concept does not exist: `NSWindow` is rectangular. Each piece has an equivalent, but it is different code:

| On Windows | On macOS | Status |
|---|---|---|
| `SetWindowRgn` + `CombineRgn` (clipping per frame) | Borderless window (`borderless`) + `isOpaque = false` + **`CALayer` mask** over the content view | Standard technique, verified; **to be checked**: per-frame flicker and that the system shadow (which follows the rectangular shape) does not look bad |
| `WS_EX_TOPMOST` / "always on top" | `NSWindow.level = .floating` / `.statusBar`, plus `collectionBehavior` (`.canJoinAllSpaces`, `.fullScreenAuxiliary`) so it does not disappear in full screen and in other spaces | Verified; the equivalent of Windows' game detection is this |
| Open the dock without stealing focus | `NSPanel` with `nonactivatingPanel` + agent app (`LSUIElement`, no Dock icon) | Verified as a common pattern (panel apps and menu bar utilities) |
| Tray (`NotifyIcon`) | `NSStatusItem` in the menu bar | Verified |
| Start at sign-in (`Run` key) | `SMAppService` (macOS 13+) | Verified |
| Global shortcut `Ctrl+Alt+H` | **`RegisterEventHotKey`** (Carbon, deprecated but functional, **no permissions**); the "modern" way, `NSEvent.addGlobalMonitorForEvents`, **requires the Accessibility permission** | Verified; it is a detail that decides the experience |
| Cursor polling for hover | `NSTrackingArea` | Verified; the Windows polling would have to be rewritten |
| Several screens and `MonitorInfo.StableId` | `NSScreen` + `CGDisplayCreateUUIDFromDisplayID` (the UUID is the natural stable id) | Verified |
| Own *toast* notifications | `UNUserNotificationCenter` | **Will be written anyway** if the iOS mobile port is done |
| Inno Setup / MSIX installer | DMG + **mandatory notarization** to distribute outside the App Store; or sandbox + Mac App Store | Verified ([notarization](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)) |
| `Microsoft.Data.Sqlite`, AES-GCM, PBKDF2 | Supported on macOS | Verified (`cross-platform-cryptography`); macOS has had AES-GCM since before iOS |

Two practical consequences:

- **The money and the Mac are already paid for** if the iOS mobile port is done: the Apple account ($99/year),
  the Mac and Xcode serve for macOS. The cost of macOS is **work, not licenses**.
- **The expensive part is exactly what others have already done.** The dock clipped to the edge, with per-frame
  animation and several monitors, is weeks of work on Windows; replicating it in AppKit means starting from scratch
  on the most fragile part of the product to enter the most competitive market.

### 2.3 The only realistic way to have *the* Aldune on macOS

**XPF** (Avalonia's commercial product) runs unmodified WPF applications on macOS and Linux. It is
the only way to bring *the same* program with its dock, instead of writing another one. Two reservations, and
they are big: it is paid and we have to **check what XPF does with the P/Invokes to `user32`/`gdi32`/`dwmapi`**
that are the heart of the dock mechanics (regions, shadows, `WS_EX_NOACTIVATE`, mouse hooks). If
XPF does not map those calls, the value drops and what is left is just "a WPF app that starts on macOS", which for this
product is little. **Before considering it, that specific point has to be tested.**

## 3. Linux: the **server** can be done, not the **dock**

### 3.1 What is portable and what is not

- **Sync server: already works on Linux.** It is the `docker-compose.sync.yml` from `SYNC.md`. For a
  Linux user, half of Aldune (the encrypted store, no account and self-hosted) **is already done**.
- **The edge dock: it is not portable without ceasing to be a normal application.** It is the problem of the
  previous chapter, made worse by Wayland.

### 3.2 Wayland, in a table (this is what decides)

A surface stuck to the edge with space reservation (what on Windows is achieved with the window region and on X11
with `_NET_WM_STRUT_PARTIAL`) is done on Wayland with the **`wlr-layer-shell`** protocol.
And that is where the problem lies: not all compositors implement it.

| Compositor | `wlr-layer-shell` | Consequence |
|---|---|---|
| **KWin / KDE Plasma 6.7** | **Yes** (version 5) | An edge panel is possible, but as a *desktop layer*: it behaves and is painted like a Plasma panel, not like an app |
| **Mutter / GNOME 51** | **No** | On GNOME (the most used desktop) a normal app **cannot** reserve the edge or place itself at absolute coordinates. It can only be done as a **GNOME Shell extension** (JavaScript) |
| Sway, Hyprland, niri, COSMIC, Muffin, Mir, Labwc, Wayfire… | Yes | As many variants as compositors, each with its own rules |
| Weston, Cage | No | — |

Source: support table of [`wlr-layer-shell-unstable-v1`](https://wayland.app/protocols/wlr-layer-shell-unstable-v1)
(Wayland Explorer, 2026-09-28). Furthermore, under **XWayland** (an X11 app on a Wayland desktop) you can
neither place the window nor reserve the edge: the classic `_NET_WM_STRUT_PARTIAL` is ignored.

**Translation:** on Windows, Aldune is *an application*; on Linux, to do the same, it would have to be
**a desktop extension for each desktop** (GNOME Shell in JavaScript, a plasmoid in QML for
KDE…), with no room left for the other variants. It is another product, with another language and another distribution.

### 3.3 What else changes

- **Three or four ways to distribute** (`.deb`, `.rpm`, AppImage, Flatpak, Snap) and none of them "the" way.
  With **Flatpak**, moreover, the sandbox complicates two things that Aldune uses: a global shortcut (portal
  `GlobalShortcuts`, uneven support) and permanent access to a sync folder.
- **Tray**: `StatusNotifierItem`; on GNOME an extension is needed to see it.
- **Per-monitor fractional scaling** on Wayland: pixel-level geometry, which on Windows already cost
  a fair amount (`DpiConversion`, `EdgeGeometry`), becomes a problem again with different rules.
- **Market**: Linux users expect free and gratis software. What they value in Aldune —local,
  encrypted, no account, self-hosted— fits their culture perfectly, but **not a price**.

### 3.4 What can be given to them, without writing an app

1. **The server**: it is already there. Keep it documented and with the image updated on every release (which is
   what is already done).
2. **The format**: it is already documented as readable by any future client (`SYNC.md`,
   "Future compatibility"). Anyone can write a client.
3. **A desktop client without a dock**, if it is ever made: the same one that would serve for macOS
   (§4.1), and with the shared Core.

## 4. Web / WASM: ruled out, with an argument that admits no way back

- A web client would need the **decryption key in the browser**: either it is served from a third-party website
  (it breaks the "no cloud, no account" promise) or it is self-hosted (one more service to maintain,
  which serves the application and sees the text in the tab).
- **There is no possible "viewer" without a key**: the sync store only contains encrypted envelopes, so a web
  view of the server would show nothing. The only real use would be a full client, with everything
  above.
- On mobile, a PWA has no widgets or reliable notifications: precisely the two things that make the
  pocket client useful.
- And on the desktop, the browser cannot be an edge panel reliably.

It is also ruled out as a support tool ("show me my store"): without a key it shows nothing,
and with a key it is a client.

## 5. What I propose to do (and what not)

| Priority | What | Why | When |
|---|---|---|---|
| **1** | **Core without Windows** (the "seams": `ISecretProtector`, `async` sync, `JsonSerializerContext`) | It is the same work the mobile needs, and it leaves the brain ready for any client | With mobile, `MOBILE_PORT.md` §5 phase 1 |
| **2** | **Android mobile client and then iOS** | It is where there is real use and a market gap | `MOBILE_PORT.md` |
| **3** | **Desktop client *without a dock* for macOS and Linux** (Avalonia), sharing Core and the presentation layer (`Aldune.Mobile.Core`) | Gives "the notebook also on the Mac" without rewriting the dock mechanics and without entering head-on into macOS's occupied market | Only if someone asks for it and uses it |
| 4 | Edge dock on macOS (AppKit, or XPF) | Very expensive and in the most competitive market; XPF has the P/Invokes still to verify (§2.3) | **No, for now** |
| 5 | Edge dock on Linux (GNOME/KDE extension) | Another product, another language, and a published extension has to be maintained that breaks with every desktop version | **No** |
| 6 | Web client | §4 | **No** |

Three things that must **not** be done along the way: splitting Core into a NuGet package (the monorepo is enough),
duplicating the texts (`Strings.cs` is linked, not copied) and porting the note theme editor.

## 6. Estimate (sessions, like those of this project)

| Work | Sessions | Note |
|---|---|---|
| Core without Windows | 3 | Already counted in the mobile plan; not added |
| Desktop client without a dock in Avalonia, macOS + Linux, reusing Core and the presentation | 5-7 | It is the cheap path to macOS/Linux |
| Packaging: DMG + signing + notarization (macOS); AppImage/Flatpak + `.deb` (Linux) | 1-2 | The Apple account and the Mac are already needed for iOS |
| Edge dock on macOS with AppKit | 6-10 | High uncertainty: regions, shadows, hover, multi-monitor. **Not recommended** |
| Edge dock on Linux as a desktop extension | 3-5 **per desktop** | Plus the permanent maintenance of the extension. **Not recommended** |

## 7. Open decisions

1. **Are macOS and Linux really wanted or are they a curiosity?** Everything depends on the answer: the client without a
   dock only makes sense if someone is going to use it.
2. **Avalonia or Mac Catalyst.** If mobile is done with MAUI, Mac Catalyst "comes almost for free", but it is
   iOS adapted to the desktop: multiple windows, agent panels and global shortcuts fit badly.
   Recommendation: **MAUI for mobile, Avalonia for the desktop**.
3. **XPF?** Only if what it does with the dock's P/Invokes is tested first (§2.3).
4. **The client without a dock on Windows too** (a "list view" for tablets and small screens)?
   It would be a new Windows feature: it goes to `ROADMAP.md` §2, not here.

## 8. How to pick this up again, and sources

Before working on this, **revalidate**: the Wayland table (§3.2, compositors change with every
version), the state of XPF and Apple's requirements. Everything in this document is from **2026-09-28**.

- Original concept and inspiration: `ROADMAP.md` §1.
- [`wlr-layer-shell` support per compositor](https://wayland.app/protocols/wlr-layer-shell-unstable-v1)
- [Notarizing macOS software](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)
- [Apple's upcoming requirements](https://developer.apple.com/news/upcoming-requirements/)
- [Avalonia](https://avaloniaui.net/) (and its XPF product, which runs unmodified WPF apps on macOS and
  Linux)
- [Cross-platform cryptography in .NET](https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography)
  (SQLite, AES-GCM and PBKDF2 also on macOS and Linux)
- The platform links of the mobile client are in [`MOBILE_PORT.md`](MOBILE_PORT.md) → "Sources".

## 9. Windows on ARM64 and the server on ARM: the two truly "missing systems"

They are the only systems that exist today and are not served, and they are **cheap**: they add no maintenance
surface because there is no new code, only another build target.

### 9.1 Windows on ARM64: it works today, but emulated

What there is, verified:

- The publish is **only `win-x64`**: `src/Aldune/Properties/PublishProfiles/portable.pubxml` (line 23),
  `scripts/build-installer.ps1` (line 17 and the zip name, line 52) and
  `.github/workflows/release.yml` (lines 34 and 45).
- The installer declares `ArchitecturesAllowed=x64compatible` and
  `ArchitecturesInstallIn64BitMode=x64compatible` (`installer/Aldune.iss`, lines 21-22). According to the official
  Inno Setup definition, **`x64compatible` includes ARM64 systems with Windows 11**, which
  can run x64 binaries through emulation ([Inno
  Setup architecture identifiers](https://jrsoftware.org/ishelp/topic_archidentifiers.htm)). That is: on a Copilot+ or a
  Snapdragon X, **Aldune already installs and works**, but emulated: more battery drain and more cost in the drawing
  part, which is precisely the one most noticeable in an animated dock.
- The version notice (`ReleaseUpdateChecker`) **does not choose a file by name**: it only reads the `tag_name`
  of the release, so on that side there is nothing to change.

A native ARM64 would be, in short:

1. Also publish `-r win-arm64`. WPF, the .NET desktop runtime and `Microsoft.Data.Sqlite`
   (through SQLitePCLRaw) have ARM64; there are no dependencies of our own that prevent it.
2. Add `arm64` to `ArchitecturesAllowed`/`ArchitecturesInstallIn64BitMode` and put both sets of
   files in the same installer, deciding by architecture (`IsArm64`), or publish two installers.
   The portable zip, with the name per architecture (`aldune-portable-win-arm64.zip`), and the `release.yml`
   with a matrix of two RIDs.
3. Verify on a real ARM machine or in a Windows 11 ARM VM. To build in CI there are
   **`windows-11-arm`** runners (GitHub Actions offers them for **public repositories**; the image with Windows
   11 ARM and Visual Studio 2026 arrived in September 2026
   ([changelog](https://github.blog/changelog/2025-04-02-arm64-windows-runners-for-public-repositories-in-public-preview/),
   [image issue](https://github.com/actions/partner-runner-images/issues/14602))), but the UI smoke
   test is done on a device all the same.

**Recommendation:** do it **when someone asks for it** or when there is an ARM machine to test it on. It is one of the
few extensions that add no debt, and it prevents a Copilot+ user from thinking that "the app is acting weird".

### 9.2 The sync server on ARM (Raspberry Pi, NAS)

**It already works and nothing needs to be done**: the `Dockerfile` starts from
`mcr.microsoft.com/dotnet/aspnet:10.0`, which is a **multi-architecture image** (linux/amd64 and
linux/arm64), and `docker compose ... up --build` builds on the machine where it runs. That is, Aldune's
encrypted store can be self-hosted today on a Raspberry Pi or an ARM NAS.

The only thing that is **not** done is **publishing a prebuilt image** for both architectures
(`docker buildx build --platform linux/amd64,linux/arm64`). It only matters if one day the image is distributed
instead of the code (for example, for those who do not want to build). It is a distribution decision, not a
compatibility one.
## 10. Linux, again (2026-10-29): which dock is possible depending on the desktop

Short review, only of whether **any** way of an edge dock on Linux exists. Conclusion: it depends on the desktop, and
the cheap test is a dock-type window with Avalonia on top of `Aldune.Core`.

| Desktop | Edge dock? | How |
|---|---|---|
| **X11** (also GNOME or KDE on X11) | Yes | Frameless window with `_NET_WM_WINDOW_TYPE_DOCK` and `_NET_WM_STRUT_PARTIAL` (reserves the edge); always on top with `_NET_WM_STATE_ABOVE`. |
| **KDE Plasma, Sway, Hyprland, COSMIC** (Wayland) | Yes | `wlr-layer-shell` protocol: the window is a *layer surface* anchored to an edge. It is implemented by wlroots compositors, KWin and COSMIC. |
| **GNOME** (Wayland, the default on Ubuntu and Fedora) | No | Mutter does not implement `wlr-layer-shell` and Wayland does not let a normal window be always on top or stick to an edge. Only a normal window or a tray icon with a list of notes fits. |

**What I do not know and would have to be checked with a one-day test:** whether Avalonia exposes `wlr-layer-shell` (I did not find it;
`gtk-layer-shell` and `qt-layer-shell` exist for GTK and Qt, none for .NET) or whether it would have to be done with P/Invoke to
`libwayland`/`gtk-layer-shell`. On X11 Avalonia already allows frameless and always-on-top windows; the *struts* have to be
set through P/Invoke to Xlib.

**Realistic way to deliver it:** an Avalonia client with two modes. *Dock mode* where possible (X11, KDE, Sway,
Hyprland) and *list mode* (normal window plus tray) on GNOME-Wayland. `Aldune.Core` and the sync format are reused
as they are; the work is in the window layer and in the packaging (AppImage, Flatpak, .deb). The encryption of the
database key has to be changed: today it uses DPAPI (Windows only), and on Linux it would go to the keyring (libsecret).

**Recommendation unchanged:** it does not go into 1.8.0. If it is done, start with the test of the dock-type window on X11 and
KDE before committing.

Sources: [Avalonia, Wayland support](https://avaloniaui.net/blog/bringing-wayland-support-to-avalonia) (several
backends per desktop, layer-shell not confirmed), [gtk-layer-shell](https://github.com/wmww/gtk-layer-shell) (does not
work on GNOME-Wayland), [proposal for "always on top" on Wayland](https://gitlab.freedesktop.org/wayland/wayland/-/issues/305).

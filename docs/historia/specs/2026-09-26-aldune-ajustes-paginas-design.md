# Aldune — Settings with a side menu and a simplified mode with the essentials (design spec)

Decided with the user on 2026-09-26 ("do whatever you think best") after measuring the window with a probe.

## Problem

- Settings measured 920 × 2150 px with everything open. On a 1440 px-tall screen it was cut off at
  1296 and you had to scroll for almost everything; the left column was twice as tall as the right one.
- Simplified mode hid **all** the settings (only the mode button, help and the version were left),
  even though its text said it "leaves the essentials".

## Decisions

- **Do not widen the window or depend on the screen**: a side menu with pages, a fixed size of 900 × 700
  capped against the monitor's work area (90 %). Each page fits without scrolling on a normal screen; on
  a small one, each page has its own scroll.
- **Pages**: General (interface mode, startup, shortcut, language) · Notes and tasks (remember
  position, tasks, trash) · Dock and screens (screen and edge side by side, full screen,
  keep open, trackpad) · Colors · Sync · Help · About (version, check for
  updates, restart, quit).
- **The note manager does not go into Settings**: it is used daily, Settings rarely. A
  "Manage notes" button stays at the foot of the side menu (the manager already had one towards Settings).
- **Settings from the dock**: it is already reached with the right click on its buttons; no visible
  button is added. Right-clicking "Sync" opens the Sync page directly
  (`AppCoordinator.OpenSettings(page)`).
- **Simplified mode**: General, Dock and screens (only screen and edge), Colors (only the theme),
  Help and About. It hides Notes and tasks, Sync and the advanced parts of the dock and the colors.
  The default values of what is hidden do not change: they are already good enough to start with.
- The last page viewed is remembered during the session.

## Verification

A probe with the real window in both modes: content height of each page versus the visible height, and
a capture of each one. Same control and handler names as before (checked by comparing the XAML).

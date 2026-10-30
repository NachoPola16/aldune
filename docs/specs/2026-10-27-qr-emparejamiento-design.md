# Pairing by QR (Windows shows it, Android scans it)

Date: 27-10-2026. Status: **implemented** (the execution plan was removed from the tree). Replaces the "Not done for now" decision from
`docs/DECISIONS-2026-10-26.md`, which was postponed because of the density of the code; with what was measured below there is no need to split it.

## What for

Join a phone to a sync profile without pasting the code: on the PC you press "Show QR", on the phone "Scan QR", and the code field
is filled in the same way as with "Paste". The token **never** goes in the QR (decision of 26-10): it is still typed separately.

## What stays

- The profile code is the usual one (`aldune-profile-v2:…`): the QR is just another way of carrying it. Zero sync format changes.
- The phone still does not create or share profiles: it only joins.
- Android only fills in the field; `parseProfileInput` and "Continue" validate as today.

## Security

- The code carries the profile's **encryption key**. A QR on screen has the same risk as copying the code: whoever sees or photographs it
  can read those notes. That is why:
  - The Windows window warns about it ("Show it only to your own devices") and **closes by itself after 2 minutes**, with a visible countdown.
  - It is not saved or written to disk; it does not go to the clipboard (the usual "Copy code" button is separate and explicit).
  - "Revoke access" remains the way out if someone saw it.
- Android: no frames are saved or sent anywhere; the camera is opened only on the scanner screen and released on exit.
  Only a text containing `aldune-profile-v2:` is accepted (`ProfileCodes.findShareCode`); any other QR is ignored with a notice.
  The camera permission is requested when pressing "Scan QR", not at startup, and `uses-feature camera` is `required=false`.

## Size

Measured: "all notes" ≈ 250 characters; with chosen notes, ≈ 50 more per note. A QR with error correction level M holds about 2,300 bytes, so
hundreds of notes fit. If it still does not fit (`DataTooLongException`), Windows says "This profile is too large for a QR" and lets the code
be copied as today. It is not split into several QRs.

## Windows

- New dependency: **QRCoder** (MIT, pure C#, version pinned in the `.csproj`). It is only used for the module matrix.
- Own painting in WPF (black rectangles on white with a quiet zone of 4 modules): sharp at any size and without
  `System.Drawing`. Always black and white, also in the dark aspects: a light QR on dark is not read by all readers.
- `ShareQrWindow` (modal, same style as `AppDialog`): the QR, the notice, the countdown and "Close". It is opened from a new "Show QR" button next
  to "Share profile" in Settings → Sync. Texts with `Strings.T` in the five languages.
- Pure logic with a test in Core: `ShareQr.Build(code)` → module matrix or "too large" (the test fixes capacity, a stable matrix for a
  given code and the limit).

## Android

- **CameraX** (Android's current camera; not the old Camera1 API) + **ZXing `core`** (pure Java decoder, Apache-2.0; no Google
  Play Services, no ML Kit and no native binaries, so F-Droid and IzzyOnDroid can build it). Ruled out: `zxing-android-embedded`
  (maintenance mode and Camera1), Google Code Scanner and ML Kit (proprietary, no F-Droid) and `zxing-cpp` (native; it stays as an improvement if
  scanning turns out weak in real tests).
- `QrScanScreen` (Compose + `PreviewView`): `ImageAnalysis` with `STRATEGY_KEEP_ONLY_LATEST`, decodes the luminance plane with
  `MultiFormatReader` restricted to QR, and on the first valid code returns the text and closes.
- "Scan QR" button next to "Paste" in "Add profile"; its result goes through the same path as "Paste" (`ProfileCodes.findShareCode`).
- Pure logic with a test on the JVM: `QrDecoder.decodeText(luma, rowStride, width, height)` (ZXing core; normal QR, inverted and with CameraX stride). The "profile codes only" filter already exists: `ProfileCodes.findShareCode`.

## Out of scope

Generating the QR from Android, scanning other types of QR, saving the QR as an image, and multi-part QRs.

## Tests

- Core and JVM: as indicated above, with test first.
- By hand: the PC shows the QR and the Pixel scans it (real camera, no screenshots of its home screen); QR too large; camera permission
  denied (the screen explains it and offers "Paste"); any other QR (it is ignored).
- `seguridad` before publishing: Windows window, camera lifecycle and that the code does not end up in logs.

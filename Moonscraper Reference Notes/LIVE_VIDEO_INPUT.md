# Live Video Input

Use a live capture device — webcam, USB HDMI interface, OBS Virtual Camera, etc. — as the gameplay background instead of a venue file.

---

## Setup

1. Connect your capture device **before launching YARG**
2. Open **Settings → Gameplay** and scroll to the **Venues** section
3. Set **Live Video Device** to your device from the dropdown
   - Leave it empty (first entry) to automatically use the first available device
   - Partial name matching is supported — you don't need the exact device name
4. Enable **Live Video Input**
5. Play any song — the capture feed fills the background for that session and all future sessions until you turn it off

---

## Behavior

| Situation | Result |
|---|---|
| Toggle is **off** | Normal venue behavior — yargrounds, video files, per-song backgrounds all work as usual |
| Toggle is **on** | Capture feed overrides **all** other background sources entirely |
| Selected device not found at launch | Falls back to the first available device |
| No capture devices found at all | Background is black; a warning is logged |
| Device plugged in while game is running | Won't appear in the dropdown until the game is restarted |

---

## Opacity

The **Song Background Opacity** slider (Settings → Graphics) still applies. It controls a black overlay on top of the feed — reducing it dims the capture to black.

---

## Compatible Devices

Anything Windows exposes as a DirectShow video capture device works:

- USB webcams
- USB HDMI/SDI capture cards (Elgato, AVerMedia, Magewell, etc.)
- OBS Virtual Camera
- Any other virtual or physical capture device

---

## Implementation Notes

- Setting is persisted to YARG's standard settings JSON — configure once, applies to every session
- Device list is populated from `WebCamTexture.devices` at settings load time
- `WebCamTexture` is started in `BackgroundManager.LoadLiveVideoBackground()` and stopped/destroyed on scene unload via `Dispose()`
- Renders directly to the `_backgroundImage` RawImage in the Gameplay scene — the same layer used by static image venues
- `VenueLoader.GetVenue()` is bypassed entirely when live input is active

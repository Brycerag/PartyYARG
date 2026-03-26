# PartyYARG Show Modes

PartyYARG is designed to operate in two fundamentally different show configurations. Both are supported by the same codebase; the difference is in which direction signals flow and who holds the master clock.

---

## Mode 1 — DAW-Led (Synchronized / Slave)

> *"We've built our show in the DAW. The game is a live visual layer that follows it."*

### How it works

The production team designs the full show in a DAW (Ableton Live, QLab, etc.) or lighting console beforehand. The DAW is the **master clock and show controller**. PartyYARG is a **synchronized slave** — it receives cue commands from the DAW and advances the show in response.

The band follows the DAW's click track. The operator runs the show from the FOH position using their pre-programmed show file.

### Signal flow

```
DAW / Lighting Console
        │
        │  OSC, TCP, or MIDI notes         (inbound to game)
        ▼
 PartyHeroController
  ├── OscReceiver      (listens on UDP port)
  ├── TcpCommandServer (listens for text commands)
  └── MidiInputController (listens for MIDI notes)
        │
        ▼
 SetlistManager.Advance()  →  Scene transition
```

### Inbound commands used

| Signal | Command | Meaning |
|---|---|---|
| TCP/OSC | `BAND_READY` / `/partyhero/band_ready` | Band is ready, proceed from ReadyUp |
| TCP/OSC | `FORCE_STATE {name}` / `/partyhero/force_state` | Jump to a specific state (e.g. skip to next song) |
| MIDI | Configurable note | Band ready trigger |
| MIDI | Configurable note | Force-next trigger |

### When to choose this mode

- You have an existing Ableton/QLab show file and want to graft a live rhythm game element into it
- The production team, not the band, is in creative control of timing
- You need frame-accurate sync with backing tracks, video, or automated lighting
- The venue has a dedicated show-caller or stage manager at a console

### Settings profile

In `partyhero_settings.json`: enable MIDI/OSC/TCP **input** for the commands the DAW will send. Disable OSC output (`OscOutputEnabled: false`, `MidiOutputEnabled: false`) unless you want confirmation acks.

---

## Mode 2 — Band-Led (Full Product / Master)

> *"PartyYARG IS the show. Audio, lights, game — the band drives all of it."*

### How it works

PartyYARG is the **master show controller**. The band triggers state changes (song ready, advance, etc.) via a MIDI foot pedal, on-stage OSC button pad, or a stage manager's TCP controller. PartyYARG then drives everything outward: it advances its own scenes, tells the DAW when to fire backing tracks, and drives the lighting rig via sACN/DMX directly from the chart's venue track.

The band is in control. The show adapts to them, not the other way around.

### Signal flow

```
Band / Stage Manager
        │
        │  MIDI foot pedal / OSC pad / TCP button
        ▼
 PartyHeroController     (same input layer as Mode 1)
        │
        ▼
 SetlistManager          (show state machine)
        │
        ├──▶ SceneIndex transitions  (ReadyUp → Gameplay → Score → …)
        │
        ├──▶ DawBridge (outbound)
        │      ├── OscSender   →  DAW fires backing track cue
        │      └── MidiOutputController → lighting console / click track cue
        │
        └──▶ MasterLightingController (via chart venue track during Gameplay)
               ├── StageKitInterpreter  →  Stage Kit USB device
               └── SacnInterpreter      →  sACN/DMX-over-Ethernet  →  real stage lights
```

### Outbound messages used (DawBridge)

| Event | OSC address | MIDI note (default) |
|---|---|---|
| Show starts | `/partyyarg/show/start {name}` | 48 (C3) |
| Song ready-up | `/partyyarg/song/readyup {name} {artist} {index}` | 50 (D3) |
| Song starts | `/partyyarg/song/start {name} {artist} {index}` | 51 (D#3) |
| Song ends | `/partyyarg/song/end {name} {artist} {index}` | 52 (E3) |
| Set break starts | `/partyyarg/break/start {label} {duration}` | 55 (G3) |
| Player swap starts | `/partyyarg/swap/start {label}` | 56 (G#3) |
| Show ends | `/partyyarg/show/end` | 49 (C#3) |

### Lighting (via chart venue track)

Each chart can embed a venue/lighting track that drives:
- Lighting cues (Verse, Chorus, Frenzy, BigRockEnding, strobes, silhouettes, etc.)
- Post-processing effects (Bloom, Trails, Sepia, etc.)
- Fog machine on/off
- Performer spotlight / singalong flags

These fire automatically during gameplay through `MasterLightingController` → `SacnInterpreter` → sACN multicast → any DMX universe on the network (QLC+, MA3, LightJams, hardware USB-DMX dongles).

### When to choose this mode

- You want an all-in-one show system the band can run themselves, minimising crew
- The setlist is flexible night-to-night and the band may change order on the fly
- You want the chart's lighting track to drive your real rig
- You are building a touring/rental product that venues can drop in without a dedicated operator

### Settings profile

In `partyhero_settings.json`: enable OSC/MIDI/TCP **input** for band-side triggers. Enable `OscOutputEnabled: true` and/or `MidiOutputEnabled: true` for the DawBridge outbound cues. Configure sACN output in YARG's main settings for light control.

---

## Coexistence — both at once

The two modes are not mutually exclusive. The full integration layer is always active; it is just a question of which direction is doing meaningful work on a given night.

Example: an advanced setup might use Mode 2 for lights (chart → sACN → rig) while using Mode 1 for audio (DAW fires the backing track and sends the band-ready signal back to the game when the intro count-in finishes).

```
DAW fires count-in  ──MIDI──▶  MidiInputController
                                      │
                               OnBandReady event
                                      │
                               ReadyUpMenu auto-starts
                                      │
                    ◀─OSC──  DawBridge: /partyyarg/song/start
                    (DAW locks backing audio fader for the song)

During gameplay:
Chart venue track  ──▶  MasterLightingController  ──▶  sACN  ──▶  real lights
```

---

## Architecture reference

| Component | File | Role |
|---|---|---|
| `PartyHeroController` | `Integration/PartyHero/PartyHeroController.cs` | Input hub (MIDI in, OSC in, TCP in) |
| `PartyHeroSettings` | `Integration/PartyHero/PartyHeroSettings.cs` | All configuration, both modes |
| `MidiInputController` | `Integration/PartyHero/MidiInputController.cs` | WinMM MIDI input |
| `OscReceiver` | `Integration/PartyHero/OscReceiver.cs` | UDP OSC input |
| `TcpCommandServer` | `Integration/PartyHero/TcpCommandServer.cs` | TCP text command input |
| `SetlistManager` | `Integration/PartyHero/SetlistManager.cs` | Show state machine |
| `ShowSetlist` / `ShowSetlistLoader` | `Integration/PartyHero/ShowSetlist.cs` | Setlist data model + JSON loader |
| `DawBridge` | `Integration/PartyHero/DawBridge.cs` | Outbound OSC + MIDI to DAW |
| `OscSender` | `Integration/PartyHero/OscSender.cs` | UDP OSC output |
| `MidiOutputController` | `Integration/PartyHero/MidiOutputController.cs` | WinMM MIDI output |
| `MasterLightingController` | `Integration/MasterLightingController.cs` | Lighting state hub (from chart) |
| `SacnInterpreter` + `SacnHardware` | `Integration/Sacn/` | DMX via sACN/Ethernet |
| `StageKitInterpreter` + `StageKitHardware` | `Integration/StageKit/` | Rock Band Stage Kit USB |

---

## Open questions / future work

- **Mode 1 ack messages**: Should PartyYARG send a TCP/OSC ack back to the DAW confirming it received a cue (e.g. `READY_ACK`)? Useful for tightly-timed shows.
- **Mode 2 panic/hold**: An operator `FORCE_STATE HOLD` command that suppresses all outbound cues and freezes scene transitions for emergency situations (mic drop, power issue).
- **Timecode sync (LTC/MTC)**: Mode 1 could potentially lock to SMPTE timecode from the DAW rather than event messages, providing frame-accurate sync without relying on network round-trip timing.
- **Setlist editor UI**: A web or native UI for building Mode 2 setlist JSON files (`ShowSetlist`) without editing JSON by hand.

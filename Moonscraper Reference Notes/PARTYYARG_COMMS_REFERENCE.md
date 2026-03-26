# PartyYARG — Complete Communications Reference

All ports, addresses, notes, and arguments as of current build.
All values are the **defaults** — everything is configurable in `partyhero_settings.json`.

---

## Ports

| Transport | Direction | Default Port | Protocol |
|---|---|---|---|
| OSC Receiver (inbound) | Ableton/AbleSet → Game | **8000** UDP | OSC 1.0 |
| OSC Sender (outbound) | Game → Ableton/AbleSet | **9001** UDP | OSC 1.0 |
| TCP Command Server | Any client → Game | **9000** TCP | newline-delimited text |

> The OSC receiver and sender are separate sockets. The game listens on 8000 and sends to 9001 on the host configured in settings (default `127.0.0.1` = same machine).

---

## OSC — Inbound (Ableton/AbleSet → Game)

Sent **to** the game's UDP port **8000**.

| Address | Args | When to Send | Effect |
|---|---|---|---|
| `/partyhero/band_ready` | *(none)* | Band is ready to start | Triggers "band ready" — advances ReadyUp countdown |
| `/partyhero/player_ready` | *(none)* | Toggle a player ready state | Toggles player card in ReadyUp scene |
| `/partyhero/force_state` | `s{stateName}` | Force show to a named state | See Force State values below |
| `/partyyarg/sync/time` | `f{seconds}` | Reply to `/song/start` trigger | Game immediately seeks highway to DAW position (position sync) |

### Force State Values (`/partyhero/force_state`)

| Argument | Effect |
|---|---|
| `Next` | Advance to next show entry (song/break/swap) |
| `SWAP_DONE` | Dismiss WaitingForSwap — advance to next entry |
| `END_BREAK` | Dismiss SetBreak — advance to next entry |

---

## OSC — Outbound (Game → Ableton/AbleSet)

Sent **from** the game to host **127.0.0.1** UDP port **9001**.

### Show-level events

| Address | Args | When Fires |
|---|---|---|
| `/partyyarg/show/start` | `s{showName}` | SetlistManager activates a show |
| `/partyyarg/show/end` | *(none)* | Show fully completed (ShowEnd scene) |

### Song-level events

| Address | Args | When Fires |
|---|---|---|
| `/partyyarg/song/readyup` | `s{title}  s{artist}  i{showIndex}` | ReadyUp scene loads for this song |
| `/partyyarg/song/start` | `s{title}  s{artist}  i{showIndex}` | **Highway t=0** — SongRunner clock starts, audio buffered, calibration applied. Trigger Ableton transport here. |
| `/partyyarg/song/end` | `s{title}  s{artist}  i{showIndex}` | Score screen loads |

### Flow events

| Address | Args | When Fires |
|---|---|---|
| `/partyyarg/break/start` | `s{label}  f{durationSeconds}` | SetBreak scene loads |
| `/partyyarg/swap/start` | `s{label}` | WaitingForSwap scene loads |

### Playback events

| Address | Args | When Fires |
|---|---|---|
| `/partyyarg/pause` | *(none)* | Player pauses game (pause menu opens) |
| `/partyyarg/resume` | *(none)* | Player resumes from pause |

> **Note:** Pause/resume currently fires on any in-game pause including controller drop. If you don't want Ableton to react to player pauses, ignore these addresses during active songs or configure with an empty string in settings to mute them.

---

## MIDI — Inbound (FCB1010 / any MIDI device → Game)

Input device: configurable by name in settings (empty = first available device).

| Note # | Note Name | Default Action |
|---|---|---|
| **60** | C4 | Band Ready |
| **61** | C#4 | Force Next (advance show state) |
| **62** | D4 | Player Ready Toggle |

> FCB1010 tip: Assign each pedal to send a Note On on the MIDI channel matching `MidiDeviceName`. Note Off velocity is ignored — trigger fires on Note On.

---

## MIDI — Outbound (Game → DAW / MIDI device)

Output channel: **1** (configurable). Each event sends a Note On (vel 100) immediately followed by Note Off (vel 0) — a short pulse, not a sustained note.

| Note # | Note Name | When Fires |
|---|---|---|
| **48** | C3 | Show Start |
| **49** | C#3 | Show End |
| **50** | D3 | Song ReadyUp |
| **51** | D#3 | Song Start ← **trigger Ableton transport here if not using OSC** |
| **52** | E3 | Song End |
| **53** | F3 | Pause |
| **54** | F#3 | Resume |
| **55** | G3 | Break Start |
| **56** | G#3 | Swap Start |

> Set any note value to `-1` in settings to disable that specific MIDI trigger.

---

## TCP — Inbound (any TCP client → Game)

Connect to the game's TCP port **9000**. Send newline-terminated UTF-8 text commands.

| Command | Effect |
|---|---|
| `BAND_READY` | Same as MIDI note 60 / OSC band_ready |
| `PLAYER_READY` | Same as MIDI note 62 / OSC player_ready |
| `FORCE_STATE Next` | Advance show state |
| `FORCE_STATE SWAP_DONE` | Dismiss WaitingForSwap |
| `FORCE_STATE END_BREAK` | Dismiss SetBreak |

> TCP is useful for AbleSet scripting or any tool that can open a socket but doesn't speak OSC.

---

## Position Sync Handshake (song start)

This is the key sequence for keeping the highway locked to Ableton's transport:

```
Game highway t=0:
  → OSC /partyyarg/song/start  s{title} s{artist} i{idx}   (UDP → Ableton)
  → MIDI Note 51 pulse                                       (optional)

Ableton receives /song/start:
  → Start transport from cue point (bar 1)
  → Reply: OSC /partyyarg/sync/time  f{transport_position}  (UDP → Game port 8000)

Game receives /partyyarg/sync/time:
  → SetSongTime(position, delay=0)   ← immediate seek, no fade
  → Highway now locked to DAW clock
```

**What to build in AbleSet/Max4Live:**
1. On `/song/start` received → Start transport
2. Read current transport position in seconds
3. Send `/partyyarg/sync/time f{pos}` to game IP:8000

---

## Settings File

All values above are configurable. File location:

```
%APPDATA%\..\LocalLow\YARG\YARG\partyhero_settings.json
```

(The exact path Unity uses: `Application.persistentDataPath + "/partyhero_settings.json"`)

The file is written with defaults the first time the game runs. Edit it in any text editor — the game reloads settings on demand (or restart).

---

## Dev Key Shortcuts (Editor / Nightly builds only)

| Key | Action |
|---|---|
| F9 | Band Ready |
| F10 | Player Ready Toggle |
| F11 | Force Next State |

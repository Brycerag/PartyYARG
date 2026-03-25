# PartyHero Custom Additions - Reference Document

**Purpose:** Document all custom additions made to Moonscraper Chart Editor for porting to YARG or other rhythm game engines.

**Date:** March 2026  
**Original Base Project:** Moonscraper Chart Editor (Unity 2018.4.23f1)  
**Target Replication:** YARG (Yet Another Rhythm Game)

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Show Flow State Machine](#show-flow-state-machine)
3. [DAW/Live Sync System](#dawlive-sync-system)
4. [Setlist Management](#setlist-management)
5. [Gameplay Rule States](#gameplay-rule-states)
6. [UI Systems](#ui-systems)
7. [Development Testing Tools](#development-testing-tools)
8. [File Structure Changes](#file-structure-changes)
9. [Configuration Files](#configuration-files)
10. [Integration Points](#integration-points)

---

## Project Overview

### The PartyHero Concept

**Goal:** Transform a chart editor into a live performance rhythm game system synchronized with live band performance.

**Key Requirements:**
- Play along with live band (not pre-recorded audio)
- Synchronize with DAW (Ableton, etc.) via OSC/MIDI/TCP
- Handle multi-set shows with breaks
- Support player swapping between songs
- Band coordination screens ("waiting for band to tune", etc.)
- Continuous timeline (no menu navigation between songs)

**Why Not Standalone Game:**
- Existing rhythm games designed for solo play with pre-recorded audio
- Need tight integration with live audio production
- Chart editor provides direct .chart file access
- Can modify charts during soundcheck

---

## Show Flow State Machine

### Core Concept

**Traditional Rhythm Game Flow:**
```
Main Menu → Song Select → Gameplay → Results → Back to Menu
```

**PartyHero Show Flow:**
```
Song 1 → Results → Waiting for Band → Song 2 → Results → 
Waiting for Player Swap → Song 3 → Results → Set Break → 
Song 4 → ... → Show End
```

**Key Difference:** No menu navigation. Show flows continuously like a live concert.

---

### State Machine Architecture

**Location:** `Assets/Scripts/Game/Gameplay/` (new files)

**Base Classes:**
- `BaseGameplayRulestate` - Base class for all gameplay rule states
- `SystemManagerState` (from MoonscraperEngine) - State machine base

**State Hierarchy:**
```
SystemManagerState (existing)
└─ BaseGameplayRulestate (NEW)
    ├─ WaitingForBandState
    ├─ WaitingForSwapState
    ├─ SetEndState
    ├─ ShowEndState
    └─ (DefaultRulestate - gameplay)
```

---

### 1. Results Screen State

**File:** `ResultsUISystem.cs`  
**When:** After song ends  
**Duration:** Until player presses SPACE (or MIDI trigger)

**Data Displayed:**
- Hit percentage
- Best streak
- Notes hit / total notes
- Next song name (optional)

**Triggers Out:**
- If continuing → WaitingForBandState
- If player swap → WaitingForSwapState
- If set end → SetEndState
- If show end → ShowEndState

**Logic:**
```csharp
public override void SystemEnter() {
    // Calculate stats from GameplayStateSystem
    float hitPercent = CalculateHitPercentage();
    int bestStreak = GetBestStreak();
    
    // Display results UI
    ShowResultsScreen(hitPercent, bestStreak);
    
    // Check what comes next in setlist
    if (isLastSongInSet) {
        // Go to SetEndState
    } else if (isPlayerSwapTime) {
        // Go to WaitingForSwapState
    } else {
        // Go to WaitingForBandState
    }
}

public override void SystemUpdate() {
    // Wait for continue input (SPACE key or MIDI)
    if (Input.GetKeyDown(KeyCode.Space)) {
        TransitionToNextState();
    }
}
```

**Configuration:**
- Next state determined by setlist JSON rules
- Can be skipped with development key

---

### 2. Waiting For Band State

**File:** `WaitingForBandState.cs`, `WaitingForBandUISystem.cs`  
**When:** Between songs in continuous play  
**Purpose:** Coordination screen while band tunes, catches breath, etc.

**UI Elements:**
- "WAITING FOR BAND" title
- Player ready indicator (✓ or ○)
- Band ready indicator (✓ or ○)
- Instructions: "Press SPACE when ready"

**Ready Logic:**
```csharp
bool playerReady = false;
bool bandReady = false;

public override void SystemUpdate() {
    // Player ready input
    if (Input.GetKeyDown(KeyCode.Space)) {
        playerReady = true;
        UpdateUI();
    }
    
    // Band ready MIDI/OSC input
    if (MIDIInput.BandReadyReceived()) {
        bandReady = true;
        UpdateUI();
    }
    
    // Both ready → start next song
    if (playerReady && bandReady) {
        LoadNextSong();
        TransitionToPlayingState();
    }
}
```

**Development Keys:**
- SPACE = Player ready toggle
- B = Band ready toggle (simulates MIDI)
- R = Resume immediately (skip wait)

**MIDI/OSC Triggers:**
- Band foot pedal sends MIDI note → sets bandReady = true
- Or OSC message: `/partyhero/band_ready`

---

### 3. Waiting For Swap State

**File:** `WaitingForSwapState.cs`, `WaitingForSwapUISystem.cs`  
**When:** Mid-set player change (guitarist switches with bassist, etc.)  
**Purpose:** Give time for physical controller handoff

**UI Elements:**
- "PLAYER SWAP TIME" title
- Swap instructions
- Player status indicator
- "Press SPACE when new player ready"

**Logic:**
```csharp
public override void SystemEnter() {
    // Pause before allowing ready
    swapTimer = 0f;
    minSwapTime = 10f; // Minimum 10 seconds
}

public override void SystemUpdate() {
    swapTimer += Time.deltaTime;
    
    // After minimum time, allow ready
    if (swapTimer >= minSwapTime) {
        if (Input.GetKeyDown(KeyCode.Space)) {
            LoadNextSong();
            TransitionToPlayingState();
        }
    }
}
```

**Configuration:**
- Minimum swap time configurable
- Can be marked in setlist JSON: `"playerSwapAfterSong": true`

---

### 4. Set End State

**File:** `SetEndState.cs`, `SetEndUISystem.cs`  
**When:** Between sets (e.g., after song 6 of 12)  
**Purpose:** Formal break, bathroom/drink opportunity

**UI Elements:**
- "SET BREAK" title
- Custom message ("Back in 10 minutes!")
- Options: Resume show / End show early
- Elapsed time counter

**Logic:**
```csharp
public override void SystemEnter() {
    breakStartTime = Time.time;
    breakMessage = GetBreakMessage(); // From setlist config
}

public override void SystemUpdate() {
    DisplayElapsedTime();
    
    // Resume option
    if (Input.GetKeyDown(KeyCode.R)) {
        LoadNextSong();
        TransitionToPlayingState();
    }
    
    // End show early (emergency)
    if (Input.GetKeyDown(KeyCode.Escape)) {
        TransitionToShowEndState();
    }
}
```

**Configuration:**
- Break message customizable per show
- Optional: Auto-resume after timer

---

### 5. Show End State

**File:** `ShowEndState.cs`, `ShowEndUISystem.cs`  
**When:** After final song  
**Purpose:** Credits, thank you, show stats

**UI Elements:**
- "SHOW COMPLETE!" title
- Thank you message
- Optional: Total show stats (all sets)
- Closing message ("Follow us @bandname")
- Return to editor button

**Logic:**
```csharp
public override void SystemEnter() {
    showEndTime = Time.time;
    totalShowDuration = showEndTime - showStartTime;
    
    // Display all-show stats
    DisplayShowStats();
    
    // Option: Send stats to analytics
}

public override void SystemUpdate() {
    // ESC returns to editor mode
    if (Input.GetKeyDown(KeyCode.Escape)) {
        applicationStateMachine.ChangeState(ApplicationState.Editor);
    }
}
```

**Configuration:**
- Closing message customizable
- Can trigger external events (lights, effects)

---

### State Transition Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                        SHOW START                           │
└──────────────────────┬──────────────────────────────────────┘
                       ▼
              ┌─────────────────┐
              │  Playing Song   │
              │  (Gameplay)     │
              └────────┬─────────┘
                       ▼
              ┌─────────────────┐
              │  Results Screen │
              └────────┬─────────┘
                       │
        ┌──────────────┼──────────────┬──────────────┐
        ▼              ▼              ▼              ▼
┌───────────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│ Waiting for   │ │ Waiting  │ │ Set End  │ │ Show End │
│ Band          │ │ for Swap │ │ Break    │ │ Complete │
└───────┬───────┘ └─────┬────┘ └─────┬────┘ └─────┬────┘
        │               │             │             │
        └───────┬───────┴─────────────┘             │
                ▼                                   ▼
         ┌─────────────┐                    ┌────────────┐
         │ Next Song   │                    │ Back to    │
         │ (Gameplay)  │                    │ Editor     │
         └──────┬──────┘                    └────────────┘
                │
         (Loop continues...)
```

---

### Implementation Files

**Core State Files:**
1. `BaseGameplayRulestate.cs` (147 lines)
   - Base class for all rule states
   - Handles state transitions
   - Manages gameplay system integration

2. `WaitingForBandState.cs` (95 lines)
   - Coordination logic between player and band
   - Ready state management

3. `WaitingForSwapState.cs` (87 lines)
   - Player swap timer
   - Controller handoff coordination

4. `SetEndState.cs` (85 lines)
   - Set break management
   - Resume/end options

5. `ShowEndState.cs` (68 lines)
   - Final state, show complete
   - Stats aggregation

**UI System Files:**
1. `ResultsUISystem.cs` (168 lines)
   - Results screen display
   - Stats calculation and formatting

2. `WaitingForBandUISystem.cs` (128 lines)
   - Ready indicator updates
   - Real-time status display

3. `WaitingForSwapUISystem.cs` (115 lines)
   - Swap screen UI
   - Timer display

4. `SetEndUISystem.cs` (103 lines)
   - Break screen UI
   - Elapsed time counter

5. `ShowEndUISystem.cs` (97 lines)
   - End screen UI
   - Closing message display

**Total:** ~1,093 lines of new C# code

---

### Integration with Existing Systems

**Modified Files:**
1. `GameplayStateSystem.cs`
   - Added currentRulestate field (public)
   - Integrated show flow transitions
   - Modified song end handling

2. `ChartEditor.cs`
   - Made ChangeState() method public for external access
   - Added show flow state references

3. `ApplicationState.cs` (if modified)
   - Added Gameplay state support

**Key Integration Points:**
```csharp
// In GameplayStateSystem.cs
public BaseGameplayRulestate currentRulestate;

void OnSongEnd() {
    // Instead of: Return to editor
    // Now: Transition to results screen
    var resultsState = new ResultsState(stats);
    ChangeState(resultsState);
}

// In any show flow state
void TransitionToNextSong() {
    // Access main editor state machine
    ChartEditor editor = ChartEditor.Instance;
    editor.ChangeState(ApplicationState.Playing, new PlayingState(...));
}
```

---

## DAW/Live Sync System

### Purpose

Synchronize game with live audio production (Ableton, Pro Tools, etc.) instead of internal audio playback.

### Sync Methods

**1. MIDI Clock Sync**
- DAW sends MIDI clock (24 pulses per quarter note)
- Game locks to external tempo
- Start/stop controlled by DAW

**2. OSC (Open Sound Control)**
- DAW sends position updates via network
- Example: `/partyhero/position 123.456` (current time in seconds)
- More flexible than MIDI

**3. TCP Socket**
- Custom protocol for advanced features
- Bi-directional communication
- Lower latency than OSC over UDP

### Configuration Files

**`DAW_SYNC_SETUP.md`** (Documentation)
- Setup instructions for each DAW
- MIDI mapping guide
- OSC address reference
- TCP protocol specification

**`MESSAGE_REFERENCE.md`** (Technical Spec)
- All MIDI/OSC message formats
- Trigger mappings
- Development testing keys

**`STARPOWER_MIDI_OSC_GUIDE.md`**
- Integration guide for triggered events
- Star power/crowd reactions sync
- Lighting cues

### Key Messages

**Game → DAW:**
```
/partyhero/song_start [songName]
/partyhero/song_end [score]
/partyhero/state_change [stateName]
```

**DAW → Game:**
```
/partyhero/band_ready
/partyhero/force_state [stateName]
/partyhero/sync_time [seconds]
```

**MIDI Triggers:**
- MIDI Note 60 (C4) = Band ready
- MIDI Note 61 (C#4) = Force next state
- MIDI CC 20 = Player ready toggle

### Implementation Notes

**For YARG:**
- Check if YARG already has OSC support (likely yes)
- Integrate show flow states with existing sync
- May need to add custom OSC addresses
- MIDI clock sync might already exist

---

## Setlist Management

### Purpose

Define multi-set shows with breaks, player swaps, and custom messages.

### File Format

**`songsync_mapping_example.json`**

```json
{
  "showName": "Summer Tour 2026 - Chicago",
  "venue": "Metro",
  "date": "2026-08-15",
  "sets": [
    {
      "setNumber": 1,
      "songs": [
        {
          "songName": "Opening Anthem",
          "chartPath": "songs/opening_anthem/notes.chart",
          "difficulty": "expert",
          "playerSwapAfter": false
        },
        {
          "songName": "Fast Song",
          "chartPath": "songs/fast_song/notes.chart",
          "difficulty": "expert",
          "playerSwapAfter": false
        },
        {
          "songName": "Epic Solo",
          "chartPath": "songs/epic_solo/notes.chart",
          "difficulty": "expert",
          "playerSwapAfter": true,
          "swapMessage": "Guitarist trades with bassist!"
        }
      ],
      "breakAfter": true,
      "breakMessage": "15 minute intermission - grab a drink!"
    },
    {
      "setNumber": 2,
      "songs": [
        {
          "songName": "Crowd Favorite",
          "chartPath": "songs/crowd_favorite/notes.chart",
          "difficulty": "expert",
          "playerSwapAfter": false
        },
        {
          "songName": "Grand Finale",
          "chartPath": "songs/finale/notes.chart",
          "difficulty": "expert",
          "playerSwapAfter": false
        }
      ],
      "breakAfter": false
    }
  ],
  "endMessage": "Thank you Chicago! Follow us @bandname"
}
```

### Setlist Features

**Per-Song Configuration:**
- Chart file path
- Difficulty level
- Player swap flag
- Custom swap message

**Per-Set Configuration:**
- Break flag
- Break duration
- Break message

**Show Configuration:**
- Show name and metadata
- End message
- Optional: Scoring rules, multipliers

### Setlist Verification Tool

**File:** `SetlistVerifier.cs` (created, exact path TBD)

**Purpose:** Validate setlist before show (all files exist, readable, etc.)

**Checks:**
- All chart files exist at specified paths
- All songs parseable
- No missing audio files (if needed)
- Total show duration estimation

**Usage:**
```csharp
SetlistVerifier verifier = new SetlistVerifier();
verifier.LoadSetlist("path/to/setlist.json");
bool valid = verifier.Verify();
if (!valid) {
    Debug.LogError(verifier.GetErrors());
}
```

**UI Integration:**
- Menu option: "Verify Setlist"
- Shows checklist of validation results
- Warnings for missing files

**Documentation:** `SETLIST_VERIFICATION_QUICKSTART.md`

---

## Gameplay Rule States

### Purpose

Modify gameplay behavior for live performance needs.

### What Are Rule States?

In base Moonscraper, gameplay is monolithic. Rule states allow:
- Different scoring rules per state
- Modified input handling
- Custom game logic per show phase

### Base Class

**`BaseGameplayRulestate.cs`**

```csharp
public abstract class BaseGameplayRulestate : SystemManagerState {
    protected ChartEditor editor;
    protected GameplayStateSystem gameplaySystem;
    
    public BaseGameplayRulestate(ChartEditor editor) {
        this.editor = editor;
        this.gameplaySystem = editor.gameplaySystem;
    }
    
    // Override in derived states
    public abstract void OnNoteHit(Note note);
    public abstract void OnNoteMiss(Note note);
    public abstract void OnSongEnd();
}
```

### Example: Bot Mode

**Hypothetical rule state for automated testing:**

```csharp
public class BotRulestate : BaseGameplayRulestate {
    public override void OnNoteHit(Note note) {
        // Auto-hit all notes perfectly
        score += 100;
    }
    
    public override void SystemUpdate() {
        // Auto-play notes at correct time
        AutoHitUpcomingNotes();
    }
}
```

### Integration

Rule states plugged into `GameplayStateSystem`:

```csharp
// In GameplayStateSystem.cs
public BaseGameplayRulestate currentRulestate;

void Update() {
    if (currentRulestate != null) {
        currentRulestate.SystemUpdate();
    }
}

void OnNoteHit(Note note) {
    if (currentRulestate != null) {
        currentRulestate.OnNoteHit(note);
    }
}
```

---

## UI Systems

### Show Flow UI Requirements

Five custom UI screens needed (none built in Unity yet, only console stubs).

### Planned UI Architecture

**Canvas Hierarchy:**
```
UIServices (existing GameObject)
└─ showFlowUICanvas (NEW)
    ├─ ResultsCanvas
    ├─ WaitingForBandCanvas
    ├─ WaitingForSwapCanvas
    ├─ SetEndCanvas
    └─ ShowEndCanvas
```

### UI Manager Component

**`ShowFlowUIManager.cs`** (planned, not yet created)

**Purpose:** Central access point for all show flow UI elements.

**Properties:**
```csharp
public Canvas resultsCanvas;
public Canvas waitingForBandCanvas;
public Canvas waitingForSwapCanvas;
public Canvas setEndCanvas;
public Canvas showEndCanvas;

// Results screen elements
public TextMeshProUGUI resultsTitle;
public TextMeshProUGUI resultsStats;
public TextMeshProUGUI resultsNextSong;

// Waiting for band elements
public Image playerReadyIcon;
public Image bandReadyIcon;
public Sprite checkMarkSprite;
public Sprite circleSprite;

// ... (all UI element references)
```

**Methods:**
```csharp
public void ShowResultsScreen(float hitPercent, int streak, string nextSong);
public void HideAllScreens();
public void UpdatePlayerReady(bool ready);
public void UpdateBandReady(bool ready);
```

### Current Implementation Status

**✅ Logic Complete:** All show flow states have functional logic  
**✅ Console Stubs:** All UI calls use Debug.Log() for now  
**⚠️ Unity UI Not Built:** No Canvas objects or UI elements created yet  
**📋 TODO List:** Detailed 12-phase plan in `SHOW_FLOW_UI_TODO.md`

### Documentation

**`SHOW_FLOW_UI_TODO.md`** (641 lines)
- Complete Unity UI implementation guide
- Phase-by-phase checklist
- Component specifications
- Layout examples
- Code integration points

**Quick Start added:**
- Unity windows/tools needed
- Components to use
- Basic workflow steps
- Minimal instructions per phase

---

## Development Testing Tools

### Purpose

Test show flow without full live setup (MIDI controllers, DAW, etc.).

### Testing Keys (Development Only)

**Documented in:** `MESSAGE_REFERENCE.md`

**Global Keys:**
- **ESC** - Return to editor / Force exit state
- **P** - Pause gameplay

**Results Screen:**
- **SPACE** - Continue to next state

**Waiting for Band Screen:**
- **SPACE** - Toggle player ready
- **B** - Toggle band ready (simulates MIDI input)
- **R** - Resume immediately (skip waiting)

**Waiting for Swap Screen:**
- **SPACE** - Confirm ready (after minimum time)
- **R** - Resume immediately (skip minimum time)

**Set End Screen:**
- **R** - Resume show (load next set)
- **ESC** - End show early

**Show End Screen:**
- **ESC** - Return to editor

### Testing Workflow

**Without MIDI/DAW:**
1. Load setlist JSON
2. Enter gameplay mode
3. Play song (or use bot mode)
4. Press SPACE on results screen
5. Use B to simulate band ready
6. Continue through show using dev keys

**Console Logging:**
All show flow states output to Unity Console:
```
============================
       SONG COMPLETE
============================
Hit: 87.5%
Best Streak: 142
Notes Hit: 456 / 521
Next: Through the Fire and Flames
============================
Press SPACE to continue...
============================
```

**Validation:**
- Check state transitions work
- Verify correct next state chosen
- Test all input methods
- Ensure no infinite loops

---

## File Structure Changes

### New Files Created

**Gameplay State Machine (9 files):**
```
Assets/Scripts/Game/Gameplay/
├── BaseGameplayRulestate.cs (NEW)
├── WaitingForBandState.cs (NEW)
├── WaitingForSwapState.cs (NEW)
├── SetEndState.cs (NEW)
├── ShowEndState.cs (NEW)
├── ResultsUISystem.cs (NEW)
├── WaitingForBandUISystem.cs (NEW)
├── WaitingForSwapUISystem.cs (NEW)
├── SetEndUISystem.cs (NEW)
└── ShowEndUISystem.cs (NEW)
```

**Documentation (10+ files):**
```
Project Root/
├── DAW_SYNC_SETUP.md (NEW)
├── MESSAGE_REFERENCE.md (NEW)
├── STARPOWER_MIDI_OSC_GUIDE.md (NEW)
├── SETLIST_VERIFICATION_QUICKSTART.md (NEW)
├── SHOW_FLOW_UI_TODO.md (NEW)
├── COUNT-IN_IDEOLOGY.md (NEW - not detailed here)
├── CONTINUOUS_TIMELINE_QUICKSTART.md (NEW)
├── SONG_TRANSITION_UX_SCENARIOS.md (NEW)
└── songsync_mapping_example.json (NEW)
```

**Mobile Companion Planning (6 files):**
```
Mobile Companion App/
├── README.md (NEW)
├── TODO.md (NEW)
├── SYNC_PROTOCOL.md (NEW)
├── TECH_OPTIONS.md (NEW)
├── CHALLENGES.md (NEW)
└── QUICK_REFERENCE.md (NEW)
```

**Utilities:**
```
Assets/Scripts/Tools/
└── SetlistVerifier.cs (NEW - location TBD)
```

### Modified Files

**Core Systems:**
1. `GameplayStateSystem.cs`
   - Added currentRulestate field
   - Modified song end handling
   - Added show flow integration

2. `ChartEditor.cs`
   - Made ChangeState() public
   - Added show flow support

3. `ApplicationState.cs` (possibly)
   - Show flow state references

**Disabled Files (MIDI Issues):**
4. `MidiOutputManager.cs.disabled` (missing NAudio.Midi.dll)
5. `MidiSettingsMenu.cs.disabled`
6. `MidiProtocols.cs.disabled`
7. `InstrumentMidiChannelMap.cs.disabled`

---

## Configuration Files

### 1. Setlist JSON

**Purpose:** Define show structure  
**Location:** User-created, loaded at runtime  
**Schema:** See "Setlist Management" section above

**Key Fields:**
- Show metadata (name, venue, date)
- Sets array
- Songs per set
- Break configuration
- Player swap flags

### 2. DAW Sync Config (Hypothetical)

**Not yet implemented, but would be useful:**

```json
{
  "syncMethod": "osc",
  "oscPort": 8000,
  "oscAddress": "/partyhero",
  "midiInputDevice": "IAC Driver Bus 1",
  "tcpHost": "localhost",
  "tcpPort": 9000
}
```

### 3. Show Preferences (Hypothetical)

```json
{
  "autoStartNextSong": false,
  "minimumSwapTime": 10,
  "defaultBreakDuration": 900,
  "enableDevelopmentKeys": true,
  "logShowFlowEvents": true
}
```

---

## Integration Points

### For YARG Implementation

**Key Questions to Answer:**

1. **State Machine:**
   - Does YARG have a state machine? Where?
   - How are game states managed?
   - Where to inject show flow states?

2. **Song Loading:**
   - How does YARG load songs?
   - Can it load from setlist JSON?
   - Sequential song loading mechanism?

3. **UI System:**
   - Unity version? (YARG likely newer than 2018)
   - TextMeshPro or Unity UI Text?
   - Existing Canvas structure?

4. **Input Handling:**
   - How are inputs processed?
   - Where to add MIDI/OSC listeners?
   - Development key system?

5. **Audio System:**
   - Does YARG support external audio sync?
   - MIDI clock support already?
   - Architecture for replacing internal audio?

6. **Multiplayer:**
   - Does YARG have multiplayer? (Likely yes)
   - Band coordination already exists?
   - Player swap mechanics present?

### Recommended Porting Approach

**Phase 1: Analyze YARG**
1. Clone YARG repo
2. Understand architecture (state machine, song loading, UI)
3. Identify integration points
4. Map Moonscraper concepts to YARG equivalents

**Phase 2: Core Show Flow**
1. Implement 5 show flow states
2. Hook into YARG's state machine
3. Test state transitions
4. Console logging first (like Moonscraper approach)

**Phase 3: Setlist System**
1. Create JSON parser
2. Implement sequential song loading
3. Integrate with YARG's song manager
4. Test multi-song shows

**Phase 4: UI Implementation**
1. Design Unity UI for 5 screens
2. Integrate with YARG's UI system
3. Replace console logs with real UI
4. Polish and animations

**Phase 5: DAW Sync**
1. Check YARG's existing sync (probably has some)
2. Add custom OSC messages
3. MIDI trigger integration
4. Test with live setup

**Phase 6: Testing**
1. Full show rehearsal
2. Identify edge cases
3. Optimize performance
4. User testing

**Estimated Time:** 4-8 weeks (if YARG architecture is friendly)

---

## Key Concepts to Port

### 1. Continuous Timeline

**Problem:** Traditional rhythm games return to menu after each song.  
**Solution:** Show flow states provide seamless transitions.

**Critical:** Never break immersion. Show is one continuous experience.

### 2. Band/Player Coordination

**Problem:** Band needs time between songs (tuning, talking, etc.)  
**Solution:** Waiting states with ready triggers.

**Critical:** Both player and band must confirm ready before continuing.

### 3. External Audio Sync

**Problem:** Game audio can't match live band performance.  
**Solution:** Sync to DAW's timeline, not internal audio.

**Critical:** Timing must feel tight (<20ms latency).

### 4. Flexible Show Structure

**Problem:** Every show is different (setlist, breaks, player changes).  
**Solution:** Setlist JSON defines show structure dynamically.

**Critical:** Must be editable day-of-show (soundcheck changes).

### 5. Graceful State Handling

**Problem:** Live shows have unexpected moments (equipment failure, etc.)  
**Solution:** Development keys allow manual state override.

**Critical:** Stage tech can force state transitions if needed.

---

## Dependencies & Libraries

### Moonscraper Dependencies

**Unity Version:** 2018.4.23f1 LTS (specific version required)

**Libraries:**
- NAudio (Core and WinMM only - Midi.dll missing)
- TextMeshPro (Unity package)
- Standard Unity packages

**Custom:**
- MoonscraperEngine namespace (custom utilities)

### YARG Dependencies (To Research)

**Expected:**
- Newer Unity version (2021+?)
- Modern input system
- Likely better MIDI/OSC support
- Possibly multiplayer networking
- Better audio system

**Advantage:** YARG is actively maintained open-source project, likely has better architecture.

---

## Testing Checklist

Use this to validate port to YARG:

### State Machine Tests
- [ ] All 5 show flow states exist
- [ ] State transitions work correctly
- [ ] Can return to editor from any state
- [ ] Development keys work in all states

### Setlist Tests
- [ ] JSON loads successfully
- [ ] Multi-set shows work
- [ ] Player swap logic activates correctly
- [ ] Set breaks appear at right times
- [ ] Show end state appears after final song

### Sync Tests
- [ ] MIDI triggers received
- [ ] OSC messages received
- [ ] Band ready trigger works
- [ ] Player ready trigger works
- [ ] Force state change works

### UI Tests
- [ ] All 5 screens display correctly
- [ ] Stats calculated correctly
- [ ] Ready indicators update in real-time
- [ ] Timers count properly
- [ ] Text formatting readable

### Integration Tests
- [ ] Full show runs without errors
- [ ] Songs load sequentially
- [ ] State machine doesn't hang
- [ ] Memory doesn't leak over long show
- [ ] Performance acceptable (60fps)

### Edge Case Tests
- [ ] Empty setlist handled
- [ ] Missing song file handled
- [ ] Network disconnect handled
- [ ] Mid-song state exit works
- [ ] Rapid state transitions don't crash

---

## Known Issues & Workarounds

### Moonscraper Issues

1. **NAudio.Midi.dll Missing**
   - Workaround: Disabled 4 MIDI-related files
   - OSC works as alternative

2. **Unity 2018.4 Limitations**
   - Old TextMeshPro version
   - Less efficient rendering
   - Would benefit from upgrade

3. **No Built-in State Machine**
   - Had to build from SystemManagerState
   - Not designed for this use case

### YARG Advantages

- Likely newer Unity (better performance)
- Probably has multiplayer (band coordination built-in?)
- May have better MIDI support
- Active development (bug fixes)
- Community support

---

## Future Enhancements (Not Yet Implemented)

### 1. Mobile Companion App
- Live audience participation
- Phone syncs to stage game
- See "Mobile Companion App/" folder for full planning

### 2. Advanced Stats
- Per-show analytics
- Score history
- Performance trends

### 3. Live Streaming Integration
- OBS scene switching tied to states
- Auto-update stream overlays
- Twitch/YouTube integration

### 4. Lighting Control
- DMX output for stage lights
- Sync to gameplay events
- Auto-generated light shows

### 5. Recording/Replay
- Record full show for review
- Replay mode for practice
- Export to video

### 6. Multi-Player Local
- Split-screen or multiple instruments
- Band plays together on stage
- Sync all players

---

## Documentation Assets

All markdown files in project root:

1. **DAW_SYNC_SETUP.md** - Setup guide for DAW integration
2. **MESSAGE_REFERENCE.md** - MIDI/OSC message reference
3. **STARPOWER_MIDI_OSC_GUIDE.md** - Event trigger guide
4. **SETLIST_VERIFICATION_QUICKSTART.md** - Setlist validation
5. **SHOW_FLOW_UI_TODO.md** - Unity UI implementation guide
6. **CONTINUOUS_TIMELINE_QUICKSTART.md** - Show flow overview
7. **SONG_TRANSITION_UX_SCENARIOS.md** - Transition design
8. **COUNT-IN_IDEOLOGY.md** - Timing and rhythm concepts (brief)

**Mobile App Planning:**
9. **Mobile Companion App/README.md** - Full concept and architecture
10. **Mobile Companion App/TODO.md** - Development checklist
11. **Mobile Companion App/SYNC_PROTOCOL.md** - Network protocol
12. **Mobile Companion App/TECH_OPTIONS.md** - Technology choices
13. **Mobile Companion App/CHALLENGES.md** - Risks and solutions
14. **Mobile Companion App/QUICK_REFERENCE.md** - One-page summary

**This Document:**
15. **PARTYHERO_ADDITIONS_REFERENCE.md** - Comprehensive port guide

---

## Quick Command Reference

### Starting a Show (Moonscraper)
1. Open Unity project
2. Load Main Editor.unity scene
3. Load setlist JSON (File > Load Setlist)
4. Enter gameplay mode (Play button)
5. First song auto-loads
6. Use SPACE and B keys to progress through show

### Development Testing
- Test individual state: Instantiate state directly
- Test transitions: Use dev keys to force states
- Test UI: Check Unity Console for logged output
- Test sync: Use MIDI Monitor or OSC debugger

### Common Tasks
- Add new song to setlist: Edit JSON, add entry
- Change break duration: Modify set breakDuration
- Skip a state: Press R or ESC (dev keys)
- Force state change: OSC message `/partyhero/force_state`

---

## Contact & Notes

**Original Implementation:** Moonscraper Chart Editor fork  
**Target Platform:** YARG (Yet Another Rhythm Game)  
**Development Period:** March 2026  
**Unity Version:** 2018.4.23f1 (Moonscraper) → 2021+ (YARG?)

**Critical Success Factors:**
1. State machine integration must be clean
2. Sync latency must be <50ms
3. UI must be immediately understandable
4. System must be reliable enough for live use
5. Setlist changes must be quick (soundcheck flexibility)

**Philosophy:**
- Simplicity over features
- Reliability over polish
- Live performance > Solo play
- Band coordination > Individual scoring

---

## Appendix: Code Snippets

### Example: Loading Setlist

```csharp
public class SetlistManager {
    public Setlist LoadSetlist(string filePath) {
        string json = File.ReadAllText(filePath);
        Setlist setlist = JsonUtility.FromJson<Setlist>(json);
        return setlist;
    }
    
    public Song GetNextSong(Setlist setlist, int currentIndex) {
        // Find next song across sets
        int songCount = 0;
        foreach (var set in setlist.sets) {
            foreach (var song in set.songs) {
                if (songCount == currentIndex + 1) {
                    return song;
                }
                songCount++;
            }
        }
        return null; // No more songs
    }
    
    public bool IsLastSongInSet(Setlist setlist, int currentIndex) {
        // Determine if current song is last in its set
        // ... logic ...
    }
}
```

### Example: State Transition

```csharp
public class ResultsUISystem : SystemManagerState {
    public override void SystemUpdate() {
        if (Input.GetKeyDown(KeyCode.Space)) {
            // Determine next state based on setlist
            if (isPlayerSwapTime) {
                var swapState = new WaitingForSwapState(editor);
                editor.applicationStateMachine.ChangeState(swapState);
            } else if (isSetEnd) {
                var setEndState = new SetEndState(editor);
                editor.applicationStateMachine.ChangeState(setEndState);
            } else {
                var bandState = new WaitingForBandState(editor);
                editor.applicationStateMachine.ChangeState(bandState);
            }
        }
    }
}
```

### Example: OSC Message Handler

```csharp
public class OSCHandler {
    void OnMessageReceived(OSCMessage msg) {
        if (msg.address == "/partyhero/band_ready") {
            if (currentState is WaitingForBandState) {
                ((WaitingForBandState)currentState).SetBandReady(true);
            }
        } else if (msg.address == "/partyhero/force_state") {
            string stateName = msg.values[0].StringValue;
            ForceStateChange(stateName);
        }
    }
}
```

---

## Summary

**Total Addition:** ~1,500 lines of C# code + 15 documentation files

**Core Concepts:**
1. Show flow state machine (5 states)
2. Setlist JSON structure
3. DAW sync (MIDI/OSC/TCP)
4. Band/player coordination
5. Continuous timeline (no menus)

**Value Proposition:**
- Transforms editor into live performance system
- Seamless multi-song shows
- Band coordination built-in
- Flexible show structure
- External audio sync

**Port to YARG:**
- Check YARG architecture first
- Likely easier than Moonscraper (better base)
- Focus on state machine integration
- Reuse all concepts and documentation
- Estimated 4-8 weeks for experienced dev

**End Goal:**
Live rhythm game system that feels like a real concert, not a video game.

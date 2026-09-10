# Audio Laboratory — implementation proposal

Read before implementation: the complete PRD, all project source, scene, package/settings files, existing documentation and tests. Baseline commit: `d93e6f1`. Baseline: 24 domain tests and 16 Windows player checks pass. No existing user changes were present.

## Existing architecture

1. **Gameplay timing:** `RunnerSimulation` advances in integer 240 Hz ticks. `PulseGame` catches it up to the latest full tick of `SongTransport.Position`. Inputs have tick timestamps. Movement and collision do not use Unity physics or accumulated render-frame delta.
2. **Audio synchronisation:** `SongTransport` schedules a preloaded PCM clip using `AudioSource.PlayScheduled`. `SongClock` shares that DSP epoch. Pausing freezes at authoritative simulation position; restarting resets input, simulation, source and presentation. The requested DSP buffer is 256 samples; scheduling lead is at least 60 ms.
3. **Visual reactions:** `VisualDirector` evaluates `VisualEventTrack.Afterlight` and `Theme.At` using absolute beat time. The environment draws pulses/parallax; the camera and particle burst emphasise the fixed beat-48 drop. Decoration and collision are separate.
4. **Current level:** `LevelDefinition.Afterlight` constructs immutable solids/spikes from an explicit 96-beat score. Its constants describe this one authored 45-second, 128 BPM level.
5. **Reuse:** keep `SongClock`, scheduled audio, the engine decoder, quality profiles, palette vocabulary, mesh drawing, environment rendering and particle pool. Reuse the visual language and safe-area Canvas design for new screens.
6. **Extend:** add selectable clips and seek operations to the transport; introduce a separate library/laboratory controller; add a title-screen entry and explicit input ownership while the laboratory is open; add a small sandbox entry point to the visual director. New music analysis lives in a Unity-independent assembly.
7. **Future issues:** Afterlight constants must not become arbitrary-song assumptions; estimated tempo/downbeat confidence must travel with timing; cache versions and content identity must be checked; analysis/decode memory must be released before gameplay; uncertain structural labels must not imply validated gameplay. These are addressed at new boundaries, without rewriting the working runner.

## Preservation boundary

Do not change the jump model, tuning, collision routines, gameplay input mappings, existing authored chart, camera algorithm, current visual track/theme definitions, shader, score, gameplay HUD layout or scene. Keep the original `VisualDirector.Render` path intact. New laboratory UI and sandbox paths are separate. The current player verification remains the regression gate.

## Proposed data contract

`SongMap` is serialisable C# data with seconds on the decoded PCM timeline:

- Schema version, algorithm version, analysis settings identity, source SHA-256, source sample rate/channels, duration.
- Estimated BPM/confidence and ranked tempo alternatives, including half/double interpretations; explicit optional developer BPM/phase override, preserving the estimate.
- Beats: time, index, strength, confidence, estimated position in a four-beat bar. Downbeats are estimates, not instrument/meter recognition.
- Onsets: time, strength and confidence, without instrument classification.
- Compact uniform energy samples: normalised intensity, absolute RMS and low/mid/high-band energy; waveform min/max peaks.
- Nonoverlapping sections and timestamped build/drop/breakdown/transition events, each with confidence.
- Binary-search queries for current/next/nearest beat, interpolated subdivision and energy, and containing section.

Silence and weak periodicity return low confidence or no beat grid. Absolute times remain independent of frame rate. Consumer code depends on the contract, not the analyser implementation.

## Analysis approach and dependencies

Use Unity's built-in local WAV/MP3 decoder through its audio download handler with a local file URI. Copy PCM in bounded chunks, prepare mono analysis data, then run analysis on a cancellable worker. No network service or external AI is used.

Implement a deterministic radix-2 FFT/STFT and logarithmically compressed positive spectral flux. Pick attacks against an adaptive local threshold. Estimate tempo from the novelty envelope's periodicity, retain competing interpretations, then track a sequence of beat times with a tempo-consistency constraint. RMS and coarse frequency-band curves are smoothed and reduced for display. Structural events compare sustained energy/trend and band changes across time windows; use conservative labels and confidence.

No third-party dependency is proposed. Unity's bundled web-request/audio modules and standard C# JSON/cryptography/IO are sufficient for this foundation. A trained beat/downbeat model may improve later accuracy, but would add model/runtime distribution and evaluation obligations; it is not introduced without evidence from the laboratory.

Background scanning computes stable content hashes and queries versioned SongMap caches. The folder provider implements a source interface so a future Android document picker can supply song identifiers without changing analysis or gameplay. Cache writes are replaceable and validated; changed content or versions trigger fresh analysis automatically.

## Plan

1. Add the data contract, queries, deterministic analysis, cache identity/validation and synthetic-signal tests.
2. Add asynchronous desktop discovery, configurable Songs directory, local audio loading and durable cache storage.
3. Add a Pulse-styled song browser and a layered timeline with play/pause/restart, seek, zoom, layer toggles and event details.
4. Connect a small opt-in visual sandbox to analysed beat/onset/energy/drop data.
5. Verify WAV and MP3 loading, analysis, cache reuse across process restarts, timeline seeking, screenshots and the unchanged Afterlight replay. Document measured accuracy and limits before future procedural generation.

## Technical references

The analysis approach is informed by the primary educational references on [spectral novelty](https://www.audiolabs-erlangen.de/resources/MIR/FMP/C6/C6S1_NoveltySpectral.html), [autocorrelation tempo analysis](https://www.audiolabs-erlangen.de/resources/MIR/FMP/C6/C6S2_TempogramAutocorrelation.html) and [dynamic-programming beat tracking](https://www.audiolabs-erlangen.de/resources/MIR/FMP/C6/C6S3_BeatTracking.html). Audio loading follows Unity's [GetAudioClip](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Networking.UnityWebRequestMultimedia.GetAudioClip.html) and [GetData](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioClip.GetData.html) APIs. These are algorithm/API references; no third-party implementation is copied into the project.

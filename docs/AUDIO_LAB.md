# Audio Laboratory

The laboratory is a separate developer workbench beside the authored Afterlight runner. It imports local audio, produces a versioned SongMap, and lets you compare its interpretation with playback. It does **not** generate levels.

## Import your song

1. Put a standard MP3 or PCM WAV export into `Builds/Windows/Songs` for the Windows player, or the repository's `Songs` folder for Unity Play mode. Your songs are ignored by Git.
2. Launch `Builds/Windows/Project Pulse.exe`, or play the existing Afterlight scene in Unity.
3. Choose **Audio Lab** on the title screen. **Begin Run** still starts Afterlight.
4. Select your song, then **Analyse**. Audio is decoded once; analysis runs in the background with progress and cancellation.
5. Press **Play**. Choose **Fit** to see the whole song, then zoom into interesting moments.

For another location, enter an existing folder in **Your Songs → Use Folder**. This persists. Discovery is top-level, not recursive; extensions are case-insensitive. **Scan** refreshes immediately; while idle the lab checks every 15 seconds. It does not scan during gameplay or song playback. A launch-only override is available: `-pulse-songs "E:/My Music"`.

The filename stem is the title; embedded tags are not parsed. Duration becomes known after decoding or cache loading. A selected unanalysed song can already play, but its timeline appears after analysis.

## Controls and layers

| Control/layer | Meaning |
| --- | --- |
| Play / Pause, Space | Schedule/pause audio and its DSP-backed clock together |
| Restart, R | Restart at sample zero |
| Left/right arrow | Seek five seconds |
| Left-drag timeline | Scrub; playing audio pauses during the drag and resumes on release |
| Mouse wheel, + / − | Zoom; wheel zoom anchors under the pointer |
| Right-drag, Pan > | Move the visible time window |
| Fit / Follow | Whole-song view / follow the cursor in pages |
| Wave | Signed min/max waveform, reduced to screen resolution |
| Beats | Mint beat markers; short ticks are interpolated half-beats when reliable |
| Gold rhythm markers | Estimated four-beat-group downbeats, only when accent evidence exists |
| Onsets | Coral attack markers; height is strength, not instrument identity |
| Energy | Mint normalised intensity; not LUFS loudness |
| Bands | Gold low (<250 Hz), violet mid (250 Hz–4 kHz), coral high (>4 kHz to analysis Nyquist) |
| Sections | Relative low/mid/high energy or an estimated build, not verse/chorus labels |
| Moments | Vertical markers and `DROP?`, `BUILD?`, etc.; question marks denote heuristics |
| Hover | Exact seconds, event strength/confidence; hover the corresponding lane |
| Visual Sandbox | Existing atmosphere/orb, pooled onset sparks and a stronger drop burst driven by SongMap |
| Escape / Back | Stop imported audio, cancel work, release the map/clip and return to Afterlight |

Layer buttons independently hide/show data. Timeline geometry changes only with its data/layers/viewport; the cursor is a separate inexpensive element. Playback shortcuts are suppressed while editing a text field. F11 retains display switching. Focus loss or an audio-device change pauses playback.

The sandbox uses the existing environment, palette vocabulary, quality profile and particles through a separate rendering entry point. It does not apply Afterlight's authored beat-48 camera/drop cue to arbitrary songs. Reduced-motion settings still apply.

## Tempo ambiguity and correction

The headline BPM has a heuristic confidence score; alternatives show periodicity scores. These are **not calibrated probabilities**. A broad 85–180 BPM preference can select a plausible metrical level over a slightly stronger half-time autocorrelation peak. Both interpretations remain visible.

For an 87/174 ambiguity, enter `174` under **BPM** and choose **Apply Grid**. **Phase (s)** offsets the first beat within one period: for 120 BPM, at least zero and less than 0.5 seconds. Use decimal points. The manual grid is an explicit regular-grid hypothesis, not newly detected attacks; confidence/downbeat grouping is not fabricated. The original automatic estimate and alternatives remain in the SongMap. Overrides persist in the cache. **Reanalyse / Debug** explicitly replaces analysis and restores automatic tracking.

## Architecture and analysis

`Assets/Pulse/Music` is Unity-independent, with no NuGet or third-party analysis package. Runtime decoding uses Unity's built-in audio/local-file web-request modules. Nothing is uploaded and no AI API is called.

Deterministic pipeline for identical decoded PCM and versions:

1. SHA-256 hash source bytes; validate a matching cache on a worker.
2. Decode selected WAV/MP3 to PCM. Verify its hash again after decoding.
3. Select the highest-energy channel from distributed previews to avoid anti-phase stereo cancellation. Copy PCM in bounded batches across frames.
4. Windowed-sinc resample to 22,050 Hz. Analyse centred 2,048-sample Hann windows with a 256-sample hop (about 11.6 ms).
5. Positive log-spectral flux, local-background suppression and attack picking. Refine attacks against nearby short-window power rises.
6. Mean-centred novelty autocorrelation, period refinement from attack intervals, competing tempo candidates. Fit global pulse phase with bounded local attack alignment; unsupported predicted beats have low confidence.
7. Produce 20 Hz smoothed RMS/intensity/bands and 80 Hz waveform peaks. Normalise against the song's smoothed 95th-percentile level, with an absolute floor preventing near-silence becoming high energy.
8. Compare sustained energy/trends and low-band changes for structural hypotheses. The structure analyser is independently replaceable.
9. Validate and atomically save, verifying the source hash again before saving.

Contract: schema **1**, algorithm **pulse-spectral-3**. SongMap carries content identity/settings, source duration/rate/channels, tempo alternatives/confidence/override, indexed beats, onset strength/confidence, energy/RMS/bands, waveform, contiguous sections and major moments. All times are seconds on the **decoded PCM timeline**, including MP3 padding as exposed by the decoder. Resampling changes duration by at most half an analysis sample.

Queries: `CurrentBeat`, `NextBeat`, `NearestBeat`, `NearestSubdivision`, `NextOnsetIndex`, `IntensityAt`, `SectionAt`. Current is inclusive; next beat/onset is strictly later; sections are half-open. Missing rhythm returns null. Subdivisions interpolate tracked beats and remain hypotheses. Consumers never accumulate render-frame deltas to locate beats.

`ISongSource` isolates discovery metadata/identifiers; `FolderSongSource` is its desktop implementation. A future Android document-picker provider must supply a readable local copy/URI at the decoder boundary. The analyser, SongMap and gameplay do not depend on Windows directory enumeration.

## Cache behaviour

JSON caches live under `Application.persistentDataPath/SongMaps`, normally `%USERPROFILE%/AppData/LocalLow/Project Pulse/Project Pulse/SongMaps` on Windows. Filenames hash **audio SHA-256 + schema + algorithm + settings identity**. Changed content/versions/settings, invalid payloads and corrupt JSON become cache misses. Old entries can remain on disk but are never selected by new keys. No manual cache deletion is necessary. The original file path is not embedded in the portable SongMap.

## Limits and next improvements

- This is an initial deterministic DSP foundation, **not a production-grade general beat/downbeat model**. It was tested with synthetic signals and Afterlight, including a locally encoded MP3. No user's Suno track was available for subjective validation.
- The tracker assumes one dominant tempo. Rubato, tempo changes, swing, polyrhythms, syncopated intros and sparse percussion may produce poor grids. Half/double errors remain possible. Confidence and overrides are essential; a returned beat is not automatically safe for gameplay.
- Four-beat accent grouping is not meter detection. Many songs show **downbeats unknown**. There is no instrument/key/chord/verse/chorus recognition.
- Structural heuristics can miss important moments or mistake loudness changes for drops. Short breaks can disappear inside sustained windows. Confidence is not an empirical success rate.
- A representative channel may miss material panned exclusively elsewhere. Future multichannel feature fusion should preserve energy without phase cancellation.
- Bounds: 0.1–1,200 seconds, 8–192 kHz, 1–8 source channels, **128 MiB decoded interleaved PCM**. Stereo 44.1 kHz reaches the PCM cap after about 6.3 minutes. Use a shorter/lower-rate export when necessary. Unity allocates the decoded clip before these checks; unusually large compressed files can temporarily use substantial memory.
- Resampling/FFT/cache work is cancellable worker work. Engine decoding/allocation and bounded PCM reads may still produce brief frame stalls. Measured on this PC: about 0.93 seconds to analyse 45 seconds of audio; largest loading/analysis frame 41.7 ms. This is not a low-end hardware guarantee.
- Paused seeks quantise clock and audio to one decoded sample. Playing seeks retain the transport's minimum 60 ms scheduling lead. Tests check Unity PCM/DSP agreement, **not speaker/Bluetooth acoustic latency or subjective sync**.
- Closing releases the selected map/clip and cancels workers. There is no library scanning during gameplay. Cache storage is not size-pruned yet.

Before procedural generation, assemble a representative, legally usable evaluation corpus including the user's Suno/electronic tracks. Listen and annotate reference beat/downbeat/drop times, then measure precision/recall and timing error instead of tuning to one score. Priorities: adaptive/local tempo, meter/downbeat inference, multichannel fusion, longer-form structure, calibrated uncertainty and device-latency handling. Future gameplay generation must reject uncertain regions and independently prove solvability: SongMap is evidence, not level design.

## Reproduce tests

```powershell
dotnet run --project Tests/Pulse.Domain.Tests.csproj --configuration Release
dotnet run --project Tests/Pulse.Music.Tests.csproj --configuration Release
```

There are 24 original domain tests and 35 music tests. The projects use isolated intermediate directories and no external packages. Build with Unity's existing **Project Pulse → Build Windows player** command.

For decoder tests, `scripts/Prepare-LabFixtures.py` creates WAV/MP3 fixtures from the original score using an **already installed** LAME DLL (e.g. Audacity's). This is a test-only encoder, not redistributed or required by the game:

```powershell
python scripts/Prepare-LabFixtures.py --lame 'C:/Program Files/Audacity/libmp3lame.dll'
./scripts/Verify-AudioLab.ps1 -OutputDirectory "$PWD/TestResults/AudioLabFinal"
./scripts/Verify-AudioLab.ps1 -OutputDirectory "$PWD/TestResults/AudioLabFinalCache" -ExpectCache
./scripts/Verify-Player.ps1 -OutputDirectory "$PWD/TestResults/AfterAudioLab"
```

The lab fixture checks intentionally assert the known Afterlight tempo/drop; they are not an arbitrary-song benchmark. They also check audible output, paused/playing scrubs, restart, end-of-song, release and fresh-process caching. Results/PNG captures live in each output directory. See [validation](AUDIO_LAB_VALIDATION.md).

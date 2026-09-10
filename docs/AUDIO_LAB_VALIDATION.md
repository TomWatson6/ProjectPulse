# Audio Laboratory validation — 10 September 2026

Windows x64 Mono player, Unity 2022.3.12f1, Direct3D 11, existing RTX 3080 Ti machine. Final build succeeded without script/shader errors; report size **77,920,575 bytes**. No external analysis dependency was added; two built-in Unity decoding modules were enabled.

## Tests and evidence

- **24/24 original domain tests** pass.
- **35/35 music tests** pass: beat/phase accuracy for synthetic 120/128/174 BPM, octave ambiguity, silence/noise, near-silence normalisation, exact queries/subdivisions, overrides and invalid inputs, determinism, cancellation, energy/bands, heuristic transitions, anti-alias resampling, JSON roundtrip, cache invalidation/replacement/corruption and discovery/hash filtering.
- **20/20 real-player laboratory checks** pass; **22/22** in the fresh-process cache pass. WAV/MP3 decoding, SongMap validation, scheduled audible output, paused/playing scrubs, restart, end-of-song and resource release were exercised. Both formats reuse cache after a process restart with **zero analysis runs**.
- **16/16 unchanged Afterlight real-player checks** pass after adding the laboratory, including first-spike death, retries, all 25 authored jumps, complete replay, display modes and audio output.

Raw records/captures: `TestResults/AudioLabFinal`, `TestResults/AudioLabFinalCache`, `TestResults/AfterAudioLab`. Reproduction commands and test-fixture preparation are in [AUDIO_LAB.md](AUDIO_LAB.md). Test audio is the project's original score plus a locally encoded 192 kbps MP3 using an already installed LAME DLL; that encoder is not a game dependency.

## Analysis observations

On the 45-second Afterlight WAV, algorithm `pulse-spectral-3` reports **127.999 BPM**, **96 beats**, **249 onsets**, and a **drop at 22.50 seconds** (the authored drop). Tempo confidence is **48.1%**, intentionally not a claim of certainty: the strong 64 BPM interpretation remains visible. Downbeats are unknown where four-beat accent evidence is insufficient.

Analysis took **0.929 seconds** in the measured run, excluding selection/initial decoding. The largest observed loading/analysis frame was **41.69 ms**; cached loading's largest observed frame was **26.97 ms**. FFT/resampling/cache work runs off-thread, but engine decoding and allocation are not stall-free. These results do not establish performance on lower-powered devices.

An early validation pass exposed 64 BPM preference and a missed drop. The tempo candidate prior and structure normalisation/thresholds were corrected, then synthetic tests and actual-player checks rerun. Mean-centred periodicity scoring was subsequently added so dense unstructured noise cannot masquerade as rhythmic evidence. Cache algorithm versions were incremented automatically throughout; no manual cache deletion was required.

The timeline and title-return captures were inspected at 1600×900 and 1920×820. The lab uses the original navy/mint/coral/orb identity, with legible lanes, hover detail, exact cursor, spectral colour legend and explicitly heuristic structural labels. It does not replace the original HUD or authored rendering.

## Timing and regression

Imported WAV/MP3 reported PCM position agrees with the DSP-backed cursor within the **20 ms** integration-test limit after scheduled playback and seeking. Paused seeks quantise source and clock to the same sample and agree within **0.1 ms**. Playing drags preserve the prior playback state. Cached maps use the same decoded PCM time base rather than file-container duration guesses.

Afterlight regression measurements (6,768 timing samples):

| Measurement | Before lab baseline | After lab |
| --- | ---: | ---: |
| Maximum simulation difference from exact DSP target | 4.000 ms | 4.000 ms |
| Post-update simulation/DSP P95 | 12.833 ms | 12.500 ms |
| Reported PCM/DSP P95 | 0.028 ms | 0.028 ms |
| Frame time P95 | 6.952 ms | 6.953 ms |
| Scheduling lead | 60 ms | 60 ms |

The movement/tuning/collision/input provider/domain clock/current chart/camera/theme/shader/scene/score files have no content changes. The original `VisualDirector.Render` body remains intact. Additions are isolated to the lab, two built-in decoder modules, small title/input/transport integration and an optional separate visual entry point.

## Not established by these tests

No personal Suno file was provided, and no subjective listening alignment, physical input latency, speaker/Bluetooth delay or physical device-disconnection test was performed. Tests validate signal/clock agreement and known synthetic/original-score landmarks, **not general musical understanding across arbitrary tracks**. Variable tempo, ambiguous meter, phase/channel choices and structural false positives remain documented limitations. A representative annotated listening corpus and uncertainty-aware evaluation are required before procedural level generation.

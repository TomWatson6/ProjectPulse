# Milestone 1 verification

This records the original milestone. The subsequent Audio Laboratory and full preserved-runner regression are recorded in [AUDIO_LAB_VALIDATION.md](AUDIO_LAB_VALIDATION.md).

Verified on 9 September 2026 with Unity **2022.3.12f1**, a Windows x64 Mono player, Direct3D 11 and an NVIDIA GeForce RTX 3080 Ti. The final Windows build succeeded with no script or shader errors. Build report size: **77,836,122 bytes**.

## Automated domain tests

**24 passed, 0 failed.** The .NET runner compiles the real `Pulse.Domain` sources. Output is in `TestResults/domain-tests.txt`.

Coverage includes scheduled starts, clock monotonicity, pause/resume, clock reset, invalid configuration, guarded session transitions, tick-assigned inputs, stable grounded movement, analytic jump trajectories, no mid-air double jump, buffered landing jumps, swept triangle collision, first-obstacle death, full-chart completion, ±35 ms timing tolerance, 30/59/144 Hz render grouping, restart after death, completion freeze, omitted gap jumps, visual timeline seeking/reset, quality budgets, coyote-time expiry, solid walls and elevated platform landings.

## Actual Windows player

**16 checks passed.** The opt-in verification run:

1. Loaded the real imported audio and detected nonzero samples at the AudioListener output.
2. Started without input and died on the first spike.
3. Retried, checking simulation and song reset to zero with no asset reload.
4. Played the full reference input score through the actual renderer and audio transport.
5. Paused mid-jump, verified frozen time, opened settings and resumed.
6. Applied borderless fullscreen and windowed modes, checking the engine's resulting mode.
7. Captured 16:10, 16:9 and ultrawide presentations; changed to Low quality during the replay.
8. Completed all 25 jump cues, reached the finish, and restarted cleanly after completion.
9. Checked simulation/audio clock measurements against explicit limits.

The final run had **6,754 timing samples**:

| Measurement | Result |
| --- | ---: |
| Maximum simulation difference from the exact DSP target used by its update | 4.000 ms |
| Post-update simulation/DSP difference, 95th percentile | 13.000 ms |
| Post-update simulation/DSP difference, maximum observed | 14.667 ms |
| Reported PCM sample-position/DSP difference, 95th percentile | 0.023 ms |
| Frame time, 95th percentile | 6.973 ms |
| Restart/resume scheduling lead | 60.0 ms |
| Actual DSP buffer / output sample rate | 256 × 4 samples / 48,000 Hz |

Raw results: `TestResults/Player/verification.txt`. Player diagnostics: `TestResults/Player/player.log`. Reproduce with `scripts/Verify-Player.ps1` after building.

An earlier check read the DSP clock at a different point in the frame from the simulation target. The instrumentation now reports both the exact target difference and the later observed difference. The rerun then identified a real issue with the initial 1,024-sample default DSP buffer: the observed 95th-percentile difference was 22.167 ms. Reducing the project's requested buffer to 256 samples improved it to 13.000 ms and reduced scheduling lead from 100.3 to 60.0 ms. The final test retained its 20 ms observed-difference limit.

## Visual and audio review

The captured title, active jump, drop, gold/violet section, settings, death, ultrawide Low-quality gameplay and completion screen were inspected visually. Text is legible and contained; gameplay and coral hazards remain distinct in both palettes. The title no longer places the player under its start button. The drop coordinates palette, rings, a particle burst and camera motion while maintaining hazard visibility.

Captures in `TestResults/Player`:

- `01-title.png`
- `02-death.png`
- `03-jump.png`
- `04-settings.png`
- `05-flow-16x9.png`
- `06-drop.png`
- `07-afterlight.png`
- `08-ultrawide-low.png`
- `09-complete.png`

The generated WAV is exactly **45.0 seconds**, **44,100 Hz**, **stereo**, signed 16-bit PCM. Measured peak amplitude is **0.7577** of full scale. RMS energy falls from **0.1210** in the build to **0.0242** in the breath, then rises to **0.1499** in the drop. Audio output was checked programmatically; this is not a subjective listening or acoustic latency measurement.

## Architecture and performance review

The domain assembly has no Unity engine references. Visual code consumes immutable collision data and cannot alter it. Quality changes only affect presentation. Runtime geometry uses three mesh batches and a fixed particle pool; the player outline reuses its array and UI strings update at 20 Hz. No physics engine, per-obstacle update functions, scene reloads on retry or audio decoding on retry are involved. The real player stayed near its 144 Hz target on the tested PC.

## Remaining hands-on validation

The reference replay supplies semantic input; physical keyboard/mouse latency and subjective movement feel need a human playtest. The player checks validate Unity's audio signal/clock, not the time sound reaches a listener through their speakers or Bluetooth headphones. Focus-loss and audio-device-change handlers are implemented but were not tested by physically changing devices. No Android build or lower-powered PC performance claim is made. All four quality tiers are implemented; the real replay exercised High and Low.

The next task should be a short hands-on movement/audio calibration pass on the intended hardware: verify jump timing by ear, test repeated deaths and retries, judge the drop's readability, and record any device offset. Then continue to the PRD's Milestone 2 manually authored visual showcase before starting arbitrary song analysis.

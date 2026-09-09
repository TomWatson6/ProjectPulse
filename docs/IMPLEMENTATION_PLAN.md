# Milestone 1 — Afterlight

The full product specification is [PRD.md](PRD.md). This task implements only the manually authored rhythm runner.

## Proposed architecture

- `Domain`: Unity-independent timing, session state, immutable level geometry, tuning, semantic input frames, fixed-step movement and swept collisions. The same simulation runs in tests and the player. One jump movement strategy is implemented behind a small movement-mode interface.
- `Runtime/Audio`: a scheduled DSP transport. Simulation and visual timelines sample one song position. Restart resets the transport, simulation, input queue and transient presentation together. Pause preserves song position and reschedules from that position.
- `Runtime/Input`: platform adapters translate keyboard/mouse or touch into semantic actions. No device APIs enter movement code.
- `Runtime/World`: renders authored collision data with readable platforms and spikes. Decoration never writes to simulation geometry.
- `Runtime/Visuals`: theme, visual event track, quality profile and directors coordinate background, colour, geometry accents, camera, glow, particles and trail from song time. Event evaluation is seekable and repeatable.
- `Runtime/UI`: resolution-independent canvas, safe-area layout, HUD, title/pause/results/settings panels and a toggleable diagnostic overlay.
- `Editor`: reproducible scene/audio setup and Windows build command.
- `Tests`: a dependency-free .NET runner compiles the actual domain source and checks clock, state, input, collisions, deterministic replays, restart and chart playability. A player verification mode exercises the real audio transport and renderer.

Unity 2022.3.12f1 is installed locally and is the initial pinned editor. Use the built-in renderer, a small original unlit shader and Unity UI; no asset-store or Windows-only runtime libraries.

## Android compatibility

The domain is ordinary C#. Touch can send the same PrimaryAction; the initial adapter demonstrates that boundary without claiming an Android release. Canvas scaling, safe-area handling and a minimum forward camera view allow different aspect ratios. Quality changes affect decoration only. Display operations live in a dedicated platform service. Audio is an imported PCM asset with no desktop file-path assumptions in the player.

## Implementation sequence

1. Save/read the complete PRD and establish project configuration.
2. Implement the clock, authored 96-beat/45-second chart and deterministic simulation; test an explicit input replay.
3. Integrate scheduled music playback, input, session transitions, instant retry and desktop display settings.
4. Compose the original soundtrack and build the Afterlight visual identity, including the coordinated beat-48 drop.
5. Add usable menus, controls, debug information and quality controls.
6. Run domain tests, compile/build Unity, exercise player behaviour, inspect rendered frames and document evidence and remaining limitations.

## Scope boundary

No song analysis, song browser, procedural gameplay, procedural visual generation, advanced movement modes, battle, accounts or online features. Musical timings and geometry are manually authored. Synthesising the supplied original soundtrack during editor setup is an asset creation step, not analysis or procedural chart generation.

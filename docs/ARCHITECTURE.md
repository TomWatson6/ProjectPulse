# Milestone 1 architecture

The authored runner architecture below remains intact. The additive Audio Laboratory is documented in [AUDIO_LAB.md](AUDIO_LAB.md), with its pre-implementation preservation boundary in [AUDIO_LAB_PLAN.md](AUDIO_LAB_PLAN.md). `Pulse.Music` owns the Unity-independent data/DSP/cache/source contracts; a separate laboratory controller owns imported clips, worker jobs, UI and transport. The original 240 Hz runner, authored chart, clock and rendering path are unchanged. Only an explicit input-ownership gate, title entry, additive transport methods and separate visual sandbox entry point connect the systems.

The product source of truth is [PRD.md](PRD.md). [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) records the proposal made before implementation.

## Ownership

`PulseGame` creates the services and coordinates session transitions. `RunnerSimulation` owns movement and collisions. `SongTransport` owns playback. `PulseHud` owns interface construction and display. `VisualDirector` composes visual state from a seekable event track, then delegates to `EnvironmentDirector`, `LevelRenderer`, `CameraDirector` and `ParticleField`.

`Pulse.Domain` has `noEngineReferences: true`. It contains the immutable level definition, tuning, input timestamps, clock mapping, session state, movement strategy and collision routines. The standalone test runner compiles these same sources, without stubs for Unity. `IMovementMode` is the single extension point for future movement styles; only jumping exists now.

`LevelDefinition.Afterlight` is an explicit authored score, not a procedural generator. The listed jump beats are both reference replay input and visual takeoff markers. Floor/platform/spike geometry is fixed at load. The reference replay is restricted to the opt-in verification path.

## Timing and movement

The imported, preloaded, decompressed PCM clip is scheduled using `AudioSource.PlayScheduled`. `SongClock` maps the same `AudioSettings.dspTime` epoch to song position. The project requests a 256-sample DSP buffer: the default 1,024-sample buffer caused clock updates to be too coarse for this rhythm runner. Its scheduled lead is the larger of 60 ms and the current total audio buffer duration plus 15 ms. Actual buffer size remains device dependent.

Gameplay advances in integer ticks of 1/240 second until it reaches the last full tick before song position. Horizontal position is derived from tick time rather than accumulated floating-point displacement. Vertical movement integrates a constant-acceleration trajectory. Collisions use the fixed simulation, independent of rendering and Unity physics. Downward platform contacts are swept; spikes use continuous separating-axis collision against their actual triangle. Platform sides and undersides are solid.

Input adapters emit semantic `GameplayActions`. A press is assigned to a timestamped simulation tick; an input received after a frame stall cannot be applied to an earlier catch-up tick. Jump buffering and coyote time are expressed in simulation ticks. Rendered player motion interpolates between the two latest states, adding at most one simulation tick of visual latency. The visual timeline evaluates absolute song time directly.

Pause stops playback and freezes the song clock at the authoritative simulation position. Resume reschedules playback from that exact PCM sample position. Restart stops the old source, resets sample position, clock epoch, simulation, input queue, reference replay index, trail, particles and visual beat state, then schedules a new start. No scene reload or audio decode occurs on retry.

A stall over 250 ms pauses at the last simulated position. Audio-device changes and focus loss pause for an explicit resume. Device changes can invalidate hardware latency assumptions; the transport recalculates its scheduling lead.

The debug overlay separates simulation-minus-DSP and reported PCM-minus-DSP differences. Neither metric is a measurement of acoustic latency at the listener's ear. Device buffers, Bluetooth, display scanout and input-device latency still need hardware calibration. Replay determinism is established for the tested runtime/tuning; cross-architecture bit-identical floating-point behaviour is not claimed.

## Presentation

The visual event track has an arrival, flow, build, one-beat breath, beat-48 drop, drift and outro. It is evaluated by position, so seeking and restarting do not depend on previously fired callbacks. The drop combines score arrangement, a palette transition, orbital rings, sparks, waveform intensity and a small camera expansion. Gameplay geometry never changes with quality or visual events.

Visuals use three reusable dynamic mesh batches, a small vertex-colour shader, analytical radial glow, layered parallax scenery and a fixed particle pool. There are no per-obstacle MonoBehaviours, dynamic rigidbodies, real-time lights or material instances. Geometry is culled to the camera's horizontal range. Mesh lists retain their capacities; HUD strings refresh at 20 Hz. Quality tiers scale rings, scenery layers, dust, particles and glow. This is an initial glow treatment, not a post-processing bloom stack.

The player's outer shell is bevelled and animates in flight; its collision body remains the fixed axis-aligned dimensions in tuning. Coral hazards retain the same shape and high-contrast edge in every visual phase. Cyan takeoff markers carry gameplay guidance independently of expensive glow.

## Platforms and UI

Unity runtime code uses platform-neutral APIs. `DesktopInputProvider` and `TouchInputProvider` translate devices into the same primary action. `DisplaySettings` isolates desktop window management. The Canvas uses a 1600×900 reference size and safe-area anchors. Camera framing preserves a useful forward view across landscape aspect ratios. No audio-loading path assumes a Windows filesystem.

The desktop implementation includes windowed and borderless modes. Exclusive fullscreen, arbitrary resolution menus, configurable VSync/frame limits, bindings, full touch UX and Android builds remain future work. The settings service is the boundary for extending those options.

## Scope and assets

The original score is rendered during editor preparation and imported as a WAV. This is offline creation of one authored asset, not runtime song generation. Everything in the game presentation is original code/geometry; the only font is Unity's bundled runtime font.

There is no arbitrary song import or analysis, pattern generation, procedural visual generation, battle, multiplayer, accounts, advanced movement, production song library or practice checkpoints in this milestone.

## Engine references

The implementation follows Unity's primary API references for [scheduled playback](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSource.PlayScheduled.html), [DSP time](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSettings-dspTime.html), and [resolution/display mode changes](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Screen.SetResolution.html).

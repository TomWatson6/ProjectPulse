# Project Pulse — Afterlight

A Unity/C# rhythm runner built around **music, movement and spectacle**. Milestone 1 is a manually authored, 45-second journey with an original electronic score at 128 BPM.

## Play

If the Windows build has been generated, open **`Builds/Windows/Project Pulse.exe`**. Keep its accompanying files and `Project Pulse_Data` folder together. Select **Begin Run**, or press Space. Your first obstacle arrives after eight beats; the small light markers indicate intended takeoff positions.

In Unity Hub, add this folder as a project and open it with **Unity 2022.3.12f1**. Open `Assets/Pulse/Scenes/Afterlight.unity` and press Play. If the scene or score has not been generated yet, use **Project Pulse > Prepare vertical slice**; preparation also runs automatically on the first interactive editor load. Use **Project Pulse > Build Windows player** to rebuild.

The score is included as an imported PCM WAV. Its original synthesis/composition source is `Assets/Pulse/Editor/AfterlightComposer.cs`; no music download, account, paid asset or external service is needed. Compilation and the first audio preparation can take a little time. Retrying during gameplay reuses all loaded assets.

## Controls

| Action | Control |
| --- | --- |
| Begin / jump / retry after death | Space, Up Arrow, or left click outside a UI button |
| Restart from the beginning | R |
| Pause / resume / close settings | Escape |
| Borderless fullscreen / windowed | F11, or Settings > Display |
| Toggle diagnostic overlay | F3 |
| Quit | Title screen > Quit, or Alt+F4 in the Windows player |
| Future touch action | A touch beginning outside UI maps to the same PrimaryAction |

Jump uses a press, not repeated auto-jumps while holding. There is an approximately 79 ms input buffer and 42 ms ledge grace period. The cyan platform edge is solid; coral triangles are lethal; gaps must be jumped. Raised platforms can be landed on; their sides are solid.

Borderless fullscreen is the first-run default. Display mode, music volume, visual quality and reduced motion are saved locally through PlayerPrefs. Windowed mode is resizable. `-pulse-windowed` forces a window for that launch. Settings offer Low, Medium, High and Ultra; reduced motion removes camera shake, camera punch and prominent expanding drop rings. It does not remove all rhythmic animation or glow.

Focus loss pauses an active desktop run. An audio-device configuration change also pauses, so the player can explicitly resume with a new audio schedule. A frame stall longer than 250 ms pauses at the last simulated position.

## Build and test

The dependency-free domain test runner compiles the actual C# simulation files:

```powershell
dotnet run --project Tests/Pulse.Domain.Tests.csproj --configuration Release
```

The local harness targets the installed .NET 7 SDK. This is a test-host choice; Unity uses its own compiler and .NET Standard API profile. There are no third-party NuGet test packages.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.12f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath $PWD.Path -executeMethod Pulse.Editor.PulseBuild.Windows -logFile "$PWD/unity-build.log"
```

For the actual player smoke test, use `scripts/Verify-Player.ps1`. It runs a deliberately enabled reference input replay, checks real playback and session behaviour, switches display modes and captures frames. The normal game does not use automated inputs. See `docs/VALIDATION.md` for measured results and limits.

## Where to tune

| File | Responsibility / current defaults |
| --- | --- |
| `Assets/Pulse/Domain/RunnerTuning.cs` | 240 Hz simulation; 9 units/s speed; 12.8 units/s jump; gravity 36; collision half-size 0.30 × 0.34; 19 buffer ticks; 10 coyote ticks |
| `Assets/Pulse/Domain/LevelDefinition.cs` | Explicit jump beats, floor gaps, raised platforms and spikes; 128 BPM, 96 beats, 45 seconds |
| `Assets/Pulse/Domain/VisualEventTrack.cs` | Authored visual phases, beat-48 drop, quality budgets |
| `Assets/Pulse/Runtime/Visuals/Theme.cs` | Teal arrival and gold/violet drop palettes |
| `Assets/Pulse/Runtime/Visuals/CameraDirector.cs` | Forward view, framing, restrained drop punch |
| `Assets/Pulse/Runtime/Audio/SongTransport.cs` | DSP scheduling lead derived from the output buffer, with a 60 ms minimum |
| `ProjectSettings/AudioManager.asset` | 256-sample requested DSP buffer for responsive timing; actual size is device dependent |
| `Assets/Pulse/Editor/AfterlightComposer.cs` | Fixed score, instruments and mix, rendered to `Resources/Afterlight.wav` |

Changing BPM or the score requires updating the authored chart/timeline together and regenerating the WAV. Changing movement parameters requires re-running the chart replay tests; there is deliberately no procedural solvability system in this milestone.

The complete product vision is in `docs/PRD.md`. Architecture and timing details are in `docs/ARCHITECTURE.md`. The most appropriate next task is a hands-on movement/audio calibration pass on the intended PC hardware, followed by Milestone 2's expanded manually authored visual showcase.

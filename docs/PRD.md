# Project Pulse

## 1. Product Vision

Project Pulse is a highly visual music-driven 2D rhythm platformer inspired by the fundamental appeal of games such as Geometry Dash while introducing procedurally generated gameplay and audiovisual experiences based on user-supplied music.

A player imports a song. Project Pulse analyses its rhythm, structure, intensity and significant musical events, then automatically generates:

* playable geometry
* multiple difficulty variants
* movement-mode transitions
* visual themes
* environmental animation
* camera choreography
* lighting and effects
* music-reactive set pieces

The intended result is not a randomly generated obstacle course.

The goal is for every generated level to feel like a deliberately authored playable music video.

The three core pillars of Project Pulse are:

**Music. Movement. Spectacle.**

All three should receive first-class treatment throughout development.

Project Pulse must use original branding, artwork, code, UI, effects and level content.

---

# 2. Target Platforms

Primary development platform:

* Windows PC

Planned secondary platform:

* Android

PC development should lead initially because iteration, debugging and profiling are easier.

However, the architecture must be compatible with Android from the beginning.

Avoid unnecessary Windows-specific dependencies.

Android builds should be tested periodically during development rather than attempting a complete mobile port only after the PC version is finished.

---

# 3. PC Display Requirements

PC must support:

* Borderless fullscreen
* Exclusive fullscreen where appropriate
* Windowed mode
* configurable resolution
* configurable refresh-rate / frame-rate behaviour
* VSync setting
* frame-rate limit
* graphics-quality presets

Borderless fullscreen should be a primary/default display option.

Menus and gameplay must support common aspect ratios including:

* 16:9
* 16:10
* ultrawide displays where practical

Gameplay-critical information must not depend upon one fixed screen resolution.

---

# 4. Android Requirements

Android support is planned for a later release but should influence architecture from the beginning.

Android gameplay must support touch input.

The fundamental gameplay controls should remain sufficiently simple that desktop and mobile players experience equivalent mechanics.

Input must use an abstraction layer so gameplay logic does not directly depend on:

* keyboard
* mouse
* touchscreen

A generic gameplay action such as `PrimaryAction` should instead be mapped to appropriate platform controls.

Android development must account for:

* touchscreen input
* different aspect ratios
* safe areas
* display cut-outs
* variable refresh rates
* battery use
* reduced GPU capability
* reduced CPU capability
* memory limitations
* different audio-loading behaviour
* platform-specific file selection

Visual effects must therefore support scalable quality levels.

A powerful PC should be capable of significantly richer visuals than a mobile device without changing gameplay geometry.

---

# 5. Core User Experience

The target user experience is:

1. Player imports an MP3 or WAV.
2. Game analyses the song.
3. Game identifies musical structure.
4. Game generates a visual identity for the track.
5. Game generates multiple playable charts.
6. Player selects a difficulty.
7. Level begins.
8. Gameplay, animation and visual effects remain tightly synchronised with the music.
9. Player dies or completes the track.
10. Restarts are effectively immediate.
11. Generated content is cached for future play.

Initial difficulty levels:

* Easy
* Normal
* Hard
* Extreme

Future versions may expose advanced custom difficulty controls.

---

# 6. Design Principles

## Music First

Gameplay should feel authored around the song.

Major transitions, drops, choruses, breakdowns and intensity changes must influence gameplay and presentation.

---

## Movement Must Feel Excellent

Player controls are extremely simple.

Therefore every detail of movement matters.

Movement must be:

* responsive
* predictable
* deterministic
* satisfying
* readable
* consistent

Identical input against identical level geometry should result in identical behaviour.

---

## Spectacle Is a Core Feature

Visual design is not merely decorative polish applied after gameplay is finished.

Animation and spectacle are central to the product.

Levels should contain dramatic audiovisual moments synchronised with music.

The visual quality target should evoke the feeling of highly authored premium rhythm-game levels.

Visual systems should support:

* animated geometry
* music-reactive environments
* glow
* lighting
* particles
* trails
* colour animation
* shaders
* distortion
* parallax
* camera animation
* camera zoom
* controlled screen shake
* foreground animation
* background animation
* transitions
* masks
* environmental transformations
* beat pulses
* drop effects
* scripted visual sequences

Gameplay readability must always take priority over pure spectacle.

Players should die because they made a gameplay mistake, not because effects made required geometry impossible to see.

---

# 7. Audio Analysis

Audio analysis creates a reusable `SongMap`.

The SongMap should eventually contain:

* song duration
* estimated BPM
* beat timestamps
* downbeats
* subdivisions
* transient/onset timestamps
* intensity curve
* frequency-energy information
* section boundaries
* silence/low-energy periods
* builds
* drops
* likely verses
* likely choruses
* major musical transitions

Analysis should be deterministic and cached.

Example:

`MySong.mp3`

produces:

`MySong.songmap.json`

Language models should not be responsible for precise collision geometry.

Objective signal analysis provides timing information to procedural systems.

AI systems may later assist with higher-level creative interpretation.

---

# 8. Song Structure

The system should attempt to convert the SongMap into higher-level musical sections.

For example:

INTRO
VERSE
BUILD
PRE_DROP
DROP
BREAKDOWN
CHORUS
OUTRO

These labels do not need to be perfectly musically academic.

They exist to give the Level Director and Visual Director meaningful context.

A DROP should be treated differently from an INTRO even when both contain beats.

---

# 9. Procedural Gameplay Generation

The generator should use reusable gameplay Patterns rather than randomly scattering obstacles.

A Pattern contains metadata such as:

* compatible movement modes
* musical duration
* difficulty range
* required inputs
* entry state
* exit state
* compatible speeds
* obstacle geometry
* optional interactive elements
* compatible visual events

Example patterns:

SINGLE_JUMP
DOUBLE_JUMP
STAIRCASE
ORB_CHAIN
GRAVITY_SWITCH
FLIGHT_TUNNEL
WAVE_CORRIDOR
DROP_SEQUENCE
SYNCOPATED_JUMP_CHAIN

The Level Director selects patterns according to:

* SongMap
* musical section
* intensity
* current movement mode
* difficulty
* recent gameplay
* future transitions
* generation seed

Patterns should form larger gameplay phrases.

The generator should create convincing intentional progression rather than visible procedural randomness.

---

# 10. Difficulty Generation

Every song should support different gameplay difficulties.

Difficulty must not simply increase scroll speed.

A `DifficultyProfile` should configure factors including:

* obstacle density
* timing tolerance
* rhythmic subdivision
* reaction windows
* gap tolerance
* pattern complexity
* movement speed
* orb frequency
* portal frequency
* gravity changes
* movement-mode changes
* consecutive-input density
* visual distraction
* recovery opportunities

Easy should still feel tightly connected to the song.

Extreme should feel substantially more demanding without becoming random or unfair.

Different difficulties should retain recognisable relationships to important musical events.

A huge drop should still feel like a huge drop on Easy.

---

# 11. Guaranteed Solvability

Every generated chart must be validated.

The validator should identify:

* impossible jumps
* unreachable platforms
* impossible transitions
* impossible movement-mode changes
* overlapping lethal geometry
* insufficient reaction windows
* invalid portals
* invalid spawn positions
* accidental unavoidable deaths

Preferably implement a deterministic Player Simulation capable of evaluating generated routes.

Invalid sections should be regenerated automatically.

---

# 12. Visual Director

The Visual Director is a major subsystem.

It receives information from the SongMap and Level Director and produces the audiovisual presentation of the level.

Gameplay geometry and visual decoration should remain logically separate.

The same playable chart should theoretically be capable of being rendered using completely different visual themes.

The Visual Director should control:

* palette
* environment
* lighting
* glow
* particles
* background
* foreground
* parallax
* geometry animation
* camera
* shader parameters
* environmental transitions
* beat-reactive effects
* drop sequences

---

# 13. Visual Event Timeline

Generated levels should contain a Visual Event Track alongside gameplay geometry.

Example:

Beat 64:

* begin background pulse

Beat 72:

* gradually shift palette toward red

Beat 78:

* reduce scene brightness

Beat 80:

* camera zoom begins

Beat 84:

* DROP
* palette transition
* camera punch
* particle burst
* background transformation
* geometry glow increase
* speed transition
* screen shake

This allows spectacle to be generated intentionally rather than being implemented as arbitrary global effects.

---

# 14. Visual Themes

The game should support reusable Theme definitions.

Possible themes include:

* Synthwave
* Cyber
* Neon
* Space
* Industrial
* Inferno
* Dreamscape
* Digital
* Monochrome
* Retro Arcade
* Alien
* Futuristic City

Themes define an aesthetic vocabulary.

Individual generated levels should still vary significantly within a theme.

Eventually themes may contain multiple:

* backgrounds
* palettes
* shaders
* particle sets
* geometry styles
* animation styles
* transition styles
* environmental props

---

# 15. Visual Quality Tiers

Visual presentation must scale according to device capability.

Suggested presets:

LOW
MEDIUM
HIGH
ULTRA

Ultra PC presentation may use substantially more:

* particles
* bloom
* trails
* layered backgrounds
* distortion
* shader complexity
* lighting
* animated elements

Mobile versions may reduce these without altering gameplay.

The gameplay timeline must never depend upon expensive visual effects being enabled.

---

# 16. Core Gameplay Mechanics

Initial gameplay:

* automatic horizontal movement
* jumping
* collision/death
* platforms
* spikes
* instant restart
* practice checkpoints

Long-term mechanics may include original implementations of concepts such as:

* standard jumping
* hold-to-fly movement
* gravity movement
* wave-like movement
* hopping movement
* charged jumping
* teleporting movement
* gravity portals
* speed changes
* movement-mode portals
* jump pads
* interactive orbs
* moving geometry
* disappearing geometry
* dual-player sections
* camera transformations

Movement-mode architecture must be extensible.

---

# 17. Input System

Gameplay code should understand semantic actions rather than physical devices.

Example:

PrimaryAction
Pause
Restart

PC bindings may include:

PrimaryAction:

* Space
* Left Mouse
* Up Arrow

Mobile:

PrimaryAction:

* Touch

This architecture allows PC and Android to share the same gameplay implementation.

---

# 18. Song Library

PC MVP should support a configurable song folder.

Dropping supported files into this directory should make them discoverable automatically.

Initial formats:

* MP3
* WAV

The library displays:

* title
* duration
* BPM
* analysis status
* generated difficulties
* completion percentage
* best performance
* generation seed

Android will require a platform-appropriate song-import/file-selection workflow rather than assuming desktop filesystem behaviour.

---

# 19. Persistence

Persist:

* imported songs
* SongMaps
* generated levels
* generation seeds
* difficulty results
* player settings
* visual themes
* completion percentages
* best attempts

Audio-file hashes should identify cached analysis.

Do not repeatedly analyse unchanged songs.

---

# 20. PC Settings Menu

Provide settings for:

## Display

* Borderless Fullscreen
* Exclusive Fullscreen
* Windowed
* Resolution
* Refresh Rate
* VSync
* Frame Limit

## Graphics

* Quality preset
* Particle quality
* Post-processing quality
* Bloom
* Screen shake
* Background detail
* Shader quality

## Gameplay

* input bindings
* practice settings
* restart behaviour

## Audio

* master volume
* music volume
* effects volume

---

# 21. Future Battle Mode

Battle Mode displays simultaneous gameplay tracks.

Upper half:

AI opponent

Lower half:

Player

Both progress through the same song.

The AI should emulate human performance rather than play perfectly.

`AISkillProfile` may define:

* timing accuracy
* mistake probability
* reaction ability
* recovery ability
* consistency
* aggression

The AI should occasionally make believable mistakes.

Players may collect battle attacks.

Potential effects:

* temporary visual interference
* temporary increased speed
* gravity disruption
* mirrored presentation
* controlled screen flashes
* environmental disruption
* false visual geometry
* short visibility reductions

Attacks must remain fair and must not produce unavoidable deaths.

Battle Mode is outside the initial MVP.

---

# 22. Technical Architecture

Target engine:

Unity / C#

Suggested modules:

Core/

* GameManager
* GameClock
* GameState
* PlatformCapabilities

Input/

* InputManager
* GameplayActions
* DesktopInputProvider
* MobileInputProvider

Player/

* PlayerController
* MovementMode
* Movement implementations

Audio/

* SongLibrary
* SongLoader
* AudioAnalyzer
* SongMap
* BeatClock
* SongStructureAnalyzer

Generation/

* LevelDirector
* Pattern
* PatternLibrary
* DifficultyProfile
* LevelDefinition
* GenerationSeed

Validation/

* LevelValidator
* PlayerSimulation

World/

* LevelRenderer
* Platform
* Obstacle
* Portal
* GameplayTrigger

Visuals/

* VisualDirector
* VisualEventTrack
* Theme
* ThemeLibrary
* MusicReactiveEffect
* CameraDirector
* EnvironmentDirector

Rendering/

* GraphicsQualityProfile
* PlatformGraphicsProfile

Persistence/

* SaveManager
* SongCache

UI/

* MainMenu
* SongSelection
* DifficultySelection
* Settings
* Results

Battle/

* BattleManager
* AIPlayer
* AISkillProfile
* BattlePowerup

Keep domain logic independent from Unity scene objects wherever practical.

Generation, audio interpretation and validation should be testable without requiring a scene to be running.

---

# 23. MVP

The first MVP demonstrates:

* Windows PC build
* borderless fullscreen
* windowed mode
* one movement mode
* automatic scrolling
* responsive jumping
* platforms
* spikes
* death
* near-instant restart
* song playback
* extremely accurate gameplay/audio synchronisation
* basic music-reactive background
* beat-reactive animation
* basic particle/glow presentation
* one imported song
* basic beat detection
* procedurally selected obstacle patterns
* Easy and Normal difficulty
* deterministic generation
* basic solvability validation

It explicitly excludes:

* full Android release
* Battle Mode
* online multiplayer
* leaderboards
* user accounts
* advanced AI generation

However, the architecture must remain Android-compatible.

---

# 24. Development Milestones

## Milestone 1 — Rhythm Runner

Build an excellent manually authored rhythm-platforming vertical slice.

Prove:

* movement
* collision
* jumping
* camera
* audio synchronisation
* restart
* PC display modes
* basic visual animation

The game should already feel satisfying.

---

## Milestone 2 — Visual Prototype

Before procedural level generation becomes complicated, prove that Project Pulse can look exceptional.

Create one manually authored showcase section demonstrating:

* animated geometry
* beat-reactive lighting
* particles
* background animation
* camera choreography
* glow
* trails
* transitions
* a dramatic musical drop

This milestone establishes the visual quality bar for the project.

---

## Milestone 3 — Audio Laboratory

Import arbitrary songs and construct SongMaps.

Create debug visualisation for:

* beats
* onsets
* intensity
* song sections
* drops

Humans should be able to inspect whether analysis matches the song.

---

## Milestone 4 — Procedural Generator

Generate gameplay using Pattern definitions.

Start conservatively with simple, reliable geometry.

---

## Milestone 5 — Difficulty Generation

Generate:

* Easy
* Normal
* Hard
* Extreme

for the same music.

---

## Milestone 6 — Procedural Visual Director

Turn analysed musical structure into:

* themes
* animations
* visual events
* transitions
* drop sequences
* environmental choreography

This milestone is critical.

Generated levels should begin to resemble deliberately authored audiovisual experiences.

---

## Milestone 7 — Gameplay Expansion

Add additional movement modes, portals, orbs, pads and richer gameplay Patterns.

---

## Milestone 8 — Android Compatibility Pass

Create a fully playable Android build.

Implement:

* touch controls
* Android song import
* responsive UI
* safe areas
* mobile graphics profiles
* mobile performance profiling

Gameplay behaviour must remain equivalent to PC.

---

## Milestone 9 — Production UX

Implement:

* polished menus
* song browser
* caching
* progress
* practice mode
* options
* statistics
* accessibility settings

---

## Milestone 10 — Battle Mode

Implement:

* split-screen battle presentation
* AI opponent
* AI mistakes
* powerups
* attacks
* battle scoring

---

# 25. Success Criteria

Project Pulse has achieved its core vision when a player can import a previously unseen song, choose a difficulty, and receive an experience that:

* is accurately synchronised to the music
* is guaranteed to be playable
* appropriately matches requested difficulty
* uses musical structure intelligently
* contains meaningful gameplay progression
* contains dramatic visual progression
* reacts convincingly to drops and transitions
* looks intentionally designed
* feels intentionally designed
* remains readable despite visual spectacle
* can be reproduced using its generation seed
* runs smoothly on appropriate PC hardware
* can run on Android with appropriately scaled presentation

The ultimate standard is:

A player should occasionally forget that the level was procedurally generated.

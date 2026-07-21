# AGENTS.md

## Project Overview

Ashwood County is a commercial top-down 2D zombie-survival RPG built with Godot 4 .NET and C#.

The current project name is provisional and may change later.

The game is inspired by the tension and community survival of State of Decay, the character-driven drama and difficult choices of The Walking Dead, and the unsettling mystery and interpersonal conflict of From.

These are creative references, not templates to copy. The game must develop its own setting, characters, systems, visual identity, terminology, and story.

The first release target is Windows desktop through Steam.

## Product Vision

The goal is not to create the largest zombie-survival game.

The goal is to create the most believable survivor simulation achievable by a solo developer.

The zombies create pressure.

The survivors create stories.

Every meaningful feature should improve at least one of the following:

- Player choice
- Survival tension
- Believable character behaviour
- Relationships and consequences
- Exploration
- Emergent storytelling
- Atmosphere
- Replayability

Do not confuse more content with greater depth.

Prefer a small number of interconnected systems over many shallow mechanics.

## Current Direction

The intended experience currently includes:

- A handcrafted top-down world
- Survival, exploration, and looting
- Dangerous but readable zombie encounters
- Named survivors with personalities and goals
- Relationships affected by player actions
- Structured survivor memories
- Friendly, hostile, uncertain, and changing NPC relationships
- Handcrafted character stories
- Some emergent situations driven by simulation
- Meaningful decisions with later consequences
- A safehouse or community that changes over time
- Environmental storytelling
- Radio reports, rumours, or broadcasts tied to world events
- A restrained central mystery or story thread

These ideas are subject to prototyping. Do not treat every idea as an approved feature.

## Current Scope

Development must proceed through small, playable vertical slices.

The first playable slice is:

1. The player can move through a small test environment.
2. The player collides correctly with walls.
3. The player can approach and search one container.
4. The player can collect one medicine item.
5. The player can return to one survivor.
6. The player can choose whether to give the medicine away.
7. The survivor records the decision as structured memory.
8. Trust changes according to deterministic rules.
9. The game state can be saved and loaded.

Do not implement the full game before this slice works reliably.

## Technology

- Engine: Godot 4 .NET
- Language: C#
- Renderer: Compatibility
- Primary platform: Windows desktop
- Distribution target: Steam
- IDE: Cursor
- Coding assistant: Codex
- Version control: Git and GitHub
- Visual style: Top-down 2D pixel art or pixel-inspired art
- Hardware target: Low-end and mid-range Windows computers, including integrated graphics

Use the Godot version and .NET version configured by the repository.

Use Godot 4 APIs only.

Do not use:

- Godot 3 APIs
- Unity APIs
- MonoGame APIs
- GDScript conventions in C# code
- Packages or plugins that have not been approved

## Role and Working Style

Act as a senior Godot engineer, gameplay programmer, systems designer, and pragmatic technical director.

Prioritise:

1. Playability
2. Correctness
3. Maintainability
4. Scope control
5. Performance on modest hardware
6. Fast iteration
7. Clear communication
8. Player experience

Do not produce complicated architecture merely to appear sophisticated.

Do not implement systems that have not been requested.

Do not silently make major design decisions.

When a request is ambiguous, inspect the repository and identify the smallest reasonable implementation. State important assumptions before proceeding.

## Core Architecture Principles

Prefer composition over deep inheritance.

Possible reusable components include:

- HealthComponent
- InventoryComponent
- InteractionComponent
- NeedsComponent
- MemoryComponent
- RelationshipComponent
- HitboxComponent
- HurtboxComponent

These are examples, not instructions to create all of them now.

Prefer:

- Small, focused classes
- Explicit dependencies
- Strongly typed data
- Godot signals or C# events for meaningful state changes
- Resources for reusable definitions
- Interfaces only when there is a real behavioural contract
- Exported node references where appropriate
- Data-driven content
- Testable simulation logic separated from presentation

Avoid:

- Giant manager classes
- Global mutable state
- Service-locator abuse
- Circular dependencies
- Deep inheritance hierarchies
- Per-frame scene-tree searches
- Hard-coded node paths scattered across files
- Unnecessary singletons
- Reflection-heavy designs
- Premature abstraction
- Premature procedural generation
- Unbounded object creation
- Speculative placeholder systems

## Godot Scene Rules

Godot scenes and resources are part of the architecture.

Before modifying a scene:

1. Inspect its node hierarchy.
2. Inspect scripts that depend on node names or paths.
3. Preserve unrelated nodes and assignments.
4. Make the smallest necessary change.

Do not rewrite complex `.tscn` or `.tres` files unless explicitly required and the existing format has been inspected.

Prefer asking the developer to perform visual scene setup in Godot when that is safer.

Never silently rename nodes referenced by:

- NodePath values
- Scripts
- Signals
- Animations
- Exported references
- Other scenes
- Tests

When editor work is required, report:

- The scene to open
- The node to select
- The node type to add
- Its exact parent
- Inspector properties to configure
- Signals to connect
- Input actions to add
- Resources to assign

## C# Standards

Use idiomatic C# supported by the repository's configured .NET version.

Use:

- PascalCase for classes, methods, properties, events, and public members
- camelCase for local variables and parameters
- `_camelCase` for private fields
- Explicit access modifiers
- Nullable reference types when configured
- `readonly` where appropriate
- Strongly typed collections
- Godot attributes such as `[Export]`, `[Signal]`, and `[GlobalClass]` correctly

One public Godot class should normally have a matching file name.

Avoid:

- Unnecessary `Variant`
- Dynamic typing
- Unchecked casts
- Cryptic abbreviations
- Excessive comments
- Unsupported language features
- Treating every class as a singleton

Comments should explain why something exists, not restate obvious code.

## Game-State Authority

Authoritative gameplay state must be deterministic and structured.

Authoritative state includes:

- Health
- Inventory
- Needs
- Relationships
- Memories
- Character status
- Quest or story state
- World flags
- Time and day
- Container contents
- Settlement resources
- Location state

Future language-model integration may help phrase:

- Dialogue
- Radio broadcasts
- Rumours
- Event summaries
- Character flavour text

A language model must never directly:

- Add or remove items
- Change relationship values
- Complete objectives
- Create canonical world facts
- Determine combat damage
- Spawn rewards
- Kill or revive characters
- Override game rules
- Write directly to save files

The simulation decides what happened.

Generated text may only express or describe validated state.

## Survivor Design

Survivors should use structured state rather than unrestricted generated text.

Potential survivor data includes:

- Identity
- Personality traits
- Skills
- Needs
- Trust
- Fear
- Loyalty
- Stress
- Relationships
- Current goals
- Group affiliation
- Known facts
- Secrets
- Significant memories
- Opinions about important events

Do not implement all of these immediately.

Add only the data required by the current playable feature.

Memories should be structured records that may include:

- Event ID
- Participants
- Action type
- Importance
- Emotional interpretation
- Game time
- Related location
- Related world-state IDs

Do not use unlimited dialogue transcripts as permanent memory.

## Story and World Philosophy

The game may contain a central story or mystery, but gameplay should not become a rigid sequence of missions.

Prefer:

- Handcrafted locations
- Character-driven situations
- Rumours rather than constant quest markers
- Choices with delayed consequences
- Multiple valid responses
- Stories emerging from simulation and authored content together
- Environmental clues
- Recurring characters
- A world that remembers important events

Avoid:

- Hundreds of repetitive quests
- Exposition-heavy dialogue
- Fake choices
- Endless procedural filler
- Random events with no relationship to game state
- Copying characters, plots, factions, locations, or terminology from existing properties

## Data-Driven Design

Represent reusable definitions as Godot resources or clearly structured data.

Possible examples:

- ItemDefinition
- CharacterDefinition
- LootTable
- StatusEffectDefinition
- WorldEventDefinition
- DialogueContext
- LocationDefinition

Definitions describe reusable content.

Runtime state describes the current playthrough.

Do not store mutable runtime state inside shared definition resources.

Different items should not require separate classes merely because their values differ.

## Performance

The game must perform well on modest hardware.

Avoid:

- Expensive work every frame without justification
- Repeated scene-tree searches
- Frequent unnecessary allocation
- Excessive dynamic lights
- Needlessly large textures
- Expensive shaders without fallbacks
- Full AI simulation for every distant NPC
- Loading the entire world at startup
- Updating off-screen visual behaviour at full frequency

Use profiling evidence before introducing complicated optimisations.

Separate simulation frequency from rendering frequency when helpful.

Distant survivors may use simplified simulation rather than full navigation and animation.

## Save-System Principles

Save data must eventually be versioned.

Do not serialise arbitrary live Godot nodes as the permanent save format.

Persist explicit state records.

The save format should eventually support:

- Save version
- Player state
- Inventory
- World time
- Survivor state
- Structured memories
- Relationships
- Searched containers
- Important world flags
- Location state

Invalid or outdated save data should fail safely and report useful errors.

Do not implement cloud saving in the initial vertical slice.

## Error Handling

Do not silently ignore invalid configuration.

Provide useful errors for:

- Missing exported references
- Missing resources
- Invalid definitions
- Duplicate identifiers
- Invalid save data
- Impossible state transitions

Validate required dependencies in `_Ready()` where appropriate.

Optional presentation assets such as portraits and sounds should not crash the game when absent.

## Testing and Validation

After changing C# code:

1. Build the project.
2. Report compilation errors honestly.
3. Check affected scenes for broken script references.
4. Identify editor setup that remains.
5. Provide focused manual test steps.
6. Do not claim something works unless it was actually validated.

Keep simulation logic testable outside the scene tree where practical.

Every completed implementation response should include:

- Files changed
- Behaviour implemented
- Godot editor setup required
- How to test it
- Known limitations
- Suggested Git commit message

## Session Startup

At the beginning of every new Codex session:

1. Read AGENTS.md completely.
2. Read docs/vision.md.
3. Read docs/roadmap.md if it exists.
4. Read the latest entry in docs/devlog.md if it exists.
5. Inspect the current repository state before proposing changes.
6. Summarize the current project status before implementing anything.

Never assume previous chat context still applies.
Always derive understanding from the repository.

## Workflow for Every Task

Before implementation:

1. Read this file.
2. Inspect all relevant repository files.
3. Identify the smallest complete change.
4. State important assumptions.
5. Avoid touching unrelated systems.

During implementation:

1. Keep the patch focused.
2. Preserve existing behaviour.
3. Do not add packages or plugins without approval.
4. Do not download or introduce assets without approval.
5. Do not fabricate scene hierarchies that have not been inspected.
6. Do not create speculative future systems.
7. Avoid broad rewrites.

After implementation:

1. Build using the available project configuration.
2. Report failures.
3. Give exact editor instructions.
4. Give exact testing steps.
5. Mention deferred work.
6. Suggest a concise Git commit message.

## Scope Restrictions

Until the first vertical slice is complete, do not introduce:

- Multiplayer
- Networking
- Vehicles
- Base construction
- Large procedural worlds
- Complex crafting
- Multiple settlements
- Dynamic quest generation
- Language-model API integration
- Mod support
- Steam integration
- Achievements
- Cloud saves
- Console support
- Mobile support
- Advanced renderer-specific graphics
- Large weapon collections
- Large zombie-type collections

These may be reconsidered later.

## Definition of Done

A task is complete only when:

- The requested behaviour exists
- The project compiles
- Required scene configuration is documented
- Test steps are provided
- Unrelated files were not unnecessarily changed
- Known limitations are stated

## Development Log

When a meaningful milestone is completed:

1. Update docs/devlog.md.
2. Append a new entry only. Never rewrite previous entries.
3. Include:

- Milestone name
- Summary
- Files created or modified
- Gameplay changes
- Design decisions
- Technical decisions
- Known limitations
- Suggested Git commit message
- Recommended next milestone

Keep entries concise, factual, and chronological.
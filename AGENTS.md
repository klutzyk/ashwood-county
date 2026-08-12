# AGENTS.md

## Project

Ashwood County is a modern 2D isometric zombie-survival city builder built with Godot 4.x .NET and C#.

The game combines ideas from:

- RimWorld
- Manor Lords
- Age of Empires
- zombie survival games

The player begins with a small group of survivors and gradually establishes, expands and defends a settlement while reclaiming Ashwood County from the infected.

This is a brand-new project. Do not assume architecture or implementation details from previous Ashwood County prototypes.

## Core Direction

The game uses:

- Godot 2D
- isometric presentation
- C#
- real-time simulation
- pause and simulation-speed controls
- survivor selection and orders
- autonomous survivor jobs
- resource gathering
- building construction
- settlement management
- zombie threats and hordes
- exploration
- survivor progression and RPG systems

The visual direction is detailed modern isometric artwork.

Do not convert the project into a full 3D game.

## Development Principles

- Build actual playable systems rather than unnecessary infrastructure.
- Keep systems modular and reusable.
- Preserve existing working functionality.
- Prefer simple foundations that can be expanded later.
- Avoid premature abstraction.
- Avoid speculative systems that are not currently required.
- Keep visual assets replaceable.
- Use placeholder art when necessary rather than blocking gameplay development.
- Do not download assets without explicit permission.
- Do not add external dependencies without a good reason.
- Use C# for gameplay code.
- Current user instructions override outdated documentation.

## Repository Safety

Before changing files:

1. Inspect Git status.
2. Preserve unrelated user work.
3. Understand relevant existing code before replacing it.

Do not:

- reset the repository
- discard unrelated changes
- push without permission
- commit unless explicitly requested

## AI Development

Coding agents may:

- inspect the repository
- implement systems
- create scenes
- create project-owned placeholder assets
- refactor weak code when justified
- run and test the project
- use sub-agents when useful

Do not waste substantial execution time on:

- repeatedly auditing the entire repository
- rewriting documentation instead of implementing the game
- excessive benchmark infrastructure
- unnecessary validation scenes
- repeatedly planning without implementation

When visual assets are required, assume they may later be replaced by externally generated artwork.

Build systems so artwork can be swapped without rewriting gameplay logic.

## Current Development Stage

The first objective is to prove the isometric game foundation.

Initial development order:

1. Isometric world representation
2. Camera pan and zoom
3. Correct isometric positioning and sorting
4. Survivor selection and movement
5. Resource gathering
6. Building placement and construction
7. Basic zombie behaviour
8. Day/night survival loop

Do not implement large late-game systems until these foundations work.

## Quality

A feature is not complete merely because it compiles.

Where relevant, verify:

- behaviour in the running game
- camera interaction
- input
- visual sorting
- navigation
- performance
- absence of runtime errors

Keep the project playable after changes.
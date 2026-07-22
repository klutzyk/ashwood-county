# Ashwood County Development Log

## 2026-07-22

### Milestone
Project foundation complete.

### Completed
- Created Godot 4.7 .NET project
- Configured Git and GitHub
- Added project documentation
- Created first playable scene
- Implemented CharacterBody2D player
- Added four-direction movement
- Configured Input Map
- Added placeholder collision
- Added temporary player visual
- Successfully built with .NET 8

### Decisions
- Engine: Godot 4.7 (.NET)
- Language: C#
- Renderer: Compatibility
- Studio: Wayward Pixel

### Notes
Movement feels responsive.
Placeholder graphics only.
No camera, animation or world collisions yet.

### Next Milestone
- Camera follows player
- Add world boundaries
- Add first building

## 2026-07-22 - Player Camera

### Milestone
Player-following camera complete.

### Summary
Added an active Camera2D to the existing Player in the test world.

### Files Created or Modified
- Modified `scenes/world/test_world.tscn`
- Modified `docs/roadmap.md`
- Modified `docs/devlog.md`

### Gameplay Changes
- The game view now follows the Player while moving.

### Design Decisions
- Kept the camera behavior immediate and unmodified for the first camera task.

### Technical Decisions
- Parent the Camera2D to Player so it follows through scene inheritance.
- Align the camera with the existing collision shape and placeholder visual offset.

### Known Limitations
- No smoothing, zoom, limits, or screen shake.
- The test world has no environment or boundaries yet.

### Suggested Git Commit Message
`Add player-following camera`

### Recommended Next Milestone
Add basic camera smoothing.

## 2026-07-22 - Temporary Scale Sandbox

### Milestone
Temporary scale-evaluation sandbox complete.

### Summary
Created a small prototype environment for evaluating the imported 16-pixel art scale, camera framing, movement feel, and readability.

### Files Created or Modified
- Created `scenes/world/sandbox.tscn`
- Modified `docs/roadmap.md`
- Modified `docs/devlog.md`

### Gameplay Changes
- Added a 40×30-tile grass test area with a dirt path, fences, trees, rocks, one house, and one crate.
- Added minimal collision for trees, rocks, fences, and the house.
- Reused the existing Player rig with a sandbox-only scale and a Camera2D zoom of `(2, 2)`.

### Design Decisions
- Treat the scene as disposable evaluation content rather than final world design.
- Keep the imported farming art restricted to prototype use while the visual direction is evaluated.

### Technical Decisions
- Used Godot 4.7 `TileMapLayer` nodes for Ground, Decoration, and Obstacles.
- Reused the project-owned Super Retro World TileSet and existing test-world Player instance.
- Added only simple `StaticBody2D` rectangle collisions required for movement testing.

### Known Limitations
- The scene has no world boundaries and is not production content.
- Player scale and movement feel are sandbox-only starting values.
- Props use simple collision approximations and do not use Y-sorted gameplay layering.

### Suggested Git Commit Message
`Add temporary world scale sandbox`

### Recommended Next Milestone
Playtest the sandbox and record the chosen base resolution, tile size, and player scale.

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

# Ashwood County Roadmap

> This document tracks production progress.
>
> Features should be implemented as small, complete vertical slices.
> Each checkbox should ideally take between 30 minutes and one day to complete.
> Avoid implementing multiple unrelated systems in a single task.

---

# Phase 0 - Foundation

## Repository

- [x] Create Godot project
- [x] Configure Git
- [x] Create GitHub repository
- [x] Configure .gitignore
- [x] Configure Codex workflow
- [x] Create project documentation

## Player

- [x] CharacterBody2D player
- [x] Four-direction movement
- [x] Placeholder visual
- [x] Basic collision shape

---

# Phase 1 - Core Engine

## Camera

- [x] Camera2D
- [x] Camera follows player
- [ ] Camera smoothing
- [ ] Camera zoom
- [ ] Camera limits
- [ ] Camera shake framework

## World

- [ ] Configure project resolution
- [ ] Configure tile size
- [ ] Create first tileset
- [ ] Create first TileMap
- [ ] Grass tile
- [ ] Dirt tile
- [ ] Road tile
- [ ] Tree obstacle
- [ ] Basic fence
- [ ] World boundaries

## Collision

- [ ] Static walls
- [ ] Collision layers
- [ ] Collision masks
- [ ] Player cannot leave world

## Input

- [x] Movement
- [ ] Pause input
- [ ] Interaction input
- [ ] Inventory input

---

# Phase 2 - Interaction

## Interaction System

- [ ] Interaction component
- [ ] Interaction interface
- [ ] Detect nearby interactables
- [ ] Interaction prompt
- [ ] Highlight selected object

## Searchable Containers

- [ ] Searchable cabinet
- [ ] Searchable shelf
- [ ] Search animation
- [ ] Search progress bar
- [ ] Prevent movement while searching

## Items

- [ ] Item definition resource
- [ ] Medicine
- [ ] Food
- [ ] Water
- [ ] Bandage
- [ ] Ammo (placeholder)

---

# Phase 3 - Inventory

## Inventory

- [ ] Inventory component
- [ ] Inventory UI
- [ ] Item slots
- [ ] Stackable items
- [ ] Item pickup
- [ ] Item drop
- [ ] Item inspection

---

# Phase 4 - Saving

## Save System

- [ ] Save manager
- [ ] Save player
- [ ] Save inventory
- [ ] Save world state
- [ ] Save searched containers
- [ ] Load game
- [ ] Autosave

---

# Phase 5 - World

## Buildings

- [ ] House exterior
- [ ] House interior
- [ ] Pharmacy
- [ ] Police station
- [ ] Hospital
- [ ] Garage
- [ ] Church
- [ ] Barn

## Environment

- [ ] Street lights
- [ ] Trees
- [ ] Bushes
- [ ] Cars
- [ ] Barricades
- [ ] Corpses
- [ ] Environmental props

---

# Phase 6 - Survivors

## Survivor Framework

- [ ] Survivor definition
- [ ] Survivor controller
- [ ] Pathfinding
- [ ] Basic idle behaviour
- [ ] Follow behaviour
- [ ] Daily routine

## Relationships

- [ ] Trust
- [ ] Respect
- [ ] Fear
- [ ] Relationship UI

## Memory

- [ ] Structured memory model
- [ ] Record player actions
- [ ] Record conversations
- [ ] Record important events

## Dialogue

- [ ] Dialogue UI
- [ ] Dialogue choices
- [ ] Character portraits
- [ ] Branching conversations

---

# Phase 7 - Zombies

## Zombie

- [ ] Zombie definition
- [ ] Zombie movement
- [ ] Detection radius
- [ ] Chase player
- [ ] Lose player
- [ ] Attack
- [ ] Damage
- [ ] Death

## Horde

- [ ] Zombie spawning
- [ ] Horde manager
- [ ] Sound attraction
- [ ] Wandering behaviour

---

# Phase 8 - Survival

## Needs

- [ ] Hunger
- [ ] Thirst
- [ ] Fatigue
- [ ] Health
- [ ] Injury
- [ ] Infection

## Time

- [ ] Day/night cycle
- [ ] Clock
- [ ] Calendar
- [ ] Sleeping

---

# Phase 9 - Community

## Safehouse

- [ ] Storage
- [ ] Community inventory
- [ ] Survivor assignments
- [ ] Food consumption
- [ ] Medicine usage

---

# Phase 10 - Story

## Main Story

- [ ] Opening sequence
- [ ] First mystery
- [ ] First major decision
- [ ] Radio broadcasts
- [ ] Story progression

---

# Phase 11 - Polish

## Audio

- [ ] Footsteps
- [ ] Ambient sounds
- [ ] Music
- [ ] UI sounds

## Graphics

- [ ] Character animations
- [ ] Zombie animations
- [ ] Lighting
- [ ] Shadows
- [ ] Weather

## Steam

- [ ] Save icon
- [ ] Achievements
- [ ] Settings menu
- [ ] Credits

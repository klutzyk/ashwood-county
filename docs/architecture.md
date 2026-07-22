# Ashwood County Architecture

## Purpose

This document defines the long-term technical architecture of the project.


---

# Technology Stack

Engine:
- Godot 4 .NET

Language:
- C#

Renderer:
- Compatibility

Target Platform:
- Windows (Steam)

IDE:
- Cursor

AI Assistant:
- Codex

Version Control:
- Git + GitHub

---

# Development Philosophy

Prefer:

- Small systems
- Data-driven design
- Composition over inheritance
- Deterministic simulation
- Reusable components
- Simple scene hierarchies

Avoid:

- Giant manager classes
- Deep inheritance
- Global mutable state
- Premature optimization
- Feature creep

---

# Folder Structure

assets/
    audio/
    characters/
    environment/
    items/
    ui/

docs/

scenes/
    world/
    player/
    ui/
    props/
    buildings/

scripts/
    components/
    systems/
    player/
    npc/
    world/
    ui/

---

# Scene Philosophy

Every major gameplay object should be its own scene.

Examples:

Player

Zombie

Survivor

House

Cabinet

Vehicle

Scenes should remain modular.

Avoid one giant world scene.

---

# Component Philosophy

Gameplay behaviour should be split into reusable components.

Possible future examples:

HealthComponent

InventoryComponent

InteractionComponent

MemoryComponent

RelationshipComponent

NeedsComponent

These should only be created when required.

---

# World Design

The world should be primarily handcrafted.

Procedural generation may later be used for:

- Loot placement
- Survivor spawning
- Zombie spawning
- Random events

The map itself should remain handcrafted.

---

# Save System

The save system should serialize explicit game state.

Never serialize arbitrary Godot nodes.

Future save data should include:

Player

Inventory

World state

Relationships

Memories

Containers

Time

Story progress

---

# AI Philosophy

Simulation determines truth.

AI generates presentation.

For example:

Simulation decides:

- Survivor trust
- Relationships
- Memories
- World events

AI may generate:

- Dialogue
- Radio broadcasts
- Flavor text

AI should never directly modify authoritative game state.

---

# Art Pipeline

Prototype

↓

Free placeholder assets

↓

AI-generated assets

↓

Hand-polished assets (if required)

Gameplay should never wait for final art.

---

# Performance Targets

The game should comfortably run on:

- Integrated graphics
- Low-end laptops
- Mid-range gaming PCs

Performance is preferred over graphical complexity.

---

# Coding Standards

One public class per file.

Small focused classes.

No dead code.

No commented-out code.

Meaningful naming.

Minimal comments.

Use composition whenever possible.

---

# Workflow

Every feature should follow:

Design

↓

Implementation

↓

Build

↓

Playtest

↓

Commit

↓

Update devlog

Never implement multiple major systems simultaneously.

Finish one vertical slice before starting another.

---

# Definition of Ready

A feature is ready when:

- Requirements are clear
- Scope is understood
- Dependencies are complete

---

# Definition of Done

A feature is complete when:

- It builds successfully
- It functions correctly
- It is playtested
- Documentation is updated
- Dev log is updated
- Git commit is created
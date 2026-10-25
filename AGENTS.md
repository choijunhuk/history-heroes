# AGENTS.md

Instructions for AI coding agents (Codex, Claude Code, and others) working in this repository.

## Project

**History Heroes**: an Archero-style mobile roguelike where world historical figures are the playable heroes. Unity 6.3 LTS, C#, 2D URP, portrait, Android first.

The developer is learning game development with this project and plans to release it on the store.

## Read first

1. `Docs/GameDesign.md`: the game design (source of truth for features)
2. `Docs/DevRules.md`: coding, data, Git, and scope rules (must follow)
3. `Docs/Ideas.md`: idea backlog (not yet approved)

## Rules for agents

- **Language:** talk to the developer and write comments and docs in **Korean**. Identifiers, branch names, and file paths stay in English.
- **Learning first:** for core gameplay logic (e.g. `PickSkills()` weighted selection), build the surrounding structure and leave a clear `TODO` with hints for the developer to implement. Don't silently implement it.
- **Scope:** follow the roadmap order in `Docs/GameDesign.md`. New ideas go to `Docs/Ideas.md`, not into code.
- **Keep docs in sync:** if a change alters game design, update `Docs/GameDesign.md` in the same change.
- **Unity project lives in `Game/`.** Never edit or commit `Game/Library/`, `Game/Temp/`, `Game/Logs/`, `Game/UserSettings/`. Always commit `.meta` files with their assets.
- **Public repository:** never commit keystores, passwords, ad SDK secrets, or other credentials.
- **Commits:** `type: description` (types: feat, fix, balance, docs, refactor, chore). See `Docs/DevRules.md`.
- **Design reviews:** save review notes to `Docs/Reviews/YYYY-MM-DD-<topic>.md`.

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
- **Implement everything:** Claude writes all code, including core gameplay logic. Don't leave `TODO`s for the developer to fill in (developer's decision, 2026-10-26).
- **Scope:** follow the roadmap order in `Docs/GameDesign.md`. New ideas go to `Docs/Ideas.md`, not into code.
- **Keep docs in sync:** if a change alters game design, update `Docs/GameDesign.md` in the same change.
- **Unity project lives in `Game/`.** Never edit or commit `Game/Library/`, `Game/Temp/`, `Game/Logs/`, `Game/UserSettings/`. Always commit `.meta` files with their assets.
- **Public repository:** never commit keystores, passwords, ad SDK secrets, or other credentials.
- **Commits:** `type: description` (types: feat, fix, balance, docs, refactor, chore). See `Docs/DevRules.md`.
- **Design reviews:** save review notes to `Docs/Reviews/YYYY-MM-DD-<topic>.md`.

## Roles: Claude Code vs Codex

| | **Claude Code** (lead) | **Codex** (reviewer / QA) |
|---|---|---|
| Model | Claude Opus 5.5 | gpt-6.1-sol |
| Owns | Design doc, architecture, all implementation, Unity Editor control (Unity CLI + official plugin skills), commits, talking to the developer | Independent reviews of design and code, test ideas, visual QA through computer-use (watching the running game) |
| Writes files? | Yes | **No by default** — runs `--sandbox read-only`. Writes only when explicitly delegated, on its own branch |

### Effort by task

| Task | Who | Effort |
|---|---|---|
| Architecture, big design changes, tricky bugs | Claude | `xhigh` |
| Normal feature implementation | Claude | `high` (default) |
| Bulk / mechanical work (data assets, renames, doc tidy-up) | Claude subagent (Sonnet) | `medium` |
| File / docs lookup | Claude subagent (Haiku) | `low` |
| Design review | Codex | `xhigh` |
| Code review of a feature | Codex | `high` |
| Quick check / single-file sanity check | Codex | `medium` |
| Visual QA (computer-use: play the game, check UI and feel) | Codex | `medium` |

Codex effort per call: `codex exec -c model_reasoning_effort=<low|medium|high|xhigh> ...` (config default is `xhigh`).

### Workflow per feature
1. Claude implements on a feature branch and checks it compiles and runs in the Editor.
2. Codex reviews the diff read-only (`high`). Findings go to `Docs/Reviews/`.
3. Claude decides what to adopt (the developer's explicit decisions always win over reviewer suggestions).
4. Codex does visual QA when the feature is visible on screen.
5. Claude merges to `main`.

**One writer at a time:** Claude and Codex never edit the same files at the same time.

## Tools

- **Unity CLI** (`unity`, installed via winget) + **`com.unity.pipeline`** package in `Game/`: drive the live Editor instead of hand-editing `.unity` / `.prefab` / `.asset` YAML.
  - Check first: `unity status` (state `ready`), then `unity command eval --caller plugin --skill <skill> '<C#>'`.
  - On Windows, run `unity command eval` from **Git Bash**, not PowerShell 5.1 — PowerShell breaks embedded double quotes in native arguments.
- **Unity's official agent plugin** (Claude Code: `unity@claude-plugins-official`) provides Unity skills (UI, sprites, LevelPlay ads, IAP, localization, …). Prefer them over guessing Unity APIs.
- **Scene builder:** `HistoryHeroes.EditorTools.V01SceneBuilder.BuildAll()` (menu **History Heroes → v0.1 테스트 장면 만들기**) regenerates layers, placeholder sprites, the Korean TMP font, all data assets, prefabs, and `Assets/Scenes/Dungeon_v01.unity`. Change numbers in the builder (or the assets) and rerun it; don't hand-edit the scene.
- **QA in Play mode** (via `unity command eval`): `QaTools.Step(card, heal)` advances one action (pick card → kill → enter door); `QaTools.State()` prints run state; `QaTools.Scenario(preset, "boss"|"swarm"|"barrage", move)` + `QaTools.ScenarioStatus()` benchmark the three builds. `GameViewTools.SetPortrait()` sets the Game view to 1080×1920.
- **Play mode has domain reload disabled** (Enter Play Mode Options). Static fields survive between Play sessions, so every runtime static needs a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset. Never call gameplay code through `eval` unless `Application.isPlaying` is true — in Edit mode it changes the open scene (stray objects then carry into the next Play).
- **Captures:** `unity command capture game_view --width 540 --height 960 --save_path Temp/qa/x.png` saves under `Game/Assets/Temp/` (git-ignored). Each `eval` call takes ~3 s, so keep polling loops coarse.
- **Disk:** drive C is tight. If the Editor stops responding, check free space first (Editor.log "Win32 IO returned 112" = disk full).
- **Android testing:** `adb` ships with the Unity Android module at `C:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`.

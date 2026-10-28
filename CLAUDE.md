# CLAUDE.md

@AGENTS.md

## Claude Code notes

- Write all the code yourself, including core logic. Don't leave `TODO`s for the developer (developer's decision, 2026-10-26).
- Talk to the developer in **Korean**, in plain words.
- Before proposing a new feature, check whether it belongs in `Docs/Ideas.md` instead of the current roadmap version.
- Follow the roles and effort table in AGENTS.md. Ask Codex for reviews with `codex exec --sandbox read-only -c model_reasoning_effort=<level>`, piping the prompt via UTF-8 stdin from PowerShell (`$OutputEncoding = [System.Text.UTF8Encoding]::new($false)`; `Get-Content prompt.md -Raw -Encoding UTF8 | codex exec ... -`). Save its review under `Docs/Reviews/`.
- For mechanical subagent work use Sonnet (`medium`); for lookups use Haiku (`low`).
- Continuing on another computer: read `Docs/Dev/Handoff.md` first, then work from `Docs/TODO.md`. Graphics stay 3D (2D trial archived in `archive/art-2d`). Helper scripts are in `Tools/dev/` (Git Bash).

# CLAUDE.md

@AGENTS.md

## Claude Code notes

- The developer uses learning output style: explain choices briefly and leave key logic for them to write (5–10 lines, marked `TODO`).
- Before proposing a new feature, check whether it belongs in `Docs/Ideas.md` instead of the current roadmap version.
- Follow the roles and effort table in AGENTS.md. Ask Codex for reviews with `codex exec --sandbox read-only -c model_reasoning_effort=<level>`, piping the prompt via UTF-8 stdin from PowerShell (`$OutputEncoding = [System.Text.UTF8Encoding]::new($false)`; `Get-Content prompt.md -Raw -Encoding UTF8 | codex exec ... -`). Save its review under `Docs/Reviews/`.
- For mechanical subagent work use Sonnet (`medium`); for lookups use Haiku (`low`).

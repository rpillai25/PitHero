# CLAUDE.md

Claude Code-specific notes for PitHero. **All project development rules live in [AGENTS.md](AGENTS.md)** — read that first.

## Pointers

- **Project rules, architecture, AOT/Nez/UI/Balance constraints:** [AGENTS.md](AGENTS.md)
- **Domain expertise (auto-loaded by description match):** `.claude/skills/`
  - `nez-ai`, `nez-ui`, `monster-design`, `equipment-design`, `pit-balance-test`, `virtual-game-layer`, `make-skill-template`
- **The single agent in this repo:** `.claude/agents/principal-game-engineer.md` (implementer; skills auto-discover)
- **Reference docs (balance, libraries, subsystems):** `PitHero/docs/` — see [AGENTS.md](AGENTS.md) "Reference Documents" for the full list
- **Feature design docs:** `features/`

## Claude Code Conventions

- Skills follow progressive disclosure — thin `SKILL.md` + `references/*.md` loaded on demand. Don't list skills in agent files; let description-matching discover them.
- `.github/agents/` and `.github/skills/` are symlinks to `.claude/`. `.claude/` is the source of truth.
- Plan mode replaces the previous `planner` and `feature-builder` agents — use it for multi-step work.

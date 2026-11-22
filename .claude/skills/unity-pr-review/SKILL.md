---
name: unity-pr-review
description: Open a Project Nomad GitHub PR in Unity for manual review on Windows, reusing a shared local checkout and Library cache. Use when asked to prepare or open a PR for play review; does not approve or merge the PR.
---

# Unity PR Review

The procedure lives in the team's shared skill at `.agents/skills/unity-pr-review/SKILL.md` (relative to the repository root; from this skill directory it is `../../../.agents/skills/unity-pr-review/SKILL.md`). Read that file and follow it exactly; this file only exposes it to Claude Code, so do not duplicate or override its rules here.

The runner is this repository's `scripts/Open-PrInUnity.ps1`. Run it with the PowerShell tool by its verified absolute path, not a same-named script from the downloaded PR.

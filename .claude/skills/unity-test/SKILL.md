---
name: unity-test
description: Run Project Nomad's Unity EditMode/PlayMode tests in batch mode on Windows and report the results, for the current checkout or a Project-Nomad PR through the shared review checkout. Use when asked to run, check, or verify Unity tests; does not change code, commit, or comment unless separately asked.
---

# Unity Test

The procedure lives in the team's shared skill at `.agents/skills/unity-test/SKILL.md` (relative to the repository root; from this skill directory it is `../../../.agents/skills/unity-test/SKILL.md`). Read that file and follow it exactly; this file only exposes it to Claude Code, so do not duplicate or override its rules here.

The runner is this repository's `scripts/Test-Unity.ps1`. Run it with the PowerShell tool by its verified absolute path. A full run can exceed the default tool timeout, so set a longer timeout or run it in the background and wait for the completion notice instead of polling.

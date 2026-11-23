---
name: unity-pr-review
description: Open a Project Nomad GitHub PR in Unity for manual review on Windows, reusing a shared local checkout and Library cache. Use when asked to prepare or open a PR for play review; does not approve or merge the PR.
---

# Unity PR Review

Confirm the PR number or Project-Nomad PR URL, then run this repository's `scripts/Open-PrInUnity.ps1 -PrNumber <number>`. If no number is given, ask for it or look up the PR from the related Issue. Never open another repository's PR with this runner.

This skill ships with the repository; from the skill directory the runner is at `../../../scripts/Open-PrInUnity.ps1`. The working directory may not be the repository root, so call it by its verified absolute path. Never substitute a same-named script from inside the downloaded PR.

The runner uses a single disposable checkout at `%LOCALAPPDATA%\ProjectNomad\PRReview`. Preparing or opening a PR discards staged, unstaged, and ordinary untracked changes in that checkout and its submodules, while reusing `Library`. Keep work you want to save in a separate worktree. Use `-CheckOnly` for read-only inspection and `-PrepareOnly` to prepare without opening Unity.

Let the runner handle cleanup within the shared checkout. On a lock, working branch, unsafe path, or authentication failure, relay the message and path. Read `docs/unity-pr-review.md` for prerequisites and recovery only when needed.

When the command finishes, report the PR number, reviewed HEAD, project path, and result. Distinguish "Editor launch requested" from "import/compile finished". Wait for the person to pick a scene and review in Play mode; invoking this skill does not authorize running tests, approving, or merging.

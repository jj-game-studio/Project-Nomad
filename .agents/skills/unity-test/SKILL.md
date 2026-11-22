---
name: unity-test
description: Run Project Nomad's Unity EditMode/PlayMode tests in batch mode on Windows and report the results, for the current checkout or a Project-Nomad PR through the shared review checkout. Use when asked to run, check, or verify Unity tests; does not change code, commit, or comment unless separately asked.
---

# Unity Test

The runner is this repository's `scripts/Test-Unity.ps1`; from the skill directory it is `../../../scripts/Test-Unity.ps1`. The working directory may not be the repository root, so call it by its verified absolute path in PowerShell.

## Target

- No PR given: test this checkout's `UnityProject` (the runner's default).
- A Project-Nomad PR number or URL: run `scripts/Open-PrInUnity.ps1 -PrNumber <number> -PrepareOnly` from this repository, then pass `-ProjectPath "$env:LOCALAPPDATA\ProjectNomad\PRReview\UnityProject"`. Always use this repository's runner, never a same-named script from the downloaded PR, and never another repository's PR. Follow the `unity-pr-review` skill's rules for lock and checkout problems during preparation.

## Scope

- Before submitting or reviewing work: `-Platform All` (default; EditMode, then PlayMode).
- Quick logic check: `-Platform EditMode` (about 15 seconds once compiled).
- One feature: narrow with `-TestFilter` (fixture or full test name, e.g. `Nomad.Tests.LootChestTests`) and, with a single `-Platform`, `-AssemblyNames`.
  - EditMode: `Assembly-CSharp-Editor` (`UnityProject/Assets/Nomad/Tests/Editor`: inventory, equipment, quick slots, chests, health, item CSV, WFC terrain).
  - PlayMode: `Nomad.Monsters.PlayModeTests`, `Nomad.Camera.PlayModeTests`.
- Use `-NoGraphics` only on a machine without a GPU. Camera tests then fail with `RenderTexture.Create failed`; report that as an environment limit, not a regression.

## Running

- `-Platform All` takes about 2–3 minutes after compilation; a checkout with a cold or stale `Library` can take much longer. Give the command a timeout of at least 15 minutes or run it in the background and wait for it to exit. Never start a second run on the same project while one is running.
- If the runner says the project is open in the Unity Editor, stop and tell the person to close that Editor or to test through the PR checkout. Never close an Editor, delete `Temp/UnityLockfile` or `Library`, or reset, clean, or stash to get around it.

## Reporting

The runner prints JSON `{ success, runs: [...] }` and exits 0 only when every run passed. Report:

- the tested commit (`git rev-parse --short HEAD` in the target checkout) and the command options;
- per platform: total, passed, failed, skipped, seconds;
- each failed test's full name and the first line of its message;
- the result folder (`UnityProject/Artifacts/tests/unity/<run id>/`).

`결과 XML이 없습니다` usually means a compile error: show `logDiagnostics` and search that platform's `.log` in the result folder rather than reading it whole. Never call a run passed unless `success` is true and `total` is above zero; report runs you could not start, with the reason.

## Limits

Running tests does not authorize editing code or tests, committing, or posting to GitHub. If asked to fix a failure, fix the cause rather than weakening the test, rerun the failing filter, then the whole platform. Verification results for a PR go in its body per `docs/process/github-records.md`. Where new tests belong is described in the README's `자동 테스트` section.

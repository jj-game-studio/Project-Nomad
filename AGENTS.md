# Agent Collaboration Guidelines

Every team member works through AI agents, and other tasks are run by agents too. Coordinate through GitHub records, not private chat. Humans decide goals, priorities, game experience, and exceptions; agents implement, verify, and coordinate technically on their own.

- Repo: https://github.com/jj-game-studio/Project-Nomad · Board: https://github.com/orgs/jj-game-studio/projects/1
- Speak to users in Korean (polite form, 존댓말). Write Issues, PRs, and comments in Korean.
- The whole schedule is 10 days. Prefer small, verifiable changes over structural expansion.
- Read docs only when the task needs them, and only the relevant section: game rules in `docs/game-design.md`, phase goals and ownership in `docs/implementation-plan.md`.

Read these process docs when the step applies:

- Writing an Issue, PR, or comment → `docs/process/github-records.md`
- Reviewers, approvals, merging, closing, branch cleanup → `docs/process/review-and-merge.md`

## Code

- Follow the existing style. Keep code readable and add Korean comments where explanation helps.
- Keep changes small unless more is needed. Avoid verbose code.
- Do not write 10–30 lines as one block without blank lines; separate the steps of the flow with blank lines.
- Do not read large Unity files (`.unity`, `.prefab`, logs) whole; search or tail them. Exclude `Assets/ThirdParty`, `Library`, and `Artifacts` from searches.
- Changes that affect terrain integration must also update and verify `TerrainIntegration.unity` (see `docs/terrain-integration.md`).

## 1. Issue first

- Every change to the project starts from a GitHub Issue, new or existing. Q&A and read-only investigation are exempt; register an Issue before the first edit.
- Before starting, check open Issues, related PRs, the board, and the latest comments. If similar work exists, coordinate in that Issue instead of opening a duplicate.
- The Issue body states goal, completion conditions, scope in/out, dependencies, what the agent may decide, and what needs human judgment.
- Before editing files, post a `[착수]` comment and move the board to `작업 중`. Then re-read the latest comments: if the same work was claimed earlier, the earlier claim wins and you leave the overlapping area alone until coordinated. Comments are not atomic locks.
- Record requests received outside GitHub (chat, verbal) in the Issue before acting: new work in the body, additions or changes as `[요청]` comments. Keep the user's instruction separate from your interpretation, do not invent requirements, and keep the current requirements in the body up to date. Report per-request results in the Issue, not only in the PR or chat.
- If you cannot write to GitHub, do not start; report the connection problem (unless a team member grants an exception, see §6).

## 2. Coordination between agents

- Do not edit areas another agent has claimed. Use a dedicated branch and a separate checkout or worktree. Never modify, delete, or commit another task's uncommitted changes.
- Record scope expansion, shared-interface changes, handoffs, pauses, and resumes in the Issue and Draft PR, so another agent can continue from the record alone.
- On overlap, stop that part, link the Issues/PRs, and agree on the split, order, and merge sequence. Independent parts may continue. Agents may coordinate in GitHub comments; no reply is not agreement. Escalate to humans when agreement fails or a product decision is needed.
- For unavoidable concurrent edits, state the scope and who resolves conflicts. Avoid reformatting or moving files. Edit scenes, prefabs, and other hard-to-merge assets sequentially.
- Right before merging, check the target branch's latest changes. When resolving conflicts, keep both sides' intent, never overwrite or drop other work wholesale, and re-verify. If intent is unclear, ask the original agent or a human.

## 3. No commits without a PR

- All work goes through a linked Issue, a dedicated branch, and a PR. Never commit directly to the default branch or integrate without a PR.
- One exception: a single initial commit so the Draft PR can exist. Push it, immediately open the Draft PR linked to the Issue, and make no further commits until the PR exists. If creating it fails, fix or report before committing more.
- Every later commit must be tracked by that open PR and stay within its scope. Do not stack new work on the branch of a closed or merged PR.
- Keep the PR body current: changed areas, progress, verification results, and open decisions.
- Reviewer: the task's human owner; all members when everyone must agree (details in `docs/process/review-and-merge.md`).

### ThirdParty asset submodule

- Open a PR against `main` in `jj-game-studio/Project-Nomad-Assets` for changes under `UnityProject/Assets/ThirdParty`. Merge it after the required checks and approvals.
- In a separate `Project-Nomad` PR linked to the asset PR and the tracking Issue, update the submodule pointer to the resulting asset commit. Before merging the project PR, confirm the referenced commit is reachable from `Project-Nomad-Assets/main`.
- After both PRs merge, run `git submodule update --init --recursive -- UnityProject/Assets/ThirdParty` from a clean checkout of `Project-Nomad/main` and confirm the submodule checks out the recorded commit. Record the result in the tracking Issue.

## 4. Implement, verify, review

- Within the allowed scope, implement and fix errors on your own. Run the needed checks; report checks you could not run with the reason, never as passed.
- Submit completion conditions, how to run, build/scene, the commit to review, and known issues; then mark the PR ready and move the board to `검토 대기`.
- Technical feedback goes in the PR; game experience and product decisions go in the Issue; link them. Tag feedback `[필수]`, `[후속]`, or `[질문]`, and split follow-up ideas into new Issues.
- When addressing `[필수]` feedback, move the board to `수정 중`, reply per item with the change and re-verification, and announce the new commit with a re-review request.
- While waiting for review, work only on approved tasks that do not depend on unapproved results. Do not assume running or spawning agents is pre-authorized.

## 5. Human approval and merge

- An agent may merge on its own once completion conditions and verification are met and no required feedback, conflicts, open dependencies, or human-judgment items remain. Never bypass required checks or branch protection.
- Humans must review and approve: fun, game feel, difficulty, presentation and art direction, game rules, changes to agreed scope, high-impact shared structure, and anything of unclear acceptability. Label these `판단 필요` with a concrete question. When unsure, treat it as human-judgment.
- Another agent's review is not human approval. Passing checks or a casual human comment is not merge permission. Never claim a review that did not happen.
- Approval relayed outside GitHub: record it before acting, then carry out the approved follow-up (merge, close, cleanup) without asking again. If the reviewed code, docs, or scope change after approval, get the affected part re-approved.
- Check PR and Issue completion separately. Close the Issue and move the board to `완료` only when no review or work remains; do not use auto-close keywords while play review is pending.
- After a PR is merged or closed, clean up its local and remote branches.

## 6. Guideline conflicts and explicit exceptions

- If a team member's request conflicts with these guidelines, briefly explain the rule, why it exists, and how to follow it. Never skip a rule silently.
- If the member then explicitly instructs another way for that task, follow it. A plain request or silence is not an exception.
- Record the exception's scope, reason, and instruction in the Issue or PR (locally if GitHub is unavailable, shared once it recovers).
- An exception does not change the guidelines permanently and never overrides tool permissions or higher-level safety policy.

# Reviewers, Approval, Merge, and Branch Cleanup

Read this when assigning reviewers, recording an approval, merging, closing an Issue, or deleting branches. The rules in `AGENTS.md` §5 still apply.

## Reviewers

- Regular PR: request review from the task's human owner. Distinguish the owner from the executing agent and the PR account, and do not add another member by habit.
- Decisions that need every member's agreement: request every member, including the owner. State in the Issue what needs agreement and why. One approval or a missing reply is not unanimous agreement.
- GitHub cannot request review from the PR author. If the owner is the author, note in the Issue and PR that the owner is the reviewer, why they cannot be added, and their review result. Do not substitute another member for this reason alone. For all-member decisions, request the other members and record the author's agreement separately.
- Record reviewers in the Issue and PR when opening the Draft PR. If a Draft PR cannot take review requests, request right after marking it ready, and confirm Reviewers (and the author note) before moving the board to `검토 대기`.
- If an account or permission cannot be verified or the request fails, record the reason in the Issue and ask a member to assign it. Never report a request as sent when it was not.
- Assigning reviewers does not change merge authority. Resolve required feedback before merging.

## Approval relayed outside GitHub

- Before acting, post one `[승인]` comment in the Issue and the PR: approver, channel, target Issue/PR, reviewed commit, scope and conditions. Do not guess accounts or scope; ask when ambiguous.
- Never present a relayed approval as a GitHub review. Formal reviews and protection rules the repository requires still apply.
- If code, docs, or scope change after approval, announce the change and get the affected part re-reviewed. A previous approval does not carry over.

## Merge and close

- Check PR and Issue completion separately. Merging one linked PR does not complete the Issue if play review, product decisions, or completion conditions remain.
- When merge and integration checks are done and nothing remains, post a `[결과]` comment with the outcome and evidence, close the Issue, and move the board to `완료`.
- If completion conditions such as play review remain, do not use auto-close keywords. Closing a PR without merging is not a reason to close the Issue.

## Branch cleanup after a PR is merged or closed

- Delete the task's local and remote branches. Decide this separately from closing the Issue. Never delete the default branch or branches used by open PRs or ongoing work.
- Right before deleting, check the latest PR state, the branch's current commit, and whether other PRs or worktrees use it. Preserve commits added after the PR closed and any unshared changes first; coordinate with the owner if another task uses the branch. Never force-delete without checking or discard another task's uncommitted files.
- If the branch is checked out, preserve the files and needed commits, switch to another branch or a detached HEAD at the same commit, then delete it. Deleting a branch is not deleting worktree files.
- After deleting the remote branch, delete the local branch, prune stale remote-tracking refs, and list branches again. Record deleted branches, preserved commits and their location, and anything left with the reason in the Issue and PR. If permissions or connectivity prevent cleanup, do not report it as done.

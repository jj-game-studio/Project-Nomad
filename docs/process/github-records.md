# GitHub Records: Issues, PRs, and Comments

Issues and PRs are records that people read: write them **in Korean**, briefly, in the fixed structure below. Every agent reads these records before starting work, so each extra line costs every later task.

## Rules

- One place per fact. Requirements and current state live in the Issue body; changes and verification details live in the PR body. Comments link there instead of repeating them.
- Edit instead of appending. Update the Issue body, the PR body, or your latest comment (`gh issue comment --edit-last`) rather than posting progress comments.
- Do not post interim reports such as "verifying" or "not passed yet". Comment when something starts, a request arrives, a decision is made, or a result is ready.
- Leave out session IDs, local paths, full 40-character SHAs (use 7 characters), long chat quotes (one key line at most), tool logs, and verification details already in the PR.
- Keep a comment to about five bullets. If it needs more, the content belongs in the body or a doc.
- `gh issue create --body` and `gh pr create --body` do not apply the `.github` templates; copy the headings below yourself.
- Requests from outside GitHub (`요청` section, `[요청]` comments): include requester and channel, the request, and any stated background, constraints, and completion conditions. Name only verifiable accounts, never copy secrets or unnecessary personal data, and leave ambiguity as a question. Do not delete earlier records; link a request already on GitHub instead of restating it.
- Before starting work, read the Issue body and the last few comments. Read the full history only when resolving a conflict or a decision.

## Issue body

Use these headings in this order. Omit `요청` when the work did not come from outside GitHub. Keep the body current: when requirements change, edit it and leave a `[요청]` comment with before → after.

```markdown
## 요청
YYYY-MM-DD 경로, 요청자(확인된 계정만): 요청 요약

## 목표

## 완료 조건
- [ ] 확인 가능한 동작

## 범위
- 수정:
- 제외:

## 담당·판단
- 담당: @계정 / 실행: 에이전트
- 검토:
- 자율 결정:
- 사람 판단:

## 의존성

## 현재 상태
진행 단계, 연결 PR, 남은 일 (바뀔 때마다 수정)
```

## Comments

Start every Issue or PR comment with one tag, and keep it to a line or a few bullets.

| Tag | When | Content |
| --- | --- | --- |
| `[착수]` | Before editing files | owner, executor, branch (base commit), planned files and shared interfaces, overlap check |
| `[요청]` | A new or changed request from outside GitHub | requester and channel, the request, before → after; mark your interpretation as such |
| `[조율]` | Coordinating with other work | the overlap, agreed split and order, who resolves conflicts |
| `[승인]` | Approval relayed outside GitHub | `@승인자 / 경로 / PR #N @abc1234 / 범위·조건` |
| `[결과]` | Work submitted or finished | outcome per request, PR link, checks not run, remaining human judgment |

Review feedback uses `[필수]`, `[후속]`, and `[질문]`: what was observed, what to change, how to confirm it.

## PR body

```markdown
## 변경 요약

## 관련 Issue
Refs #N

## 검증
- 실행한 검사:
- 실행하지 못한 검사와 이유:

## 사람 검토
- 검토 커밋:
- 실행 방법 (씬 / 빌드):
- 판단 필요:

## 알려진 문제
```

Use `Refs #N`, not `Closes #N`, while play review or other Issue conditions remain.

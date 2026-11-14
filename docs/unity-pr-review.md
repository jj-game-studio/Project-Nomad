# Unity PR 검토 프로젝트 열기

Windows에서 Git, 인증된 GitHub CLI(`gh auth status`), Unity CLI와 해당 PR이 지정한 Unity Editor를 준비합니다. 비공개 애셋 서브모듈 접근 권한도 필요합니다. 설치된 Unity CLI의 실행 옵션은 `unity open --help`로 확인할 수 있습니다.

저장소 루트의 PowerShell에서 실행합니다.

```powershell
.\scripts\Open-PrInUnity.ps1 -PrNumber 41
```

PR의 최신 HEAD와 서브모듈을 준비하고 프로젝트 버전에 맞는 Editor를 엽니다. 기본 Unity Hub 설치 경로인 `%ProgramFiles%\Unity\Hub\Editor\버전\Editor\Unity.exe`를 사용합니다. 씬 선택과 Play는 직접 하며, 이 명령은 검토 승인이나 병합을 수행하지 않습니다.

## 공용 폴더와 캐시

모든 PR은 `%LOCALAPPDATA%\ProjectNomad\PRReview`라는 **단일 독립 checkout**에서 전환합니다. Unity 프로젝트는 그 아래 `UnityProject`이며 원래 작업 저장소에는 영향을 주지 않습니다. `Library`를 삭제하거나 PR마다 복사하지 않습니다. 최초 실행은 전체 임포트가 필요하고 이후에는 기존 캐시를 활용합니다. 에셋·패키지·Editor 버전이 달라지면 재임포트가 발생할 수 있습니다.

기존 로컬 시험 폴더 `.local-pr-review/checkouts/PR-41`는 자동 이동·삭제하지 않습니다. 실행 중인 Unity 및 미커밋 파일을 보존하기 위한 선택이며, 기존 캐시는 기존 위치에 남습니다. 새 공용 폴더의 첫 임포트 비용은 발생합니다.

## 조회와 준비만 실행

```powershell
# PR 정보만 조회하며 검토 폴더를 생성하거나 Unity를 실행하지 않습니다.
.\scripts\Open-PrInUnity.ps1 -PrNumber 41 -CheckOnly

# 다운로드와 PR 전환만 수행합니다. Unity CLI나 Editor 설치가 없어도 가능합니다.
.\scripts\Open-PrInUnity.ps1 -PrNumber 41 -PrepareOnly
```

동일 PR 재실행과 다른 PR 전환 모두 같은 경로를 사용합니다. `refs/review`에 기존 HEAD와 검토한 커밋을 로컬로 보존하며 원격에 푸시하지 않습니다.

## 갱신이 중단된 경우

- 검토 프로젝트의 Unity를 먼저 종료합니다. `Temp/UnityLockfile`이 남았다면 실제 Editor 종료를 확인한 뒤 직접 처리합니다. 스크립트는 잠금 파일을 자동 삭제하지 않습니다.
- 수정·미추적 파일 및 서브모듈 변경이 있으면 경로와 `git status`로 확인하고 필요한 내용을 보존합니다. 스크립트는 reset, clean, stash 또는 강제 checkout을 하지 않습니다.
- 별도 작업 브랜치를 만들었다면 자동 전환하지 않습니다. 필요한 작업을 보존하고 detached 상태로 직접 돌아온 뒤 실행합니다.
- clone이나 submodule 준비 실패 시 폴더를 보존합니다. GitHub 인증·애셋 권한을 확인합니다. 불완전한 checkout은 수동 확인이 필요하며 자동 재생성하지 않습니다.
- 다른 실행이 준비 중이면 완료 후 다시 시도합니다. 대상 폴더가 다른 저장소이면 사용하지 않습니다.

## Codex 스킬

이 저장소의 `.agents/skills/unity-pr-review/SKILL.md`가 팀 공유 스킬입니다. 해당 checkout을 작업 공간으로 연 Codex에서 스킬 목록을 갱신한 뒤 `$unity-pr-review`와 PR 번호를 전달합니다. 스킬은 이 저장소의 `scripts/Open-PrInUnity.ps1`을 호출하므로 스킬 폴더만 개인 스킬 경로에 복사하지 않습니다.

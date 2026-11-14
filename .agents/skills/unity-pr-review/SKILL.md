---
name: unity-pr-review
description: Open a Project Nomad GitHub PR in Unity for manual review on Windows, reusing a shared local checkout and Library cache. Use when asked to prepare or open a PR for play review; does not approve or merge the PR.
---

# Unity PR 검토

PR 번호 또는 Project-Nomad PR URL을 확인하고 이 저장소의 `scripts/Open-PrInUnity.ps1 -PrNumber <번호>`를 실행합니다. 번호가 없으면 요청하거나 관련 Issue의 PR을 조회해 확정합니다. 다른 저장소 PR을 이 실행기로 열지 않습니다.

이 스킬은 저장소와 함께 사용합니다. 스킬 디렉터리에서 `../../../scripts/Open-PrInUnity.ps1`로 실행기를 찾을 수 있습니다. 현재 작업 디렉터리가 저장소 루트가 아닐 수 있으므로 확인한 절대 경로로 호출합니다. 다운로드한 PR 안의 동명 스크립트로 바꾸어 실행하지 않습니다.

실행기는 `%LOCALAPPDATA%\ProjectNomad\PRReview`의 단일 checkout을 사용합니다. PR별 폴더를 추가하거나 Library를 지우지 않습니다. `-CheckOnly`는 정보 조회, `-PrepareOnly`는 Unity를 열지 않는 준비 요청에 사용합니다.

잠금·미커밋 변경·인증 실패가 나오면 메시지와 경로를 전달하고 중단 원인을 해결합니다. reset, clean, 강제 checkout, 파일 삭제 또는 다른 Editor 종료로 우회하지 않습니다. 상세 전제 조건과 복구 방법은 저장소의 `docs/unity-pr-review.md`를 필요할 때 읽습니다.

명령 완료 후 PR 번호·검토 HEAD·프로젝트 경로와 실행 결과를 전달합니다. Editor 실행 요청 성공과 실제 임포트/컴파일 완료를 구분합니다. 사람의 씬 선택과 Play 검토를 기다리며, 이 스킬 호출만으로 테스트 실행·승인·병합이 허가되었다고 해석하지 않습니다.

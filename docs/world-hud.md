# 캐릭터 스태미나·상호작용 표시 (#92)

Unity 6000.6.9f1에서 `Nomad > HUD > Open World HUD Preview` → Play.
WASD 이동, E는 강조된 물체 사용, Shift 구르기. T는 미리보기 스태미나 20 감소,
R은 최대치, F는 부족 알림입니다. 실제 구르기 소모·회복 규칙은 없습니다.

## 장면 연결

플레이어 루트에 `CharacterStaminaHud`와 `InteractionHints`를 추가합니다.
후자는 기존 `PlayerInteraction`, `PlayerMovement`, `PlayerInventory`,
`CharacterController`를 사용합니다. 기존 머리 위 HP바와 별개이며 HP 코드를 수정하지 않습니다.
메인 카메라를 자동 사용하거나 Inspector에서 카메라를 지정합니다.
스태미나의 기본 Screen Offset은 `(60, -40)`입니다. HP바와 간격을 벌리도록 아래로
40 UI 단위 내리고, 참고 이미지처럼 몸과 곡선 사이 여백을 위해 기존 X20에서
오른쪽으로 40 UI 단위 더 띄웠습니다. 기존 수동 배치 컴포넌트는 Inspector의
Screen Offset을 X=60, Y=-40으로 맞추면 됩니다.

게임의 자원 시스템에서 `CharacterStaminaHud.SetStamina(current, maximum)`을 호출하고,
스태미나 부족으로 행동을 거절했을 때 `NotifyInsufficient()`를 호출합니다.
최대치가 0 이하이거나 값이 NaN/무한대면 숨깁니다. 부분 잔량은 주황 원호로 표시하고
완전 회복 후 0.7초가 지나면 숨깁니다. 처음에는 데이터가 없어 숨겨집니다.
하단 HUD의 `PlayerHud.SetResources`는 기존대로 별도 호출합니다.

## 상호작용 판정과 제한

발 기준 2m 이내·장애물에 가리지 않은 `IInteractable`에 흰 이중 원을 표시합니다.
가장 가까운 대상(동일 거리면 InteractionId 사전순)을 금색으로 강조하고 E를 붙입니다.
화면 밖·카메라 뒤·구르기·사망·상자 창 열림·상호작용 컴포넌트 비활성에서는 숨깁니다.
표시는 입력을 받지 않으며 EventSystem이나 GraphicRaycaster를 추가하지 않습니다.

다른 작업이 수정 중인 공용 PlayerInteraction 파일을 피하기 위해 현재 main의 판정을
독립 조회합니다. **Target Layers / Obstacle Layers를 PlayerInteraction과 같게 설정해야 합니다.**
앞으로 상호작용 거리·선택·입력 차단 규칙이 바뀌면 이 조회와 회귀 검증도 갱신해야 합니다.
미병합 상점 PR #84의 추가 입력 차단과 통합은 이번 검증 범위에 포함하지 않습니다.
정식 지역/플레이어 프리팹(#89) 연결은 담당자 조율 후 적용하며 원본 씬은 변경하지 않습니다.

## 검증

배치 진입점은 `Nomad.WorldHud.Editor.WorldHudAuthoring.Verify`입니다.
`-batchmode -executeMethod ...`로 실행하고 `-quit`은 붙이지 않습니다.
미리보기 씬/전용 머티리얼을 생성하고 Play Mode에서 실제 가상 E 입력과 대상 일치,
거리·동률·사용 완료 숨김·벽·카메라 뒤·활성화 복원, 자원 유효성·회복 숨김·부족 표시를 확인합니다.
성공 표식은 `WORLD_HUD_PLAYMODE_PASS`, 렌더 캡처는 `UnityProject/Artifacts/world-hud/`입니다.
UI 크기·위치·외형과 물체 표식 가독성은 사람 검토 대상입니다.

2026-11-19 Unity6000.6.9f1 검증: `Logs/world-hud-verified3.log`의
`WORLD_HUD_PLAYMODE_PASS` 확인. 1920×1080/1280×720/2560×1080 렌더 생성,
1920·1280 렌더에서 원호·이중 원·E·안내 글자 확인. 구르기는 배치 렌더의 긴 프레임 영향을
피하려고 검증에서만 60Hz 시간을 사용하고, 가상 Shift 입력 직후 이동 Update를 호출하여 검사합니다.
앞선 배치의 구르기 시점 검사는 실패했으며 최종 결과는 verified3 로그를 기준으로 합니다.
최종 HUD 로그에 컴파일 오류·런타임 예외·TMP 리소스 경고는 없고,
Unity 라이선스 클라이언트의 시작 시 토큰 갱신 오류 후 정상 실행된 기록은 남아 있습니다.
Windows 플레이어 빌드·팀원 HP바와의 실제 통합 화면·사람 수동 조작 검토는 미실행입니다.

## 이동·상호작용 근처에서 게이지가 튀던 현상 수정

화면 공간 Canvas를 플레이어의 자식으로 생성하여, 플레이어 이동·회전과 Canvas
재빌드 사이에 부모 변환이 화면 좌표에 섞였습니다. 카메라와 HUD가 모두 LateUpdate를
사용하므로 카메라 갱신 전 투영되는 실행 순서 문제도 있었습니다.
렌더 직전 재투영만 적용하면 재현 오차가 그대로 남았고, Canvas를 플레이어와 같은 씬의
독립 루트로 분리한 뒤 해결됐습니다. 소유 컴포넌트 제거 시 Canvas도 정리합니다.
스태미나와 물체 표식은 렌더 직전 최종 카메라 위치로 재배치하며,
표식 대상 탐지는 LateUpdate에서 한 번 수행합니다.

열려 있는 사용자 Editor는 유지하고 `%TEMP%/NomadHudCheck-92` 독립 검증 사본에서
이동·회전·거리 경계 왕복 및 두 카메라 실행 순서를 24회 비교했습니다.
기존 최대 오차 545.574px → 수정 후 0.000px (`before.log`, `after-root.log`).
정지 렌더·E키 대상·거리·차폐·구르기·자원 표시 검증도 함께 통과했습니다.
최종 `final.log`에서 오차 0.000px, 전체 `WORLD_HUD_PLAYMODE_PASS` 및 소유자 제거 후
독립 Canvas 정리를 확인했습니다. 이 실행에는 UnityEditor.Search.SearchDatabase의 시작
인덱싱 ArgumentOutOfRangeException 1건이 있으며 HUD 코드 스택과 구분합니다.

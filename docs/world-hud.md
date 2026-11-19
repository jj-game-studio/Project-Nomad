# 캐릭터 스태미나·상호작용 표시 (#92)

Unity 6000.6.9f1에서 `Nomad > HUD > Open World HUD Preview` → Play.
WASD 이동, E는 강조된 물체 사용, Shift 구르기. T는 미리보기 스태미나 20 감소,
R은 최대치, F는 부족 알림입니다. 실제 구르기 소모·회복 규칙은 없습니다.

## 장면 연결

플레이어 루트에 `CharacterStaminaHud`와 `InteractionHints`를 추가합니다.
후자는 기존 `PlayerInteraction`, `PlayerMovement`, `PlayerInventory`,
`CharacterController`를 사용합니다. 기존 머리 위 HP바와 별개이며 HP 코드를 수정하지 않습니다.
메인 카메라를 자동 사용하거나 Inspector에서 카메라를 지정합니다.

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

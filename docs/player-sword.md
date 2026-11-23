# 장검 손 부착·공격 모션 검토본

Issue #168 / PR #169. 기존 Kingdom 외형과 ARPG Warrior `Attack_Combo1`을 사용합니다.

## 실행

Unity 6000.6.9f1에서 `Nomad > Player Visual > Open Sword Preview` → Play.
장검을 든 플레이어로 WASD 이동, 좌클릭 공격, Tab 장비 변경, Space 점프, Shift 구르기를 확인합니다.
표적은 체력 100이며 장검 한 번에 25 피해를 받습니다. 표적이 죽으면 Play를 다시 시작합니다.
`Build Sword Preview`는 독립 SwordPlayerRig/PlayerSword Controller/검토 씬을 다시 만듭니다.
정식 GameConfig·마을·필드에 이 프리셋을 등록하지 않습니다.

## 구현

- 오른손 뼈 아래 WeaponSocket을 만들고 장검(`demo-sword`)만 부착합니다. 다른 무기와 기존 캡슐 플레이어는 기존 배치를 유지합니다. 장착 모델의 Collider는 비활성입니다.
- 별도 상체 레이어/AvatarMask로 ARPG 공격을 재생합니다. 하체는 기존 이동 모션을 따릅니다. 루트 모션은 사용하지 않습니다.
- 새 Controller의 Run 속도는 LocomotionSpeed 파라미터로 조정하여 공격 속도와 분리합니다. 기존 Controller의 동작은 유지합니다.
- 무기 교체·공중/구르기·입력 차단·사망·비활성화 시 공격 자세를 해제합니다. 모듈 해제 시 손 부착도 기존 방식으로 돌아갑니다.

## 남은 연결

현재 전투의 즉시 피해/VFX 규칙은 그대로입니다. 모션은 접촉 구간(정규화 시간 0.32)부터 재생하는 임시 검토본이며 준비 동작과 지연 피해가 연결된 완성본이 아닙니다.
PlayerEquipmentCombat.cs를 선점한 #160 담당자에게 파일 분할·병합 순서를 조율 요청했습니다.
사용자는 2026-11-23 채팅에서 선점 예외를 적용하지 않고 답변을 기다리도록 지시했습니다.
합의 후 준비 동작 → 한 번의 타격/VFX → 마무리를 연결하고 취소/교체 검사를 다시 해야 합니다.
손 자세와 모션 느낌의 사람 검토, 정식 게임 연결, 활/지팡이 모션은 별도입니다.

## 검증

- PlayerSwordVerification.Run: 실제 Humanoid 손 움직임·모델 손 부착·Collider 비활성, 즉시 피해 1회·중복/쿨다운 차단, 입력 차단, 공격 중 이동/하체 Run·일정 공격 재생 속도, 무기 교체, 비활성/재활성, 사망 취소.
- PlayerVisualVerification.Run: 기존 271프레임 이동·점프/낙하/착지·구르기·벽 충돌 회귀.
- 배치 실행: `unity open <UnityProject 절대 경로> --batchmode --args "-executeMethod Nomad.PlayerVisual.Editor.PlayerSwordVerification.Run -logFile <로그 절대 경로>"`. 검사 후 Editor가 직접 종료합니다.
- 로컬 렌더 근거: Artifacts/sword/sword-idle.png, sword-attack.png, sword-moving.png. 자동 검증과 사람 플레이 수용은 구분합니다.

전체 회귀: EditMode 105/105, PlayMode 89/89, 실패·건너뜀 0. Windows 빌드와 사람 직접 플레이 검토는 미실행입니다.

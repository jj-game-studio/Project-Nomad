# 장검 손 부착·공격 모션

Issue #168 / PR #169. Kingdom 외형과 ARPG Warrior `Attack_Combo1`을 사용합니다.

## 실행

Unity 6000.6.9f1에서 `Nomad > Player Visual > Open Sword Preview` → Play.
WASD 이동, 좌클릭 장검 공격, Tab 장비 변경, Space 점프, Shift 구르기입니다.
표적은 체력 100이며 장검 한 번에 25 피해를 받습니다. 표적이 죽으면 Play를 다시 시작합니다.
`Build Sword Preview`는 독립 SwordPlayerRig/PlayerSword Controller/검토 씬을 다시 만듭니다.
정식 GameConfig·마을·필드에 이 프리셋을 등록하지 않습니다.

## 모션과 무기

- 오른손 뼈 아래 WeaponSocket에 장검(`demo-sword`)만 부착합니다. 다른 무기와 기존 캡슐 플레이어는 기존 배치를 유지합니다. 장착 모델의 Collider는 비활성입니다.
- 상체 레이어/AvatarMask로 공격을 재생하고 하체는 기존 이동·점프를 따릅니다. 루트 모션은 사용하지 않습니다.
- LocomotionSpeed와 SwordSpeed 파라미터로 이동 속도·공격 속도를 분리합니다. 공격 전체 길이는 장검의 기존 공격 간격(0.6초)을 따릅니다.
- 모션이 처음부터 재생되고 정규화 시간 0.32에 맞춘 준비 시간(0.192초) 뒤 기존 근접 판정과 베기 VFX를 한 번 실행합니다. 60fps에서는 약 0.20초입니다.
- 조준 방향은 공격 시작 시 고정하고, 타격 순간의 플레이어 위치에서 거리·벽·대상을 다시 검사합니다.
- 무기/선택 슬롯 변경, 구르기, 입력 차단·장비 UI·거래, 사망, 전투/모션 컴포넌트 비활성화 시 준비 중인 타격을 취소합니다. 취소해도 기존 공격 간격은 유지합니다. 일시정지 중에는 준비 시간도 멈춥니다.
- 모션 컴포넌트가 없거나 지원하지 않는 무기는 기존 즉시 타격/발사를 유지합니다. 무기 피해·사거리·간격·재화 수치는 바꾸지 않습니다.

## 선택적 연결 API

`IMeleeAttackTiming.GetWindup`이 준비 시간을 제공합니다. PlayerSwordAnimation이 장검에만 양수를 반환합니다.
`TryAttack`은 공격 준비가 시작되면 true를 반환하고, `MeleeStarted`로 모션을 시작합니다.
기존 `Attacked`는 실제 타격 시점에 발생하여 베기 VFX가 피해와 함께 시작됩니다. 준비 취소는 `MeleeCancelled`, 현재 준비 여부는 `IsMeleePending`으로 확인합니다.

## 범위와 남은 검토

사용자는 2026-11-23 채팅에서 #160 선점 PlayerEquipmentCombat.cs를 장검 타격 시점 연결에 한해 예외로 수정하도록 허용했습니다. 스탯·아이템 수치·타 작업본은 수정하지 않습니다.
손 자세와 공격 느낌·타격 시점의 사람 검토, 정식 게임 연결, 활/지팡이 모션·콤보·점프 모션 교체는 별도입니다.

## 검증 실행

`unity open <UnityProject 절대 경로> --batchmode --args "-executeMethod Nomad.PlayerVisual.Editor.PlayerSwordVerification.Run -logFile <로그 절대 경로>"`

검사는 저장된 검토 씬에서 실제 플레이 프레임으로 지연 피해/VFX·모션 시점·취소·일시정지·이동·벽과 거리 재판정·기존 무기 동작을 확인하고 Editor를 종료합니다. 검증 루프는 UNITY_EDITOR에서만 컴파일하며 저장 프리팹/씬에 포함하지 않습니다.
기존 이동·점프·구르기는 PlayerVisualVerification.Run으로 확인합니다. 렌더는 로컬 Artifacts/sword에 보관합니다.
사람 직접 플레이와 Windows 빌드는 자동 Play 검증과 구분합니다.

# CSV 아이템·장비와 실제 공격

## 실행

Unity 6000.6.9f1에서 `Nomad > Equipment > Open Equipment Demo` → Play.
CSV를 수정해 다시 반영하려면 Play를 멈추고 `Nomad > Equipment > Create Equipment Demo`를 실행합니다.
전용 EquipmentDemo 씬과 Items를 갱신하며 원본 A_PlayerCombat/기존 인벤토리·마을·통합 씬은 저장하지 않습니다.
CSV는 `Assets/Nomad/Equipment/equipment.csv`입니다. 모든 샘플 수치는 검토용 임시 값입니다.

Tab을 누르면 기존 가방20칸 위에 별도 장비10칸(5×2)이 함께 표시됩니다.
가방 아이템을 장비칸에 드래그하거나 가방 아이템 선택 후 장비칸을 클릭하면 장착합니다.
장비를 가방의 빈칸으로 드래그하면 그 칸에 해제하고, 호환 장비가 있는 가방칸이면 교체합니다.
장비 우클릭은 첫 빈 가방칸으로 해제합니다. 무기칸만 클릭하면 사용할 무기를 선택합니다.
장비창이 열려 있는 동안 공격하지 않습니다. 가방20칸과 장비10칸은 별도 공간입니다.
가방에서 드래그하는 아이템은 장비 UI보다 앞에 표시됩니다. 도착 장비칸은 장착 가능하면 초록색과
반투명 아이템으로 미리 표시하고, 불가능하면 빨간색으로 강조합니다. 드롭 전에는 실제 장비를 바꾸지 않으며
포인터 이탈·Escape·창 닫기·포커스 이탈 때 미리보기를 원래 상태로 복원합니다.

LMB로 선택 무기를 사용합니다. F2 또는 왼쪽 위 화면 토글로 공격 범위를 켜고 끕니다.
근접은 실제120도·CSV사거리의 경계를 표시하며 벽에 가린 구간은 잘립니다.
원거리는 발사구에서 실제 첫 충돌까지의 경로를 표시합니다. 범위 표시를 꺼도 공격 이펙트는 유지합니다.
근접 궤적·원거리 발사 섬광·실제 근접 명중/발사체 충돌 섬광은 짧게 사라지며 동시효과24개로 제한합니다.

전용 씬에서 1=검, 2=활, F5=갑옷 장착/해제, F6=선택 무기 해제,
F7=검 재장착, F8=활 재장착입니다. 정식 게임의 무기 전환 키 배치는 확정하지 않았습니다.
선택 무기칸이 비면 CSV의 맨손 설정을 사용합니다. 맨손 정의는 가방에 지급하지 않습니다.
장비 상태는 장비10칸 UI와 Console/API로 확인합니다. 기존 저장된 EquipmentDemo 씬도 Play 시작 시 필요한 UI/표시 컴포넌트를 보완합니다.

## 장착과 소유

EquipmentSlot: Weapon1, Weapon2, Head, Body, Bag / Face, Accessory, Ring, Totem1, Totem2.
무기2칸은 근접/원거리 공용입니다. EquipmentType은 장착 가능한 분류이고 EquipmentSlot은 실제 위치입니다.
`TryEquip(가방칸, 장비칸)`, `TryUnequip(장비칸)`, `SelectWeapon(무기칸)`을 사용합니다.
아이템은 실제로 가방에서 장비칸으로 이동합니다. 교체하면 이전 장비가 원래 가방칸으로 돌아옵니다.
해제는 첫 빈 가방칸을 사용하고 빈칸이 없으면 아무것도 바꾸지 않습니다.
같은 정의의 사본을 여러 칸에 장착할 수 있지만 실제 보유 사본이 각각 필요합니다.
성공 상태를 모두 반영한 뒤 Inventory.Changed와 Equipment.Changed를 알리며 장비 API 재진입을 막습니다.
가방·토템·반지 등의 고유 효과, 가방 용량 확장, 내구도/옵션/저장은 포함하지 않습니다.

## 전투

PlayerEquipmentCombat이 활성화되면 같은 오브젝트의 기존 PlayerCombat을 잠시 비활성화해 이중 공격을 막습니다.
비활성화 시 이전 상태로 복원합니다. 기존 PlayerCombat/PlayerInventory 코드와 기존 씬은 수정하지 않습니다.
현재 선택 무기 또는 맨손 정의의 AttackPower, AttackMode, AttackRange, AttackInterval을 매 공격에 읽습니다.
전환·장착·해제로 공격 간격을 초기화하지 않습니다. 원거리는 기존 Projectile을 사용하며 발사체 속도45는 기존 기본값을 유지합니다.
이미 발사된 탄은 발사 당시 피해량을 유지합니다.

근접은 마우스 조준 방향의 전방120도와 CSV 사거리 안에 있는 각 Health에 한 번씩 즉시 피해를 줍니다.
여러 콜라이더를 가진 대상도 한 번만 맞으며 다른 적이 뒤의 적을 가리지 않습니다. 벽/지형은 차폐합니다.
자기 자신·사망한 대상·트리거는 제외합니다. 간단한 궤적/명중 이펙트는 제공하며 캐릭터 공격 애니메이션/무기 모델은 별도입니다.
일시정지·사망·구르기·UI 위 클릭·InputBlocked 상태에서는 공격하지 않습니다.
상점 등 후속 UI의 전체 입력 차단은 PlayerEquipmentCombat.InputBlocked에 연결해야 합니다.

Health는 선택적 ICombatDamageModifier가 있을 때만 일반 전투 피해를 보정합니다.
PlayerEquipment는 장비 방어력 합계를 일반 피해에서 고정 차감하고 최소 피해1을 적용합니다.
예: 방어20 + 피해25 → 실제5, 피해2 → 실제1. 낙하 피해와 기존 무적/사망 처리는 유지됩니다.
장비 컴포넌트가 없는 몬스터/기존 플레이어는 이전 피해 규칙을 그대로 사용합니다.

## 정식 프리팹 연결과 검증

정식 Player/지역 프리팹은 #89 소유 범위이므로 이번 변경에서 연결하지 않았습니다.
연결 시 PlayerEquipment/PlayerEquipmentCombat을 붙이고 Camera·Muzzle·Projectile·UnarmedDefinition 및
PlayerInventory.Items를 지정하세요. 장비 데이터가 있는 아이템만 장착 가능하며 기존 field-knife/vest는 자동 변환하지 않습니다.

전용 검증: `Nomad.Equipment.Editor.EquipmentVerification.Run`.
UI/입력/표시/3해상도 검증: `Nomad.Equipment.Editor.EquipmentUiVerification.Run`.
기존 저장씬 검증은 `RunExisting`, 드래그 중 UI 그리기 상태의 3해상도 스냅샷은 `RunDragCapture`를 사용합니다.
비동기 PlayMode 검사에는 자동 -quit이 붙지 않는 Unity 배치 실행을 사용합니다.
기존 회귀: ItemCsvVerification.Run, InventoryVerification.Run, PlayerCombatVerification.Run.
검증 결과와 미실행 항목은 PR #100에 기록합니다.

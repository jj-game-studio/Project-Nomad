# 몬스터 장비 드롭

현재 main에 등록된 실제 ItemDefinition과 LootChest를 사용합니다. 장비/스탯/CSV/가방 UI는 수정하지 않습니다.

사망 → 시각적 낙하(약0.65초) → 상호작용 가능한 장비 상자 → E로 기존 루팅 창 열기 → 드래그/더블클릭/전체 가져오기로 가방에 획득하는 흐름입니다. 자동 습득이나 자동 장착은 하지 않습니다.

- 기본 수량: 일반1, 엘리트1, 보스2. 가방이 가득 차면 바닥에 남으며 일부 획득 후 잔량을 보존합니다.
- 같은 사망에서 중복 생성하지 않으며 필드 재생성/이동 시 남은 드롭도 정리합니다.
- 가방이 없는 순수 몬스터 디버그는 기존 시각적 낙하만 유지합니다.
- 표: Assets/Nomad/Gameplay/Resources/MonsterEquipmentDrops.asset. Inspector에서 등급별 아이템 풀과 수량을 바꿀 수 있습니다. 동일 확률로 뽑으며 가능한 동안 보스의 같은 장비 중복을 피합니다.
- Nomad > Monsters > Refresh Equipment Drop Table은 현재 승인된 Equipment/Items에서 표를 만듭니다. 기본 풀은 현재 무기7종이며 일반은 낮은 수치 절반, 엘리트는 높은 수치 절반, 보스는 상위 장비입니다. 임시 밸런스이며 사람이 확인해야 합니다.
- 등록되지 않은 아이템 ID/장비가 아닌 항목은 생성에서 제외해 획득 불가능한 가짜 보상을 만들지 않습니다. 미병합 장비/티어 PR의 데이터는 임의 통합하지 않습니다.

MonsterDeathDrop은 Inventory를 직접 참조하지 않고 DropCreated 이벤트만 제공합니다. 같은 ActorFactory가 붙이는 MonsterEquipmentLoot가 데이터와 LootChest를 연결합니다. 기존 전투/다른 아이템 작업의 계약을 바꾸지 않습니다.

검증: 배치 Nomad.Gameplay.Editor.MonsterProgressionVerification.Run (Game) / RunTerrain (저장 TerrainIntegration). 실제 E·가방 포화/부분/반복 획득·보스 처치 후 안내 종료·이전 드롭 정리를 확인합니다.
# B 소품 배치와 상자 루팅 (#16)

## 현재 범위

`PropScatter`는 `TerrainGenerator`의 시작/완료 이벤트에 연결됩니다. 지형 성공 후 seed로 소품 후보를 고르고 generationId별로 한 번만 배치합니다. 재생성 시작·실패 시 이전 소품을 즉시 비활성화하고 정리합니다. 최종 필드 Ready나 NavMesh 검증은 #18입니다.

- 계단 조각 전체와 모든 연결점에서 조각 중앙까지의 통로를 예약합니다. 통로 반폭에 소품 반경 0.6m와 여유 0.1m를 추가하고 중앙 2m도 비웁니다.
- 평지·높은 평지에서 조각당 최대 50후보를 검사하며 기본 목표는 2개입니다. 공간이 부족하면 가능한 수량만 생성합니다. 배치 밀도는 검토용 초기값입니다.
- 중심/네 모서리가 같은 높이의 수평 지형 바닥에 지지되는지, 소품 점유 영역에 장애물이 없는지 확인합니다.
- 현재 정적 BoxCollider 소품만 지원합니다. 루트 배율 1, 원점은 바닥 중앙이며 메시와 충돌체는 X/Z ±0.6m, Y 0~1m 안에 있어야 합니다. Rigidbody·트리거·다른 충돌체 및 큰 외형은 거부합니다. B 미리보기는 구매 애셋 없이 1m 기본 큐브를 사용합니다.

`LootChest.TryLoot(PlayerInventory)`는 B의 수량 전송 핵심입니다. 보유 목록 순서대로 Add를 호출하고 실제 반환량만 차감합니다. 공간 부족과 미등록 ID는 보존합니다. 재진입을 차단하며 외부 인벤토리 알림에서 예외가 나도 반영된 수량을 재지급하지 않습니다. 목록 조회는 값 복사로 제공하며 초기화 입력도 복사합니다.

`Status`는 Closed / Remaining / Empty, `Message`는 결과 안내, `Changed`는 처리 후 알림입니다. Inspector의 closed/open/empty Visual에 서로 다른 자식 오브젝트를 지정하면 상태에 따라 표시합니다. ChestId와 InteractionPoint를 제공합니다.

## 명시적으로 남겨 둔 연결

2026-11-14 사용자 지시: **A 담당 구현을 기다리고 B 범위만 진행**합니다. 공용 CanInteract/Interact 계약, E키 입력, 가장 가까운 대상 선택·동거리 ID 정렬, 2m/시야/구르기 검사는 구현하지 않았습니다. A가 계약을 제공하면 B 상자에 어댑터를 연결해야 합니다. TryLoot는 그 검사를 대체하지 않습니다. 따라서 실제 플레이어 상호작용 완료와 #16 전체 완료를 뜻하지 않습니다. 상자 랜덤 배치는 #20입니다.

## 직접 검토

1. Unity 6000.6.9f1에서 `Nomad > Loot > Open B Props Loot Preview`를 선택합니다. 미저장 장면으로 만들며 기존 장면을 덮어쓰지 않습니다.
2. 소품은 (60,0,60) 부근의 생성 필드에 있습니다. Hierarchy에서 B Props를 선택하고 Scene 뷰에서 확인합니다. 기본 상자 외형은 별도 원점 부근 검토 바닥에 있습니다.
3. Play 후 선택된 상자의 `LootPreview` 컴포넌트 컨텍스트 메뉴에서 `1 Fill inventory` → `2 Direct loot`를 실행합니다. Console에서 added=3, remaining=7을 확인합니다. 다시 2를 실행하면 0/7입니다.
4. `3 Remove scrap` 후 2를 실행하면 7개를 회수하고 상자 외형이 낮아지며 Empty가 됩니다. 이 메뉴는 B 검토용 직접 호출이며 E키 상호작용이 아닙니다. 상태와 보유량은 LootChest/PlayerInventory Inspector의 디버그 표시 및 Console로 확인합니다.

## 자동 검증

프로젝트 이름 충돌을 피하려면 Unity CLI에 프로젝트의 **절대 경로**를 전달합니다.

```powershell
unity run '<checkout>/UnityProject' --timeout 900 --log-file '<checkout>/UnityProject/Artifacts/loot-props.log' --no-tail -- -executeMethod Nomad.Loot.Editor.LootVerification.Run
```

검증은 별도 빈 장면의 실제 Play Mode에서 실행합니다. 부분 추가 3/10 → 잔여 7, 반복 호출, 재진입, 빈 상자, 목록 순서, 알 수 없는 아이템, 외부 Changed 구독자 예외를 검사합니다. 실제 지형 9종을 30seed×2회 생성하고 소품 배치·통로 보호·수량 상한·재현성·중복 generationId·실패 정리와 프레임 경과 후 삭제를 확인합니다. 성공 표식은 `B_LOOT_PROPS_PLAYMODE_PASS`입니다.

실행 결과와 검토 커밋은 연결된 PR과 Issue #16에 기록합니다. 외형·밀도와 최종 플레이 수용은 사람 검토 대상입니다.

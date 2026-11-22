# B 소품 배치와 상자 루팅 (#16)

## 현재 범위

`PropScatter`는 `TerrainGenerator`의 시작/완료 이벤트에 연결됩니다. 지형 성공 후 seed로 소품 후보를 고르고 generationId별로 한 번만 배치합니다. 재생성 시작·실패 시 이전 소품을 즉시 비활성화하고 정리합니다. 최종 필드 Ready나 NavMesh 검증은 #18입니다.

- 계단 조각 전체와 모든 연결점에서 조각 중앙까지의 통로를 예약합니다. 통로 반폭에 소품 반경 0.6m와 여유 0.1m를 추가하고 중앙 2m도 비웁니다.
- 평지·높은 평지에서 조각당 최대 50후보를 검사하며 기본 목표는 2개입니다. 공간이 부족하면 가능한 수량만 생성합니다. 배치 밀도는 검토용 초기값입니다.
- 중심/네 모서리가 같은 높이의 수평 지형 바닥에 지지되는지, 소품 점유 영역에 장애물이 없는지 확인합니다.
- 현재 정적 BoxCollider 소품만 지원합니다. 루트 배율 1, 원점은 바닥 중앙이며 메시와 충돌체는 X/Z ±0.6m, Y 0~1m 안에 있어야 합니다. Rigidbody·트리거·다른 충돌체 및 큰 외형은 거부합니다. B 미리보기는 구매 애셋 없이 1m 기본 큐브를 사용합니다.

`LootChest.TryLoot(PlayerInventory)`는 B의 수량 전송 핵심입니다. 보유 목록 순서대로 Add를 호출하고 실제 반환량만 차감합니다. 공간 부족과 미등록 ID는 보존합니다. 재진입을 차단하며 외부 인벤토리 알림에서 예외가 나도 반영된 수량을 재지급하지 않습니다. 목록 조회는 값 복사로 제공하며 초기화 입력도 복사합니다.

`Status`는 Closed / Remaining / Empty, `Message`는 결과 안내, `Changed`는 처리 후 알림입니다. Inspector의 closed/open/empty Visual에 서로 다른 자식 오브젝트를 지정하면 상태에 따라 표시합니다. ChestId와 InteractionPoint를 제공합니다.

## E키 상자 연결

`LootChest`는 A의 `Nomad.Interaction.IInteractable`을 구현합니다. `InteractionId`는 기존 ChestId이며 `CanInteract`는 활성 상태·유효 ID·플레이어/인벤토리·잔여 수량·재진입을 확인합니다. `Interact`는 기존 `TryLoot(context.Inventory)`를 호출합니다. 빈 상자는 대상에서 제외하고, 공간 부족 시 남은 상자는 다시 시도할 수 있습니다. 거리·시야·구르기와 E 1회 입력은 기존 A `PlayerInteraction`이 담당합니다.

상자 루트 또는 항상 활성인 자식에 탐지 Collider를 두고 상호작용점을 그 안에 지정합니다. 상태별 Visual에는 Collider를 두지 않습니다. 상자마다 고유 ID를 지정하고, 플레이어 루트에는 PlayerMovement·CharacterController·PlayerInventory·PlayerInteraction을 연결합니다. 상자 랜덤 배치는 #20의 `ChestSpawner`입니다.

### 실제 E키 검토

1. `Nomad > Loot > Open B Chest E Key Preview` → Play → Game 탭을 클릭합니다. A 인벤토리 장면을 열어 고정 상자를 추가하지만 원본 장면은 저장하지 않습니다. 검토 후 변경된 A 장면을 저장하지 마세요. 기존 이동·카메라·인벤토리 UI를 재사용합니다.
2. 초기 인벤토리는 Scrap 96/99와 칼 19개로 가득 차 있습니다. 시작점 왼쪽 앞의 상자에서 **E** → Scrap 3개 획득, 상자 7개 잔여를 확인합니다. **Tab**으로 실제 인벤토리를 확인합니다.
3. E를 계속 누르거나 다시 눌러도 공간이 없으면 상자 7개가 보존됩니다. **G**로 Scrap 공간을 비운 뒤 E → 남은 7개 획득, Empty입니다. 다시 E를 눌러도 복제되지 않습니다.
4. **F**는 인벤토리를 다시 채우는 검토 도구이며 상자 수량을 초기화하지 않습니다. 처음부터 다시 검토하려면 Play를 종료하고 재시작합니다. 화면 아래에 입력과 상자 상태·잔여 수량을 표시합니다.
5. 상자에서 멀어지거나 벽 뒤에 있거나 구르는 중에는 획득되지 않습니다. #16의 새 입력 흐름에 대한 사람 플레이 수용은 별도 검토합니다.

## 직접 검토

1. Unity 6000.6.9f1에서 `Nomad > Loot > Open B Props Loot Preview`를 선택하고 **Play 후 Game 탭을 클릭**합니다. 이전 버전에서 만든 장면은 메뉴로 다시 만들어야 새 검토 도구가 들어갑니다. 미저장 장면을 만들며 기존 장면을 덮어쓰지 않습니다.
2. 필드의 안전한 평지에 검토용 플레이어를 배치합니다. 기존 PlayerMovement·TopDownCamera를 그대로 재사용하며 Game 화면에서 WASD 이동, Space 점프, 왼쪽 Shift 구르기로 확인합니다. 별도 전체 보기/시점 전환은 없습니다.
3. **R**은 새 랜덤 seed로 지형·소품을 재생성합니다. **T**는 화면에 표시된 seed로 같은 배치를 재현합니다. Play를 시작할 때도 새 seed를 사용합니다. 매번 플레이어를 안전한 시작 위치로 되돌리며 이동/구르기 상태도 초기화합니다. 추락하면 시작점으로 복귀합니다.
4. Game 화면 왼쪽 위에서 현재 seed, Ready/실패 상태, 지형 수와 소품 수, 조작 안내를 확인합니다. 생성 실패나 안전한 시작점 부재 시 플레이어를 활성화하지 않습니다. R로 다시 시도할 수 있습니다.
5. 상자 수량 검토는 별도 원점 부근 바닥의 `B Chest` 오브젝트에 있는 `LootPreview` 컴포넌트 컨텍스트 메뉴를 사용합니다. `1 Fill inventory` → `2 Direct loot`는 3개 획득/7개 잔여, 다시 2는 0개, `3 Remove scrap` 후 2는 남은 7개 회수/Empty입니다. 이는 기존 B 직접 호출 도구입니다. 실제 E 입력 검토는 위 전용 메뉴를 사용합니다.

## 자동 검증

Unity CLI의 `run`은 자동으로 -quit를 붙여 비동기 Play Mode 검증 전에 종료합니다. 아래와 같이 프로젝트 지정 Editor를 -quit 없이 실행합니다. 프로젝트는 **절대 경로**로 전달합니다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.6.9f1/Editor/Unity.exe' -batchmode -projectPath '<checkout>/UnityProject' -executeMethod Nomad.Loot.Editor.LootVerification.Run -logFile '<checkout>/UnityProject/Artifacts/loot-props-play.log'
```

검증은 별도 빈 장면의 실제 Play Mode에서 실행합니다. 부분 추가 3/10 → 잔여 7, 반복 호출, 재진입, 빈 상자, 목록 순서, 알 수 없는 아이템, 외부 Changed 구독자 예외를 검사합니다. 실제 지형 9종을 30seed×2회 생성하고 소품 배치·통로 보호·수량 상한·재현성·중복 generationId·실패 정리와 프레임 경과 후 삭제를 확인합니다. 성공 표식은 `B_LOOT_PROPS_PLAYMODE_PASS`입니다.

같은 실행 명령에서 메서드를 `Nomad.Loot.Editor.FieldPreviewVerification.Run`으로 바꾸면 실제 R/T 키 입력, seed 변경과 동일 배치 재현, HUD 갱신, 플레이어 바닥 지지·이동·기존 카메라 추적·추락 복귀, 재생성 정리를 검증합니다. 성공 표식은 `FIELD_PREVIEW_PLAYMODE_PASS`이며 `Artifacts/field-preview.png`에 실제 카메라와 HUD를 렌더합니다. B 검토 전용 플레이어 배치는 #18의 정식 플레이어 시작/Ready 계약을 대체하지 않습니다.

실행 결과와 검토 커밋은 연결된 PR과 Issue #16에 기록합니다. 외형·밀도와 최종 플레이 수용은 사람 검토 대상입니다.

### E키 통합 자동 검증

위 Editor 명령의 메서드를 `Nomad.Loot.Editor.ChestInteractionVerification.Run`으로, 로그 경로를 `Artifacts/chest-interaction.log`로 바꿉니다. 실제 가상 키보드 입력을 A PlayerInteraction에 전달해 부분 획득 3/10, 길게 누르기, 가득 참 재시도, G 공간 확보 후 잔여 7개, 빈 상자 중복 방지, 상태 전환 후 Collider 유지, 2m 밖·벽 차폐·구르기 차단 및 구르기 후 새 E를 검사합니다. 성공 표식은 `B_CHEST_INTERACTION_PASS`이며 카메라/HUD 렌더는 `Artifacts/chest-e-preview.png`입니다. 원본 A 장면과 공용 입력 코드는 수정하지 않습니다.

## 저장 지형 통합 씬 연결

`Nomad > Terrain > Open Integration Test Scene`에서 상자를 E키로 루팅합니다. #124의 필드 및 검토 씬은 기존 소품 큐브 위치에서 접근 가능한 곳을 `ChestPoints`로 사용합니다. 경로 검사가 끝나면 큐브를 끄고, `ChestSpawner`가 확인된 위치마다 신규 상자 하나를 배치합니다. 재생성 시작에 이전 상자를 정리하고, 같은 세대는 한 번만 배치합니다. 통합 씬에서 **G**는 갤러리, **V**는 가방 비우기, **F**는 가방 채우기입니다. [상세 검증](terrain-integration.md)

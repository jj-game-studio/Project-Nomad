# Temporary shop

## UI 테마

크림색 카드·짙은 숲색 헤더·금색 선택 테두리를 사용합니다. 창·슬롯·버튼·수량 칸은
`ShopRoundedRect`가 둥근 모서리와 일정한 테두리를 직접 그려 크기가 바뀌어도 모서리가 늘어나지 않습니다.
아이템 수량은 별도 배지에 표시하고 빈 칸의 반복 문구는 생략합니다.

제목은 Lilita One, 본문은 Varela Round입니다. 원본 폰트를 수정 없이 사용하며,
출처·SHA-256·SIL OFL 1.1 원문은 `Assets/StreamingAssets/ThirdPartyLicenses/ShopFonts/`에 보관합니다.
StreamingAssets의 라이선스 파일은 플레이어 빌드에도 포함됩니다.

## 플레이

### 마을 상점

`Nomad > Shop > Open Village Shop`에서 `Village.unity`를 열고 Play합니다.
남쪽 입구에서 광장으로 올라가면 남서쪽 시장 노점 앞에 `MARKET / [E] Trade` 표시가 있습니다.
표시에서 2m 안으로 접근해 E로 거래합니다. 마을 Player에 실제 가방·지갑·상호작용이 연결되어 있으며
시작 재화는 1,000 G, 가방은 비어 있습니다. 데모 아이템을 무료로 지급하지 않습니다.

`Nomad > Shop > Place Shop In Village`는 기존 마을에 상점만 연결하며 반복해도 중복 생성하지 않습니다.
`Nomad > Village > Create Village`로 마을 전체를 재생성할 때도 같은 상점 연결을 수행합니다.
시장 노점 앞의 접근점·트리거만 추가하고 기존 마을 건물·길·계단은 유지합니다.

### 거래 정보

- 상점 상품은 All / Materials / Equipment / Supplies로 분류하며 빈 분류도 명확히 안내합니다.
- 선택한 상품의 설명·단가·총액과 거래 가능 여부를 표시합니다. 품절 상품은 목록에 남겨 둡니다.
- `Buy limit`는 소지금·재고·남은 가방 공간 중 가장 작은 수량을 선택합니다. 누르기만 해서는 구매하지 않습니다.
- 기존 Max/Shift+클릭은 재고·보유 수량 전체 선택을 유지합니다. 돈·공간 부족 사유를 먼저 표시합니다.
- 매입하지 않는 아이템은 `Not bought`로 표시하고 판매 버튼을 비활성화합니다.
- 구매·판매 버튼의 상태와 실제 거래가 동일한 검사를 사용합니다.
- `Restock in`에 다음 재입고까지 남은 시간을 표시합니다. 임시 주기는 게임 시간 300초이며 일시정지 중에는 멈춥니다.
- 주기가 지나면 부족한 상품을 초기 재고까지 채웁니다. 플레이어가 판매해 초기값을 넘은 재고는 줄이지 않습니다.
- 창을 닫거나 상점을 잠시 비활성화해도 보충 시각은 유지하며, 복귀 시 지난 주기를 한 번에 반영합니다.
- 가방 아이템을 고른 뒤 `Keep item [L]`로 판매를 잠급니다. 같은 아이템의 모든 스택에 `KEEP`가 표시됩니다.
- 잠금은 슬롯 이동·상점 재열기 뒤에도 유지합니다. `Unlock sale [L]`로 풀기 전에는 V/Enter와 직접 거래 API도 판매하지 않습니다.
- `After trade`는 현재 선택 수량으로 거래한 뒤의 예상 잔액입니다. 거래 불가 상태에서는 `--`로 표시합니다.

덕코프의 상인/자판기에서 상품을 확인하고 구매·판매하는 흐름을 참고했습니다.
참고: [거래 화면 조작 사례](https://www.youtube.com/watch?v=sX1XS5NfSNA),
[개발사 배포 페이지의 품절 상품 유지 변경 기록](https://apps.apple.com/vc/app/escape-from-duckov/id6743841750?mt=12).
분류·구매 한도·판매 잠금 조작과 재입고 주기는 이 프로젝트에 맞춘 구현이며 덕코프와 동일한 규칙을 뜻하지 않습니다.

### 독립 테스트

`Nomad > Shop > Open Shop Debug`에서 씬을 열고 Play를 누릅니다.
씬이 없으면 `Assets/Nomad/Shop/Editor/Generated/ShopDebug.unity`를 생성합니다. 시작 위치에서 상점과 거래할 수 있습니다.
테스트 씬과 전용 재질은 필요할 때 만드는 로컬 산출물로 Git에서 제외합니다. 상점 검증도 먼저 같은 생성 경로를 실행합니다.

| 입력 | 동작 |
| --- | --- |
| WASD | 창이 닫혔을 때 이동 |
| E | 2m 이내 상점 열기 / 닫기 |
| 방향키 / 마우스 클릭 | 상품 선택 |
| Shift + 클릭 / Max 버튼 | 선택한 칸의 보유 수량·상점 재고 전량 선택 (즉시 거래하지 않음) |
| Tab | 상점 / 내 가방 목록 전환 |
| 수량 입력 칸 | 구매·판매 개수 직접 입력 (1~선택한 칸 수량) |
| + / - 또는 화면 버튼 | 거래 수량 1개씩 변경 |
| B / 구매 버튼 | 선택한 상점 상품 구매 |
| V / 판매 버튼 | 선택한 가방 슬롯 아이템 판매 |
| L / Keep item 버튼 | 선택한 가방 아이템의 판매 잠금·해제 (같은 아이템 모든 스택) |
| Enter | 현재 목록에서 구매 또는 판매 |
| Esc / 닫기 버튼 | 상점 닫기 |

수량 편집 중 Enter는 입력만 확정하고, 거래 단축키는 작동하지 않습니다. Esc/Tab은 편집을 끝냅니다.
빈 값·0은 거래할 수 없으며 편집 종료 시 1로 보정하고, 초과 수량은 재고/보유 수량으로 제한합니다.
숫자만 입력할 수 있습니다. 빈 칸/품절은 수량 0으로 표시하고 입력·거래를 비활성화합니다.

왼쪽은 내 가방 20칸, 오른쪽은 상점 재고입니다. 각 칸에 단가와 수량을 표시합니다.
선택한 아이템의 총액과 현재 재화를 함께 확인할 수 있습니다. 상품이 많으면 목록을 스크롤합니다.
창이 열린 동안 이동·점프·구르기·공격·다른 상호작용은 차단하며, 거리 이탈·가림·사망·비활성화 때 닫습니다.
게임 시간은 멈추지 않습니다.

## 임시 값과 재사용

- 시작 재화: `PlayerWallet.startingGold`, 데모 1,000 G. 재화 지급은 `TryAddGold`를 사용합니다.
- 상품 설정: `Data/VillageMarket.asset` (`ShopCatalog`). 아이템·구매 단가·판매 단가·초기 재고를 설정합니다.
- 재입고 주기: `ShopCatalog.restockSeconds`. 0이면 자동 보충하지 않습니다. 300초는 이번 구현의 테스트 값이며 덕코프의 실제 주기를 뜻하지 않습니다.
- 임시 구매/판매 단가: Scrap 5/2 G, Field Knife 120/60 G, Field Vest 200/100 G.
- 카탈로그의 판매 단가를 0으로 두거나 상품에서 제외하면 그 아이템을 매입하지 않습니다.
- 마을 배치: `Place Shop In Village` 메뉴를 사용합니다. 다른 장소에는 Shopkeeper와 접근용 트리거를 배치하고 카탈로그를 연결합니다. 각 상점의 `interactionId`는 고유하게 지정합니다.
- 플레이어는 `PlayerInventory`, `PlayerWallet`, `PlayerInteraction`이 필요합니다. 구매할 ItemDefinition은 플레이어 인벤토리의 items에도 같은 에셋으로 등록합니다.
- 상호작용 지점은 상점 원점 위 0.5m입니다. 원점은 카운터 앞, 플레이어가 다가갈 수 있는 바닥에 둡니다.
- 탐지/가림 레이어는 기존 PlayerInteraction 설정과 Shopkeeper.obstacleLayers를 함께 맞춥니다.
- 기본 아이템 정의와 인벤토리 API를 재사용합니다. 상점 가격은 아이템 공용 정의를 수정하지 않습니다.

재화·재고는 현재 플레이 세션 동안 유지됩니다. 저장, 동적 시세, 장비 착용은 포함하지 않습니다.
판매 잠금도 같은 PlayerWallet이 살아 있는 세션에 한정됩니다. 판매 보호는 소모/버리기를 막는 기능이 아닙니다.
현재 마을 씬을 다시 로드하면 지갑·재고는 초기화됩니다. #89의 영속 Player/지역 전환은 별도 작업입니다.
가격·시작 재화는 테스트 값이며 최종 경제 규칙은 사람 검토 대상입니다.
씬 재생성 메뉴는 `Nomad > Shop > Rebuild Shop Debug`이며 기존 카탈로그의 사용자 설정은 보존합니다.
미사용 데모 프리팹과 전용 씬/재질의 저장본은 제거했습니다. 실사용 카탈로그의 GUID는 유지해 마을 씬 참조를 보존합니다.

## 검증

Unity를 종료한 전용 checkout에서 아래 명령을 실행합니다. 검증은 완료 후 Editor를 종료합니다.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe' -batchmode -projectPath "$PWD\UnityProject" -executeMethod Nomad.Shop.Editor.ShopVerification.Run -logFile "$PWD\shop-verification.log"
```

거래 실패 시 무변경, 중첩, 재고·재화 오버플로, 알림 일관성·재진입, 키보드 및 실제 마우스 클릭,
재열기, 입력 차단, 거리 이탈·사망 시 닫기를 플레이 모드에서 확인합니다.
로그의 `SHOP_PLAYMODE_PASS`가 통과 표식이고 `UnityProject/Artifacts/shop-ui.png`가 화면 캡처입니다.
재입고 검증 통과 표식은 `SHOP_RESTOCK_PASS`입니다. 실제 Play 시간 경과로 품절→재입고,
비활성화 중 여러 주기 경과·초과 재고 보존·일시정지/재개·주기0 설정을 확인합니다.
판매 잠금·해제·재열기·직접 API 차단·예상 잔액은 기존 UI/거래 검사에 포함합니다.
새 checkout에서 발생할 수 있는 Unity Search 시작 예외는 `SHOP_VERIFY_ENVIRONMENT`로 별도 보고하며 게임 검사와 구분합니다.

마을 통합 검증은 위 명령의 진입점을 `Nomad.Shop.Editor.VillageShopVerification.Run`으로 바꿉니다.
저장된 상점·재배치 멱등성, 입구에서 시장까지 충돌을 거친 도보 접근, E 열기·구매·판매·재열기,
상점 비활성화 후 UI/입력 잠금 해제와 렌더를 확인합니다. 통과 표식은 `VILLAGE_SHOP_PLAYMODE_PASS`입니다.
결과 이미지는 `village-shop-location.png`, `village-shop-ui.png`입니다.

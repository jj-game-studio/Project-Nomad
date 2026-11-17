# Temporary shop

## UI 테마

크림색 카드·짙은 숲색 헤더·금색 선택 테두리를 사용합니다. 창·슬롯·버튼·수량 칸은
`ShopRoundedRect`가 둥근 모서리와 일정한 테두리를 직접 그려 크기가 바뀌어도 모서리가 늘어나지 않습니다.
아이템 수량은 별도 배지에 표시하고 빈 칸의 반복 문구는 생략합니다.

제목은 Lilita One, 본문은 Varela Round입니다. 원본 폰트를 수정 없이 사용하며,
출처·SHA-256·SIL OFL 1.1 원문은 `Assets/StreamingAssets/ThirdPartyLicenses/ShopFonts/`에 보관합니다.
StreamingAssets의 라이선스 파일은 플레이어 빌드에도 포함됩니다.

## 플레이

`Nomad > Shop > Open Shop Debug`에서 씬을 열고 Play를 누릅니다.
씬 파일은 `Assets/Nomad/Shop/ShopDebug.unity`입니다. 시작 위치에서 상점과 거래할 수 있습니다.

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
- 상품 설정: `Demo/TemporaryShop.asset` (`ShopCatalog`). 아이템·구매 단가·판매 단가·초기 재고를 설정합니다.
- 임시 구매/판매 단가: Scrap 5/2 G, Field Knife 120/60 G, Field Vest 200/100 G.
- 카탈로그의 판매 단가를 0으로 두거나 상품에서 제외하면 그 아이템을 매입하지 않습니다.
- 배치: `Demo/TemporaryShop.prefab`을 놓습니다. 각 상점의 `interactionId`는 고유하게 지정합니다.
- 플레이어는 `PlayerInventory`, `PlayerWallet`, `PlayerInteraction`이 필요합니다. 구매할 ItemDefinition은 플레이어 인벤토리의 items에도 같은 에셋으로 등록합니다.
- 상호작용 지점은 상점 원점 위 0.5m입니다. 원점은 카운터 앞, 플레이어가 다가갈 수 있는 바닥에 둡니다.
- 탐지/가림 레이어는 기존 PlayerInteraction 설정과 Shopkeeper.obstacleLayers를 함께 맞춥니다.
- 기본 아이템 정의와 인벤토리 API를 재사용합니다. 상점 가격은 아이템 공용 정의를 수정하지 않습니다.

재화·재고는 현재 플레이 세션 동안 유지됩니다. 저장, 동적 시세, 마을 배치, 장비 착용은 포함하지 않습니다.
가격·시작 재화는 테스트 값이며 최종 경제 규칙은 사람 검토 대상입니다.
씬 재생성 메뉴는 `Nomad > Shop > Rebuild Shop Debug`이며 기존 카탈로그의 사용자 설정은 보존합니다.

## 검증

Unity를 종료한 전용 checkout에서 아래 명령을 실행합니다. 검증은 완료 후 Editor를 종료합니다.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe' -batchmode -projectPath "$PWD\UnityProject" -executeMethod Nomad.Shop.Editor.ShopVerification.Run -logFile "$PWD\shop-verification.log"
```

거래 실패 시 무변경, 중첩, 재고·재화 오버플로, 알림 일관성·재진입, 키보드 및 실제 마우스 클릭,
재열기, 입력 차단, 거리 이탈·사망 시 닫기를 플레이 모드에서 확인합니다.
로그의 `SHOP_PLAYMODE_PASS`가 통과 표식이고 `UnityProject/Artifacts/shop-ui.png`가 화면 캡처입니다.
새 checkout에서 발생할 수 있는 Unity Search 시작 예외는 `SHOP_VERIFY_ENVIRONMENT`로 별도 보고하며 게임 검사와 구분합니다.

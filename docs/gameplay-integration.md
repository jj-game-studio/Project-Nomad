# 통합 게임플레이 (#40)

관련 PR #59. main의 지형/상자/경로(#56/#57/#58)와 몬스터 전투 PR #52를 별도 통합 브랜치에서 연결합니다. #52 개별 승인/병합 또는 상위 #19/#39/#20 전체 완료를 의미하지 않습니다.

## 실행과 플레이 흐름

Unity6000.6.9f1에서 `Nomad > Gameplay > Open Integrated Gameplay`를 실행합니다. 기존 `Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity`를 그대로 사용하며 새 별도 게임 씬을 만들지 않습니다. `Nomad > Terrain > Open Integration Test Scene`으로 열어 Play해도 같은 결과입니다.

필드 생성 → 소품/통행 검사 → 지속 NavMesh 준비 → 안전 시작점에 플레이어 배치 → 유효 지점에 잡몹/엘리트 생성 → 탐험·사격/회피·상자 획득 → 사망 시 런 종료/재시작.

| 입력 | 동작 |
| --- | --- |
| WASD / Space / Shift | 이동 / 점프 / 구르기 |
| 마우스 좌클릭 | 기존 PlayerCombat 사격·실제 발사체 피해 |
| E | 접근한 기존 생성 상자를 인벤토리에 획득 |
| R / T (생존 중) | 개발용 새 필드 / 같은 seed 재현. 현재 HP·인벤토리 유지 |
| R 또는 Restart run (사망 후) | 씬을 다시 로드하여 HP·인벤토리·전투를 새 런으로 초기화 |
| F1 | 지형 진단 HUD와 범위·피격 표시 디버그 토글 |
| O / G / P / L | 기존 전체 필드/갤러리/소품/구형 지형 비교 도구 |

일반 플레이에서는 작은 HP/적 수/Scrap/슬롯 HUD만 표시합니다. 플레이어/몬스터 머리 위 체력바, 공격 준비 표시와 피격 연출은 같은 몬스터 시스템을 사용합니다. overview/갤러리에서는 사격과 상호작용을 막습니다. 현재 R/T/F/V 및 비교 도구는 개발 기능이며 배포용 입력/경제 규칙이 아닙니다.

## 연결 책임과 세대 수명

- `TerrainIntegrationControls`는 기존 생성 순서를 유지하고 `FieldNavigation`의 성공 결과를 공개합니다. `GameplayIntegration`은 생성 시작/완료를 구독하며 이미 완료된 결과도 한 번만 소비합니다.
- 생성 시작 즉시 이전 몬스터 루트와 해당 씬의 발사체를 비활성화한 뒤 제거합니다. 세대 ID로 중복 배치를 막고 실패한 필드에는 적을 생성하지 않습니다.
- `EnemyPoints`를 seed 기반으로 섞고 시작점12m 밖의 유효 지점에 일반6/엘리트1을 임시 배치합니다. 공간이 부족하면 가능한 수만 배치하며 무한 재시도하지 않습니다. 이 수량과 안전 거리는 테스트 설정이고 최종 난이도가 아닙니다.
- 기존 몬스터 테스트 스폰창과 생성 필드가 `MonsterActorFactory`를 공유합니다. Health/Tracker/MeleeAttack/분할 HP/원형 탐지/범위 예고/피격 연출을 재사용합니다. 독립 지형 NavMesh를 추가로 굽지 않습니다.
- 같은 플레이어에 기존 `PlayerMovement`, `Health`, `PlayerCombat`, `PlayerInventory`, `PlayerInteraction`을 연결합니다. 공용 Health/Player/상자 핵심 계약은 수정하지 않습니다.
- 사망 이벤트에서 지형 재생성 입력/사격/상호작용을 잠그고 기존 몬스터는 목표 사망 계약으로 멈춥니다. 재시작은 런 전체 씬을 다시 로드하므로 체력·인벤토리·이전 발사체가 초기화됩니다.
- 기존 `Rebuild Integration Test Scene`은 Gameplay가 붙은 씬에 한해 연결을 복구합니다. 단순 지형 씬에 Gameplay를 강제로 추가하지 않습니다. 최초 연결 메뉴는 `Nomad > Gameplay > Connect Existing Integration Scene`입니다.

## 부족한 부분과 다음 순서

| 우선순위 | 부족한 부분 | 현재 연결된 범위 / 다음 작업 |
| --- | --- | --- |
| 1 | 성공 런의 끝인 탈출 | 이동·전투·파밍·사망/재시작은 연결. 탈출 지점/조건/제한 시간/전리품 확정이 없어 성공 종료 루프가 필요 |
| 2 | 획득한 아이템이 전투를 바꾸는 장비 연결 | 현재 상자 Scrap 획득과 슬롯 표시. 무기/탄약·재장전·방어구/장착/스탯 반영 없음 |
| 3 | 정식 배치와 보상 | 일반6/엘리트1 임시 배치, 기존 소품 상자 루팅 재사용. #39의 수량/난이도 정책, #20의 ChestPoints 배치, 적 드롭/보상 필요 |
| 4 | 마을·경제·진행 | 거래/시세/상점·거점 이동·지역 해금·저장/불러오기·사망 후 메타 성장 미연결 |
| 5 | 콘텐츠·게임 피드백 | 보스 용/맵별 몬스터·모델·애니메이션·사운드 미적용. 기존 무료 팩은 원본 보관만 완료 |
| 6 | 배포 검증 | Windows 플레이어 빌드/빌드 씬 등록·해상도/성능·사람 플레이 수용 검토 필요 |

현재 결과를 완성된 익스트랙션 게임이나 최종 밸런스로 보고하지 않습니다. 우선 성공 탈출을 결정해 실패/성공이 모두 끝나는 한 런을 만드는 것이 다음 제품 판단입니다.

## 검증 방법

`Nomad.Gameplay.Editor.GameplayIntegrationVerification.Run`을 Unity 별도 배치 실행에서 호출합니다. 검증기는 종료 시 Editor를 닫는 자동화 전용이며, 사람이 플레이할 때는 Open Integrated Gameplay 메뉴를 사용합니다.

저장된 기존 통합씬에서 seed0/7/51과 재현,7마리 유효 배치·세대 정리, 실제 Tracker의 높이 이동, 실제 PlayerCombat/발사체, HUD 입력 차단, 실제 E키 상자→동일 인벤토리, 비행 중 발사체 정리, 실제 근접 피해로 플레이어 사망, 입력 잠금, 씬 재로드 후 초기화를 확인합니다. 몬스터 회귀는 `scripts/Test-MonsterTracking.ps1`로 별도로 실행합니다.

자동 검사는 조작감·재미·난이도 수용이나 Windows 빌드 실행을 대신하지 않습니다. 기존 Unity Search 초기 색인 예외가 발생하면 기능 성공 표식과 로그 예외를 구분하여 보고합니다.

## 검증 결과 (2026-11-17)

- `GAMEPLAY_INTEGRATION_PASS`, 종료0:실제 저장 씬에서 seed0/7/51 및 동일seed 재현,6+1 NavMesh 유효 배치/시작점 안전거리, 세대 교체, 실제 Tracker의 높이 차 이동, PlayerCombat/발사체4회로 일반몹 사망, 작은 화면 HUD 입력 차단, 실제 E키 상자→같은 인벤토리, 비행 중 발사체 제거, 실제 근접 피해에 의한 사망/입력 잠금, 이전 런이 제거된 뒤 새 런의 HP100/빈 인벤토리/7마리 복구까지 확인했습니다.
- 기존 몬스터 PlayMode46/46 통과, 실패/건너뛰기0, 종료0,78.3초, 실행기 로그 진단없음. 근거 `Artifacts/tests/monster-tracking/20261117-025400-6f64c218`.
- 기존 상자 `TERRAIN_LOOT_PASS`, 종료0:25개 생성 상자, 실제 E/F/V/T/P 입력, 부분3개/잔여7개, 빈 상자 복제 방지/콜라이더/HUD/재생성·재현 검사 통과.
- 기존 지형 `MICRO_PLAY_PASS`, 종료0:동일 저장 씬324타일/2916도달성 표본, T/R/O/G/L/P/W 입력, 원형11종 갤러리, 카메라/이동/낙하 복귀 검사 통과.
- Editor 기반 통합/상자/지형 실행 로그에는 기존 `UnityEditor.Search.SearchDatabase` 초기 색인의 `ArgumentOutOfRangeException`이 남아 있습니다. 기능 검사는 성공 표식/종료0까지 완료했지만 전체 로그가 무오류라고 보고하지 않습니다.
- 로컬 근거:`Artifacts/gameplay-integration-results.txt`, `gameplay-integration-verification.log`, `gameplay-loot-regression.log`, `gameplay-terrain-regression.log`. 실제 게임 화면 캡처는 `Artifacts/integrated-gameplay-player.png`입니다. Artifacts는 Git 추적에서 제외합니다.

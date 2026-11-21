# 메인 게임 씬과 모듈 연결 (#89)

`Game.unity`(Build 0번)에는 `GameManager` 하나만 있습니다. 기능은 씬이 아니라 `Assets/Nomad/Game/Resources/GameConfig.asset`에 프리팹을 등록해 붙입니다.

## 구조

- **영속 모듈** (`GameConfig.modules`): GameManager 아래에 한 번 생성되어 지역이 바뀌어도 유지됩니다. 현재 `PlayerRig`(Player·Top Down Camera·EventSystem)가 등록되어 있습니다.
- **지역 씬** (`GameConfig.regionScenes`): `Village`, `Field`. additive로 로드하고 이전 지역은 언로드합니다. 지역 씬을 단독 Play해도 GameManager와 같은 Player가 생성됩니다.
- **지역 모듈** (`GameConfig.regionModules`): 지정한 지역에 들어올 때마다 그 지역 씬에 생성되고 지역과 함께 사라집니다. 몬스터 배치처럼 필드에만 필요한 기능을 여기에 등록합니다. 현재 Field에 `FieldMonsters`가 등록되어 있습니다.

## Player

- `GameManager.Instance.Player`: `Player` 태그가 붙은 영속 Player. 이동·체력·가방·장비 전투·HUD·퀵슬롯·상호작용·월드 HUD를 모두 가집니다.
- 지역 전환 중에는 숨겨지고, 위치가 정해지면 `GameManager.PlacePlayer`로 활성화됩니다.
- 런타임에 만드는 UI 루트는 지역 씬이 아니라 소유자(Player)와 같은 씬에 둡니다. 새 UI를 만들 때는 `SceneManager.MoveGameObjectToScene(root, gameObject.scene)`을 따릅니다.
- 시작 장비(`StarterKit`: 검·활)는 기획 확정 전 임시값입니다.

## 지역 입출구

- `RegionEntrance`: 지역에 들어올 때 서는 위치. `fromRegion`이 이전 지역과 같은 입구를 먼저 쓰고, 없으면 기본 입구(빈 값)를 씁니다.
- `RegionExit`: 트리거에 Player가 들어오면 `targetRegion`으로 전환합니다. 마을의 `Field Exit Trigger`에 붙어 있습니다.
- Field는 입구 대신 `FieldRegion`이 지형·경로가 Ready된 뒤 생성 시작점에 Player를 세우고, 필드 출구(`FieldExit`)에 닿으면 Village로 돌아갑니다.
- Field 지형은 #73 TerrainTest(Perlin 언덕 Terrain 지면·꾸밈·조명·Volume)를 따릅니다(#94). 필드에 있는 동안만 영속 카메라에 후처리·SMAA·단색 배경을 적용하고, 떠나면 원래대로 되돌립니다. 진입할 때마다 새 seed로 생성합니다(검토 씬은 고정 seed).

## 필드 몬스터 (#89 5단계)

- `FieldMonsters` 프리팹(`Game/Prefabs`): 필드 생성이 끝나면 적 생성 지점을 seed로 섞어 일반 6(검·석궁·지팡이 번갈아)·엘리트 1·보스 1을 시작점 12m 밖에 배치하고, 다시 생성하면 이전 몬스터를 지웁니다. 수량·거리는 #59와 같은 임시값입니다.
- 몬스터는 #59의 `MonsterActorFactory`로 만들고 `GameManager.Instance.Player`의 Health를 노립니다. 필드 씬과 함께 사라집니다.
- 플레이어 사망 뒤 처리(재시작·귀환)는 아직 없습니다.

## 기능 추가 방법

1. 기능을 프리팹으로 만들고, Player·지역 참조는 직렬화하지 말고 `GameManager.Instance.Player`·`RegionLoaded`에서 찾습니다.
2. 필드 생성 결과가 필요하면 `FieldNavigation`의 `State == Ready`이면 `LastResult`를 바로 쓰고, 아니면 `GenerationCompleted`를 구독합니다(지역 모듈은 생성 완료 뒤에 만들어질 수 있습니다).
3. `GameConfig`의 `modules` 또는 `regionModules`에 등록합니다. Game 씬은 수정하지 않습니다.

## 다시 만들기

`Nomad > Game > Build Game Content`가 최신 main 기준으로 Player 프리팹, `Field.unity`(TerrainTest 복사본에서 검토용 Player·카메라·상태 UI·디버그 키 제외), 마을 입출구, 필드 몬스터 모듈, GameConfig·Build Settings를 다시 만듭니다. 몬스터 모듈만 다시 만들 때는 `Nomad > Game > Build Field Monsters`를 씁니다. 각 기능의 원본 테스트 씬은 수정하지 않습니다. 다른 작업이 GameConfig에 등록한 모듈은 유지합니다.

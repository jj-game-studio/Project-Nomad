# TerrainTest 씬과 필드 꾸밈 (#73 1단계)

관련 작업: [#73](https://github.com/jj-game-studio/Project-Nomad/issues/73). 기준: [3m 지형과 단일 통합 테스트](terrain-integration.md).

Duckov 참고 화면처럼 덜 밋밋한 필드를 만들기 위한 1단계입니다. **지형 생성(WFC·높이 골격·29후보)은 바꾸지 않고**, 생성된 필드 위에 Kingdom 식생·엄폐물·절벽 바위와 조명·포스트 프로세싱을 더합니다. 2단계(Unity Terrain 기반 고저차 표현)는 1단계 사람 검토 후 결정합니다.

## 씬

`Assets/Nomad/Terrain/Test/TerrainTest.unity`를 사용합니다. 몬스터 작업 등이 사용하는 `TerrainIntegration.unity`는 수정하지 않습니다.

- `Nomad > Terrain > Dressing > Open TerrainTest Scene`으로 엽니다. 씬이 없으면 통합 씬을 복사해 만듭니다.
- `Rebuild TerrainTest Scene`은 통합 씬을 다시 복사하고 아래 구성을 덧붙입니다. TerrainTest를 직접 편집했다면 덮어쓰므로 주의합니다.
- 입력은 통합 씬과 같습니다(R/T 재생성, O 전체 보기, WASD 이동 등).

덧붙이는 구성:

| 항목 | 값 |
| --- | --- |
| 방향광 | 회전 (38, -40), 따뜻한 색, 강도 1.6, Soft 그림자 |
| 환경광 | Trilight (하늘/지평/지면) |
| Volume | `TerrainTestVolume.asset`: Neutral 톤매핑, 블룸, 색보정, 화이트밸런스, 비네트 |
| 카메라 | 포스트 프로세싱·SMAA 활성, 단색 배경. 시점은 기존 `TopDownCamera`(약 56°, FOV 50) 유지 |

SSAO는 프로젝트 공용 `PC_Renderer`에 이미 있습니다. 수치는 검토용 초기값입니다.

## 꾸밈 배치 (`FieldDressing`)

생성기의 `GenerationCompleted`에서 3m 타일 필드를 꾸밉니다. 이전 10m 비교 모드(L)는 꾸미지 않습니다. 같은 seed에서는 같은 배치가 나옵니다.

- **지면 재질**: 텍스처 없는 무광 잔디색(URP Lit)으로 교체하고, 높은 층일수록 8%씩 밝게 칠해 높이 차가 읽히게 합니다. 원본 Grass 텍스처는 삼각 무늬가 3m 타일마다 반복되어 제외했습니다. 2단계에서 Terrain Layer로 대체할 임시 지면입니다.
- **군락과 빈터**: seed별 저주파 노이즈로 타일마다 식생 밀도를 정합니다. 기준값(0.48) 미만인 타일은 빈터로 비워 상자·전투 공간을 남기고, 군락 타일에만 식생과 장애물을 둡니다.
- **지면 덮개**(충돌 없음): 군락 타일에 1~3개를 놓습니다. 군락마다 풀밭·꽃밭·고사리·덤불 중 하나의 테마를 이어서 써서 같은 소품이 맵 전체에 고르게 반복되지 않게 합니다.
- **큰 덤불 제외**: 폭 1m를 넘는 덤불은 통과하면 어색하고 막아도 의미 있는 공간이 되지 않아 배치하지 않습니다(사람 검토 결정).
- **절벽 바위**(충돌 없음): 내부 높이 차 경계의 40%에만 `Rock_Cliff`를 원래 비율 그대로 폭 3.4m로 키워 절벽 면에 묻습니다. 윗면 위로 0.1m, 낮은 쪽으로 두께의 15%만 드러납니다. 축소한 작은 바위와 맵 외곽 바위는 쓰지 않습니다.
- **외곽 돌벽**(이동을 막음): 맵 바깥에 접한 변마다 `StoneWall`을 3m 길이로 맞춰 타일 바깥에 세웁니다. 맵 밖 이탈을 막으며 경로 복구 대상이 아닙니다. 기존 통합 씬의 "외곽 벽 없음" 규칙은 바꾸지 않았고 TerrainTest에만 적용됩니다.
- **울타리**(이동을 막음): 군락 타일에서 같은 높이 평지끼리 맞닿은 변을 따라 2~3칸 길이로 드물게 세웁니다. 한 칸짜리 조각은 놓지 않습니다.
- **나무·엄폐물**(이동을 막음): 사분면 안쪽(중심에서 ±0.75m)에 놓습니다. 해당 모서리 쪽 이웃 세 칸이 같은 높이 평지일 때만 배치합니다. 나무는 0.4m 기둥, 엄폐물은 외형 크기의 `BoxCollider`만 가지며 0.9m보다 큰 엄폐물은 제외합니다. 원본 프리팹의 충돌체는 모두 제거합니다.

꾸밈은 `PropScatter` 오브젝트 하위의 `FieldDressing` 루트에 생성됩니다. `PropScatter`의 자체 소품 정리와 분리되지만, 같은 오브젝트 하위의 `BoxCollider`가 기존 도달성 검사와 `FieldNavigation` NavMesh에 장애물로 포함됩니다. 배치 직후 자체 경로 검사로 막힌 지점 주변의 나무·엄폐물을 제거합니다(막힌 지점 반경 2.5m 안의 물체, 없으면 가장 가까운 하나를 최대 24회 제거한 뒤에도 막히면 제거 가능한 물체 전체 제거).

구매 프리팹 목록은 비공개 Assets 저장소 `Nomad/Terrain/Dressing/Resources/Nomad/FieldDressingSet.asset`에 있습니다. 이 에셋이 없는 환경에서는 꾸밈 없이 기존과 같이 동작합니다. 목록과 밀도는 `Rebuild Kingdom Dressing Set` 메뉴로 다시 만들거나 에셋 Inspector에서 조정합니다.

## 검증

```powershell
unity run '<checkout>/UnityProject' --timeout 1500 --log-file '<checkout>/UnityProject/Artifacts/terrain-test.log' --no-tail -- -executeMethod Nomad.Terrain.Editor.FieldDressingAuthoring.CreateAndVerify
```

세트와 씬을 다시 만들고, 고정 seed 6개(51, 7, 1234, 2024, 99, 4242)에서 꾸밈 없는 기준 결과와 꾸민 결과를 차례로 만듭니다. Ready, 경로, 꾸밈 배치, 충돌체 종류, 외곽 돌벽을 확인하고 적·상자 후보 수를 기준과 비교해 기록합니다. 시작점이 꾸밈 충돌체와 겹치지 않는지도 확인합니다. 성공 로그는 `FIELD_DRESSING_PASS`이고, seed별 결과는 `FIELD_DRESSING_SEED`입니다. 렌더는 `Artifacts/terrain-test-overview.png`, `terrain-test-player.png`, `terrain-test-border.png`, `terrain-test-cliff.png`입니다. 구매 애셋이 포함된 이미지이므로 공개 저장소에 커밋하지 않습니다.

## 사람 판단

외형 수용, 밀도, 조명 수치, 카메라 각도와 2단계(고저차 표현 교체) 진행 여부는 사람이 검토합니다. 맵 바깥 배경(숲·지면)은 맵 밖 이동 규칙과 관련되어 이번 범위에서 제외했습니다.

## 알려진 영향

- 장애물이 빈터 경계를 쪼개면 `FieldNavigation`의 상자 후보(주변 2m 개방 조건)가 줄 수 있습니다. 고정 seed 6개 기준 32개 → 26개였습니다(seed 99: 7 → 1). 사람 검토에서 이 수준을 수용했습니다. 적 후보 64개와 경로 표본 2916개는 변하지 않았습니다.
- O키 전체 보기에서는 그림자 거리 밖이라 지면이 평평하게 보입니다.
- 외곽 돌벽은 각 타일 윗면 높이에 세우므로 높이가 다른 외곽 타일 사이에는 벽 높이 단차가 있습니다.

## 2단계: Terrain 지면·경사로·경로

TerrainTest의 `FieldDressing`(기본 `terrainSurface` 켬)은 꾸밈보다 먼저 생성 결과를 하나의 Unity Terrain으로 바꿉니다. 꾸밈 목록(비공개)이 없어도 동작합니다.

- **높이**: WFC 높이 골격(0/1/2m)을 그대로 씁니다. 평지는 타일 높이, 계단 칸은 `ContinuousHeight`의 연속 경사(3m에 1m, 약 18°)라 걸어서 오르내리는 경사로가 됩니다. 높이 차 경계는 샘플 하나(0.25m 이하) 폭에서 1~2m가 바뀌어 76° 이상이므로 NavMesh(최대 45°)와 캐릭터 모두 절벽으로 취급합니다.
- **충돌·경로**: Terrain에 `TerrainCollider`를 두고, 타일의 외형과 `BoxCollider`는 끕니다. 타일 오브젝트는 생성 결과·연결 정보로 남습니다.
- **칠**: 잔디 / 경사로와 저주파 노이즈 흙 패치 / 50° 이상 급경사 암벽색. 레이어 텍스처는 코드로 만든 단색(±2.5% 얼룩)이며 구매 애셋을 쓰지 않습니다.
- **공용 코드(동작 범위 확장, 시그니처 변경 없음)**: `MicroTerrainWalkability.Build`는 생성 루트 아래 Terrain을 NavMesh 소스로 넣고 꺼진 BoxCollider를 뺍니다. 바닥 판정(`FieldNavigation`·`PropScatter`·`FieldPreview`)은 새 `FieldGround.BelongsTo`로 "조각 자신 또는 그 칸을 덮는 필드 Terrain"을 바닥으로 인정합니다. Terrain이 없는 씬(`TerrainIntegration.unity` 포함)은 기존과 같습니다.
- 생성기 내부의 1차 도달성 검사는 Terrain을 만들기 전 타일 기준으로 실행되고, 이후 꾸밈 복구·`FieldNavigation` 준비는 Terrain 기준으로 다시 검사합니다.

검증은 위 `CreateAndVerify`가 Terrain 존재와 켜진 타일 BoxCollider가 없음을 함께 확인하고 `Artifacts/terrain-test-ramp.png`를 남깁니다. 기존 `TerrainIntegrationValidation.Run` 회귀로 Terrain이 없는 경로가 그대로임을 확인합니다.

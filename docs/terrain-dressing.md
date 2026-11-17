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

- **지면 재질**: 타일 외형 재질을 Kingdom `Grass_01`로 교체합니다.
- **지면 덮개**(충돌 없음): 계단이 아닌 타일마다 풀·고사리·꽃·작은 덤불을 3개 놓습니다.
- **절벽 바위**(충돌 없음): 낮은 이웃이나 맵 바깥에 접한 변에 `Rock_Cliff`를 높이 차에 맞춰 세워 블록 단면을 가립니다. 충돌은 기존 타일이 담당하므로, 바위 아랫부분이 낮은 타일 쪽으로 조금 겹쳐 보일 수 있습니다.
- **나무·엄폐물**(이동을 막음): 타일 모서리 쪽(중심에서 ±0.9m)에 확률적으로 놓습니다. 나무는 0.5m 기둥, 엄폐물은 외형 크기의 `BoxCollider`만 가집니다. 원본 프리팹의 충돌체는 모두 제거합니다.

꾸밈은 `PropScatter` 오브젝트 하위의 `FieldDressing` 루트에 생성됩니다. `PropScatter`의 자체 소품 정리와 분리되지만, 같은 오브젝트 하위의 `BoxCollider`가 기존 도달성 검사와 `FieldNavigation` NavMesh에 장애물로 포함됩니다. 배치 직후 자체 경로 검사로 막힌 지점 주변의 나무·엄폐물을 제거합니다(최대 8회 후, 여전히 막히면 막는 물체 전체 제거).

구매 프리팹 목록은 비공개 Assets 저장소 `Nomad/Terrain/Dressing/Resources/Nomad/FieldDressingSet.asset`에 있습니다. 이 에셋이 없는 환경에서는 꾸밈 없이 기존과 같이 동작합니다. 목록과 밀도는 `Rebuild Kingdom Dressing Set` 메뉴로 다시 만들거나 에셋 Inspector에서 조정합니다.

## 검증

```powershell
unity run '<checkout>/UnityProject' --timeout 1500 --log-file '<checkout>/UnityProject/Artifacts/terrain-test.log' --no-tail -- -executeMethod Nomad.Terrain.Editor.FieldDressingAuthoring.CreateAndVerify
```

세트와 씬을 다시 만들고, 기본 seed와 무작위 seed 5개에서 Ready·경로·꾸밈 배치·충돌체 종류를 확인합니다. 성공 로그는 `FIELD_DRESSING_PASS`이고, seed별 결과는 `FIELD_DRESSING_SEED`입니다. 렌더는 `Artifacts/terrain-test-overview.png`, `terrain-test-player.png`입니다. 구매 애셋이 포함된 이미지이므로 공개 저장소에 커밋하지 않습니다.

## 사람 판단

외형 수용, 밀도, 조명 수치, 카메라 각도와 2단계(고저차 표현 교체) 진행 여부는 사람이 검토합니다. 맵 바깥 배경(숲·지면)은 맵 밖 이동 규칙과 관련되어 이번 범위에서 제외했습니다.

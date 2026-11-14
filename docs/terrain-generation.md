# 지형 조각 랜덤 연결

관련 작업: [#14](https://github.com/jj-game-studio/Project-Nomad/issues/14).
기준: [공통 규격](common-spec.md), [최소 지형 조각](terrain-pieces.md).

## 실행

Unity 6000.6.9f1에서 비공개 ThirdParty 서브모듈을 초기화한 프로젝트를 엽니다.
`Nomad > Terrain > Open Random Generation Preview`를 선택하면 기존 씬의 미저장
변경을 확인한 뒤 새 미저장 씬을 엽니다. 구매 애셋을 포함하는 씬을 공개 저장소에
저장하지 않습니다. 기존 B 씬과 프리팹은 덮어쓰지 않습니다.

`B_RandomTerrain`의 Inspector에서 prefabs, pieceCount, seed, generationBounds를
설정하고 Play하면 재생성합니다. 기본값은 기존 6종과 StairsCorner/StairsTee/StairsCross,
총 9종·12조각·자동 seed,
월드 원점 중심 400×100×400m입니다. 수량·영역은 테스트용 초기값입니다.
생성기는 배율 1, Y축 90도 단위 회전을 사용하며 위치는 첫 조각의 루트 원점입니다.
generationBounds는 월드 좌표의 AABB이며 첫 조각을 포함해 모든 점유 영역을 제한합니다.

Corner는 남·동, Tee는 남·동·북, Cross는 사방 연결점을 가집니다. 기존 Flat의
Visual·Collision·점유 영역을 재사용한 실제 프리팹이며 비공개 애셋 저장소에 있습니다.
기존 세 프리팹과 B 씬은 유지합니다. 원본 아트와 통행 수치도 변경하지 않습니다.

StairsCorner(ㄱ), StairsTee(T), StairsCross(+)는 중앙 4×4m의 높이 2m 참과
2/3/4개 방향의 계단을 가집니다. 바깥 연결점은 높이 0m, 계단 팔은 폭 4m·길이 3m이며
기존 계단 메시와 단 충돌체를 재사용합니다. 통로 폭 2m·최대 단차 0.25m 규격을 유지합니다.
평지 포트만 추가한 조각이 아니라 실제 중앙 참까지 오르내릴 수 있는 계단입니다.

기본값 requirePlanarBranch를 켜면 4조각 이상인 결과는 루트 위치가 X·Z 양축으로
각각 10m 이상 펼쳐지고, 하나의 조각에 실제 연결 3개 이상이 있어야 성공합니다.
단순히 직선을 회전하거나 사용하지 않은 포트만 많은 조각을 놓는 것으로 충족하지 않습니다.
조건 미달이면 기존 재생성 한도 내에서 다시 시도하고, 끝내 만족하지 못하면 실패합니다.
1~3조각은 분기를 만들 수 없어 이 조건을 적용하지 않습니다. 직선 테스트가 필요할 때만
Inspector에서 requirePlanarBranch를 끌 수 있습니다.

평지 분기와 계단 분기는 초기 연결 형태를 제공합니다. 연결하지 않은 변을 막는 벽이나
내부 장애물은 아직 없으므로 논리 연결 목록 외에 서로 맞닿은 바닥도 이동할 수 있습니다.
소품·내부 장애물 배치는 #16 범위로 유지합니다.

## 생성과 실패 처리

- `TerrainGenerator.Configure(...)`로 설정하고 `Generate()`로 동기 생성합니다.
  자동 실행은 generateOnStart를 켭니다. 생성 콜백 안에서 재진입하거나 설정을 바꿀 수 없습니다.
- seed별 전용 System.Random을 사용하므로 UnityEngine.Random의 전역 상태를 바꾸지 않습니다.
  기본 randomizeSeed를 켜면 Generate/Play마다 새 seed를 정합니다. 사용 값은 실행 중
  Inspector의 seed와 LastResult.Seed에서 확인합니다. 바로 이전 seed와 같은 값은 피하지만
  서로 다른 seed가 언제나 다른 맵 형태를 만든다는 보장은 아닙니다.
  재현하려면 randomizeSeed를 끄고 사용했던 seed를 입력합니다. 같은 프리팹 순서·설정·원점·
  seed·실행 환경에서 같은 결과를 얻습니다. Configure의 기존 4인자 호출은 고정 모드이며,
  자동 모드는 마지막 useRandomSeed 인자에 true를 전달합니다.
- 사용하지 않은 연결점을 골라 새 조각의 연결점을 반대 방향으로 회전하고 바닥 높이와
  위치를 맞춥니다. 기존 `Matches`로 유형·폭·높이·위치·방향을 재검사합니다.
- 모든 기존 조각과 WorldOccupancy를 비교합니다. 경계 접촉은 허용하지만 내부 겹침은
  거절합니다. 예외는 실제 연결된 두 연결점의 LocalSeamBounds 양쪽에 겹침 전체가
  포함될 때만 적용합니다. 위치 오차 1cm를 일반적인 관통 허용량으로 사용하지 않습니다.
- 추가할 조각당 후보 검사 최대 50회입니다. 실패하면 필드 전체를 정리하고 최대 3회
  재생성합니다. 최초 1회와 재생성 3회를 합쳐 총 4회이며 하나의 난수열을 계속 사용합니다.
  공간이 없거나 연결점이 소진돼도 이 한도로 종료합니다.
  수량을 채웠어도 2D 분기 조건을 충족하지 못하면 해당 생성 시도는 실패입니다.
- 생성 중 루트는 비활성입니다. 성공한 루트만 활성화합니다. 실패하면 부분 필드를 정리하고
  빈 조각/연결 목록과 실패 사유를 반환합니다. 런타임 Destroy는 지연되므로 즉시 비활성화한 뒤
  삭제합니다. 재생성 또는 생성기 파괴 시 자신이 만든 지형만 정리합니다.

## 후속 시스템에 전달하는 지형 단계 계약

`State`: Idle / Generating / Complete / Failed.
`LastResult`: 시작 시 null로 무효화하고 완료 뒤 결과를 유지합니다.
`GenerationStarted(generationId)`: 이전 결과를 무효화하고 지형을 정리한 뒤 호출합니다.
`GenerationCompleted(TerrainGenerationResult)`: 성공 또는 실패 시 한 번 호출합니다.
이벤트 수신자는 콜백에서 예외를 던지거나 생성기를 파괴하지 않아야 합니다.

결과는 GenerationId, Seed, Success, FailureReason, Attempts, CandidateChecks와 읽기 전용
Pieces/Links 목록을 제공합니다. Links는 연결된 From/To 연결점을 담습니다. 후속 시스템은
최신 결과의 조각과 연결점에서 소품·경로 처리를 시작할 수 있습니다. 이전 결과의 목록은
변경하지 않지만 내부 Unity 오브젝트는 재생성 시 파괴되므로 세대 ID와 최신 결과를 확인해야 합니다.

**Complete는 지형 연결만 완료됐다는 뜻입니다.** 이 결과는 공통 규격의 최종
GenerationResult 또는 Ready가 아닙니다. #16 소품 처리와 #18 NavMesh·도달성·스폰 지점
검증 후 별도 조정 단계가 최종 Ready를 제공해야 합니다. 적·상자를 이 결과만으로 생성하지 않습니다.
플레이어 입력 잠금, 이전 적·발사체·상자 정리도 전체 생성 흐름을 연결할 때 담당 영역에서 처리합니다.

## 검증

`Nomad > Terrain > Validate Random Generation`은 열린 사용자 씬을 보존하고 임시 씬에서
100개 seed를 각각 두 번 생성합니다. 매번 실제 아홉 프리팹의 X/Z 확장과 실제 분기를
확인하고, 기존 지형의 높이/회전 연결, 같은 seed 재현성,
모든 쌍의 내부 겹침, 이벤트/세대 갱신, 공간 부족 시 200회 후보 검사·총 4회 종료,
잘못된 설정, 실패 후 복구, 합성 조각의 직교 회전·분기, 좁은 연결부 예외를 확인합니다.
자동 seed 갱신과 해당 seed의 고정 모드 재현도 확인합니다.

`Create Stair Junction Pieces`는 새 계단 세 프리팹만 제작·덮어씁니다. 직접 편집했다면
재제작 대신 `Validate Stair Junction Pieces`를 사용하십시오. 이 검사는 9개 팔의
바닥/단차/머리 위 여유, 중앙 참 외형·충돌 일치 및 CharacterController 왕복을 확인합니다.
`StairJunctionAuthoring.CreateAndValidate`는 제작·계단 검사·전체 생성 검사와
Artifacts/stair-junctions.png 및 terrain-planar-preview.png 렌더링을 실행합니다.

`Create Planar Connection Pieces`는 비공개 Terrain 폴더의 Corner/Tee/Cross만
제작·덮어쓰는 도구입니다. 원본 Flat/Stairs/Raised와 B 씬은 덮어쓰지 않습니다.
기존 신규 프리팹을 수작업 편집했다면 재제작하지 말고 Validate를 사용하십시오.
`PlanarTerrainAuthoring.CreateAndValidate` batch 진입점은 새 세 프리팹 제작·전체 검증 후
Artifacts/terrain-planar-preview.png를 렌더링합니다. 구매 애셋을 포함한 이미지는 공개 저장소에 커밋하지 않습니다.

프로젝트를 닫고 batch로 재현할 수 있습니다.

```powershell
unity run '<checkout>/UnityProject' --timeout 600 --log-file '<checkout>/UnityProject/Artifacts/generation-edit.log' --no-tail --format json -- -executeMethod Nomad.Terrain.Editor.TerrainGenerationValidation.Validate
```

성공 로그: `TERRAIN_GENERATION_PASS`.
실제 Play 모드의 지연 삭제·재생성 검증은 자동 종료 진입점을 사용합니다.
이 진입점은 프레임 진행을 확인하고 성공/실패 또는 120초 제한에서 종료합니다.
비동기 Play 검증에는 자동으로 -quit을 붙이는 unity run 대신 Editor를 직접 실행합니다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.6.9f1/Editor/Unity.exe' -batchmode -projectPath '<checkout>/UnityProject' -executeMethod Nomad.Terrain.Editor.TerrainGenerationValidation.VerifyPlayMode -logFile '<checkout>/UnityProject/Artifacts/generation-play.log'
```

성공 로그: `TERRAIN_GENERATION_PLAYMODE_PASS`와 실제 frame/time.
Artifacts는 실행 산출물이며 커밋하지 않습니다. 사람의 경로·높낮이 배치 수용 여부와
A 플레이어/적의 통합 검토는 자동 검증 결과와 구분합니다.

## 계단 분기·자동 seed 추가 검증

- 실제 계단 3종 총 9개 팔의 2m 참, 폭 2m 구간 바닥/단차/여유 및 캡슐 왕복 통과.
  렌더링에서 발견한 중앙 참 피벗 오프셋을 보정하고 외형·충돌 일치 검사도 통과했습니다.
- 실제 9종 프리팹 100 seed × 2회 생성 회귀 검사, 자동 seed 변경과 고정 재현 통과.
- 실제 Play의 Start 자동 생성과 재생성/지연 정리 통과(frame=7, time=0.12, 종료 코드 0).
  기존 SearchDatabase 시작 인덱싱 예외는 1건 발생했으며 생성 코드 스택은 없습니다.
- 사용자 에디터·미추적 Editor/Tests·UpgradeLog.htm·원본 머티리얼 변경을 보존하고
  별도 검증 사본에서 실행했습니다. 구매 애셋을 포함한 렌더링은 공개 저장소에 올리지 않습니다.

## 이전 2D 보완 검증 결과

- 실제 여섯 프리팹으로 100 seed × 2회 생성해 매번 양축 확장과 실제 3방향 분기를
  확인했습니다. 꺾이기만 하고 분기하지 않는 결과의 거절, 재현성, 높낮이/회전 연결,
  내부 겹침/연결부 예외, 공간 부족·재생성 종료·결과 이벤트 검사도 통과했습니다.
- Play 모드 종료 코드 0, frame=5 / time=0.11에서 실제 생성·재생성 및 지연 정리를
  확인했습니다. 기존 Unity SearchDatabase 시작 인덱싱 예외 1건은 다시 발생했습니다.
- 열린 사용자 Unity를 종료하지 않고 `.worktrees/issue14-validation`의 별도 사본에서
  검증했습니다. 실행한 소스 및 새 프리팹과 제출 파일의 일치를 확인합니다.
- `Artifacts/terrain-planar-preview.png`에서 기본 seed의 양축 배치와 정상 렌더링을 확인했습니다.
- 신규 프리팹은 비공개 [애셋 PR #2](https://github.com/jj-game-studio/Project-Nomad-Assets/pull/2),
  공개 코드/참조는 [PR #29](https://github.com/jj-game-studio/Project-Nomad/pull/29)에서 함께 검토합니다.
  비공개 PR을 먼저 병합하고 공개 서브모듈 커밋이 보존되는 순서를 유지합니다.
- Unity 생성 YAML에는 빈 필드 뒤 공백 경고가 있으며 생성 형식을 유지했습니다.
  기존 원본 머티리얼의 별도 로컬 변경은 보존하고 이번 커밋에 포함하지 않습니다.
- A 플레이어/적의 통합 플레이, Windows 플레이어 빌드와 사람의 배치 수용 검토는 별도입니다.

## 이전 제출 시 확인한 결과

- Unity 6000.6.9f1 / Windows에서 편집 모드 검증 종료 코드 0 및
  `TERRAIN_GENERATION_PASS`를 확인했습니다.
- Play 모드 검증 종료 코드 0, `TERRAIN_GENERATION_PLAYMODE_PASS`의
  frame=5, time=0.08과 지연 삭제 완료를 확인했습니다.
- Play 진입 시 UnityEditor.Search.SearchDatabase의 시작 인덱싱에서
  ArgumentOutOfRangeException 1건이 발생했습니다. 지형 코드 스택은 없고
  생성/정리 검증은 완료됐습니다. 에디터 예외가 없는 실행이라고 보고하지 않습니다.
- 최초 임포트의 기존 ModelImporter 버전 경고와 라이선스 핸드셰이크 재시도 로그가
  있습니다. 구매 애셋 원본과 공용 설정은 수정하지 않았습니다.
- A 실제 플레이어/적 통합, 시각적 경로 수용 검토와 Windows 플레이어 빌드는
  이번 자동 검증에서 수행하지 않았습니다.

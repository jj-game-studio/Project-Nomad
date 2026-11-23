# 생성 필드 경로와 생성 지점 (#18)

## 소비 순서

`TerrainGenerator.GenerationCompleted`와 `TerrainGenerationState.Complete`는 지형 단계 결과입니다. 적·상자 배치의 시작 신호로 사용하지 않습니다.

`FieldNavigation.Configure(generator)`를 먼저 호출합니다. 지형 생성과 소품 배치/경로 복구를 끝낸 호출 측에서 `Prepare(generator.LastResult, obstaclesRoot)`를 호출합니다. 통합 씬의 `TerrainIntegrationControls.Regenerate`가 이 순서를 수행합니다. 별도의 자동 이벤트 순서에 최종 준비를 맡기지 않습니다.

소비자는 `FieldNavigation.GenerationStarted`에서 이전 개체를 정리하고 `GenerationCompleted`의 성공 결과에서만 배치합니다. 늦게 연결한 소비자는 `State`와 `LastResult`를 조회합니다. `GenerationId`를 기록하여 같은 결과의 중복 배치를 막습니다. 완료 콜백에서 동기적으로 재생성하지 말고 다음 프레임에 요청합니다.

## 결과와 수명

- 상태: `Idle / Generating / Ready / Failed`. `Ready`에서만 `HasNavigation`과 사용 가능한 지점이 있습니다.
- 결과: `GenerationId`, `Seed`, `Success`, `FailureReason`, `PlayerPosition`, `PlayerRotation`, `EnemyPoints`, `ChestPoints`, `ExitPoints`.
- 지점: 세대 내 고유 `Id`, 월드 `Position`·`Rotation`, 점유 `Radius`, 도달 가능한 `ApproachPosition`. 지점 ID는 세대 ID와 함께 사용합니다. 결과 목록은 읽기 전용 스냅샷입니다.
- 지형 재생성 시작 시 이전 결과와 NavMesh를 즉시 무효화합니다. 같은 최신 결과의 반복 `Prepare`는 같은 결과를 반환하고 통지를 반복하지 않습니다. 실패 시 지점은 비우고 NavMesh를 제거합니다. 컴포넌트 비활성화/파괴 시에도 제거합니다.
- 통합 씬은 재생성 전에 플레이어를 비활성화하고 최종 성공 후 안전 시작점에 배치합니다. 다른 호출 측도 입력 잠금과 기존 개체 정리를 담당합니다.

## 공간 검증

기존 검사와 최종 경로가 같은 BoxCollider 기반 NavMesh 빌더를 사용합니다. 에이전트는 반지름0.35m/높이1.8m/단차0.3m/경사45도이며 voxel0.075m입니다. 소품은 보행 불가 영역입니다. NavMesh는 검증 뒤에도 등록 상태로 유지합니다. 패키지·프로젝트 설정은 추가하지 않습니다.

현재3m 지형은 모든 타일의 대표 표면과 모든 필수 연결부에서 시작점으로부터 완전한 경로를 검사합니다. 시작점과 생성 지점은 평평한 바닥 지지, 네 모서리 높이, 캡슐 여유를 확인합니다. 계단 위에는 생성 지점을 제공하지 않습니다. 적 지점은0.5m 여유와 시작점5m 제외, 상자 지점은0.75m 점유와2m 이내 접근점을 사용합니다. 지점 간 점유 영역도 겹치지 않습니다.

상자 후보는 보행 경계에서2m 이상 떨어진 곳을 선택합니다. 후보를 모두 장애물로 반영한 NavMesh를 다시 굽고 주변 표면·필수 연결부·접근점의 도달성을 재검사합니다. 실패하면 문제 지점에 가까운 후보를 하나씩 제외하고 최대8번 재검사합니다. 모두 제외되면 상자 예약 없는 경로로 복구합니다. 상자를 배치하지 않더라도 상자 예약 공간은 보행 불가이며, 소비자는 제공된 크기와 위치를 지켜야 합니다. 후보는 조각당5개, 반환 상한은 적96개/상자8개입니다. 적 후보는 seed로 섞은 타일 순서로 골라 필드 전체에 퍼집니다(#154, 이전에는 타일 순서대로 골라 첫 줄 근처에 몰렸습니다). 이는 후보 작업량 제한이며 실제 생성 수량 정책이 아닙니다. 공간 부족은 빈 목록으로 정상 종료합니다.

3m 지형의 중앙 상자 주변 통행 기준은 기존 통합 씬과 같은 실제 캡슐 보행 검사입니다. 구형10m 조각은 연결부 폭을 추가로 예약합니다. 구형 비교 모드의 모든 표면 검증을 신규3m 검증과 동일하다고 보고하지 않습니다.

## 출구 (#90)

`ExitPoints`는 필드 가장자리의 임시 출구입니다. 4면 모두에서 계단이 아닌 가장자리 타일을 지형 seed로 섞어 면마다 1개를 고릅니다(#154). 가장자리 줄에 유효한 자리가 없으면 한 줄 안쪽 타일까지 대체 후보로 봅니다. 플레이어 시작점(`PlayerPosition`)은 seed로 고른 출구 하나에서 필드 안쪽으로 3m(막혔으면 3.75/2.5/4.5m, 그래도 없으면 다음 출구) 들어온 자리이고, `PlayerRotation`은 필드 안쪽을 봅니다. 시작점이 출구 트리거(반경 0.75m) 밖이라 시작하자마자 출구가 발동하지 않습니다. 출구 선택 전의 경로·상자 검사는 첫 타일의 임시 시작점으로 하며, 필드 전체가 연결되어 있으므로 결과는 실제 시작점에도 그대로 유효합니다. 출구는 다른 출구에서12m 이상 떨어져 있고, 상자 예약과 겹치지 않으며 시작점에서 NavMesh 경로로 도달 가능합니다. 적 지점은 출구 자리를 피합니다. 지점 ID는 `exit-west/east/south/north`, `Rotation`의 forward는 필드 바깥 방향입니다. 대체 후보까지 없는 면은 건너뛰며 필드를 실패시키지 않습니다. 검증은 seed마다 출구 4개와, 시작점 5m 안의 출구가 정확히 하나인지 확인합니다.

통합 씬은 출구마다 초록 원판·기둥 표식과 `FieldExit` 트리거를 만들고, 플레이어가 들어가면 HUD에 `Exit reached`를 표시합니다. 실제 마을 이동·출구별 목적지 규칙은 소비 측(#89)에서 `FieldExit.Entered` 또는 `ExitPoints`로 연결합니다.

## 통합 씬과 검증

`Nomad > Terrain > Open Integration Test Scene` → Play. HUD에서 Ready/Failed, Enemy slots, Chest slots, Exits를 확인합니다. R은 새 seed, T는 동일 seed 재현, P는 기존 소품 토글입니다. L은 구형 비교입니다. 적과 신규 상자를 실제 생성하는 정책은 #39/#20에서 연결하며, 기존 #16의 루팅 상자는 그대로 유지합니다.

`Nomad > Terrain > Validate Field Navigation` 또는 Unity 배치 실행의 `Nomad.Terrain.Editor.FieldNavigationValidation.Run`으로 같은 저장 씬을 검사합니다. 실행 시 연결 컴포넌트가 없으면 이를 추가하고 런타임 지형 생성 전에 씬을 저장합니다. 검사 결과는 `Artifacts/field-navigation-results.txt`, 화면은 `field-navigation-overview.png`와 `field-navigation-player.png`입니다.

자동 검사는 실제 몬스터의 추적·전투나 사람의 이동감 판단을 대신하지 않습니다. 해당 통합 검증은 #40과 사람 플레이 검토에서 수행합니다.

`FieldNavigationPlayVerification.Run`은 같은 저장 씬의 Play 모드에서 실제 NavMeshAgent가 목적지까지 이동한 뒤 재생성된 NavMesh와 플레이어 활성 상태를 검사합니다. 이 진입점은 완료 후 Editor를 종료하는 자동 검증용입니다. `unity open <프로젝트 절대 경로> --batchmode --args '-executeMethod Nomad.Terrain.Editor.FieldNavigationPlayVerification.Run -logFile <로그 절대 경로>'`로 실행할 수 있습니다.

## 검증 결과 (2026-11-17)

- Unity6000.6.9f1, 저장 통합 씬의 seed0/1/7/18/51을 각각2회 생성했습니다. 매번 적 후보64개, 상자 후보는 각각6/3/8/8/0개로 재현되었습니다. 모든 표면/연결부 도달성, 세대별 단일 이벤트, 후보 비중첩·접근점, 생성 실패/시작점 차단과 NavMesh 정리/복구 검사가 통과했습니다.
- 실제 Play에서 에이전트가68프레임 동안 (6,0.01,0)에서 (8.95,1.06,11.81)로 이동해 목적지에 도달했습니다. 재생성, 비활성/재활성, 이전 세대 중복 공개 거부와 새 경로 유지까지 검사했습니다. 성공 표식은 frame93/time1.51입니다. 이는 실행 프레임 검사이며 성능 벤치마크가 아닙니다.
- Play 시작에 기존 `UnityEditor.Search.SearchDatabase` 인덱싱의 `ArgumentOutOfRangeException`이 기록되었습니다. 검증은 성공 표식까지 진행했지만 로그 전체가 무오류인 것으로 보고하지 않습니다.
- Windows 빌드, 실제 몬스터/신규 상자 배치, 구형 비교 모드 전체 회귀와 사람의 이동감 검토는 이번 검사에 포함하지 않았습니다. seed51은 보수적인 상자 후보 검사 결과가0이며 다른 소비자는 빈 목록을 정상 처리해야 합니다. 몬스터/상자 수량이나 난이도를 확정한 결과가 아닙니다.

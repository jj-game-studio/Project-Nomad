# 고정 경로 몬스터 탐지·추적

- 작업: [#37](https://github.com/jj-game-studio/Project-Nomad/issues/37), 상위 #17.
- 범위: 고정 테스트 경로의 탐지·추적. 공격·피해·사망은 #38, 생성 필드 연결은 #39/#40입니다.
- 컨셉 목록은 #17의 컨셉 기록을 참고합니다. 이 테스트는 맵·몬스터 전체 제작이나 밸런스 확정을 뜻하지 않습니다.

## 실행

Unity 6000.6.9f1에서 `Nomad > Monsters > Create Tracking Preview`를 실행하고 Play합니다.
이 메뉴는 몬스터 전용 `Assets/Nomad/Monsters/MonsterTracking.unity`를 생성하거나 다시 만듭니다.
기존 장면의 미저장 변경은 저장 여부를 확인하며, A/B 소유 장면은 덮어쓰지 않습니다.

WASD 이동, Space 점프, 왼쪽 Shift 구르기는 기존 PlayerMovement를 사용합니다.
앞쪽 계단을 통해 높은 평지로 이동하면 몬스터가 따라옵니다. 왼쪽 벽은 시야 차단, 오른쪽 떨어진 바닥은 도달 불가능한 경로 확인용입니다.

## 연결과 흐름

전용 씬 초기화 → 테스트 지형만 NavMesh 생성 → Inspector의 플레이어 Transform 연결.

매 프레임 → 목표 유효성 확인 → 시야각/거리와 장애물 검사 → 마지막 목격 위치 갱신 → 0.2초 간격으로 목적지 변경을 확인해 완전한 경로로 추적. 동일 목적지의 유효 경로는 재사용합니다.

- 플레이어 참조는 Inspector 또는 `BindTarget(Transform)`으로 명시합니다. 전역 검색이나 플레이어 코드 변경은 필요하지 않습니다.
- 목표가 가려지면 실시간 위치를 쫓지 않고 마지막으로 본 위치를 사용합니다. 마지막 목격 후 2초가 지나면 멈춥니다.
- 목표 비활성/소멸, 12m 초과, 에이전트가 NavMesh를 잃은 경우 추적을 종료합니다.
- 완전한 경로가 없으면 이동을 중단하고 `PathBlocked`로 표시합니다. 목표가 보이는 동안 0.2초 간격으로 경로 회복을 확인하며, 한 프레임 안에서 반복 재시도하지 않습니다.
- 테스트 경로는 기본 Unity AI 모듈로 생성합니다. 공유 패키지·설정은 추가하지 않으며 생성 필드의 NavMesh 제공 책임은 #18에 유지합니다.

## 임시 검증 수치

시야 120도/8m, 방향 무관 근접 감지 2m. 두 감지 모두 벽 뒤는 탐지하지 않습니다.
이동 속도 3.5m/s, 정지 거리 1.2m, 추적 종료 거리 12m, 시야 상실 유예 2초입니다.
NavMesh 크기는 공통 규격의 반지름 0.35m, 높이 1.8m, 단차 0.3m, 경사 45도를 사용합니다.
시야·추적 수치는 MonsterTracker, 속도·정지 거리는 NavMeshAgent Inspector에서 조정합니다. 확정 수치가 아니며 사람의 플레이 검토가 필요합니다.

## Unity Test Runner 검증

Unity의 `Window > General > Test Runner`에서 PlayMode의 `Nomad.Monsters.PlayModeTests`를 실행합니다.
각 테스트는 저장된 전용 씬을 추가 로드하고 종료 시 언로드합니다. 테스트 실행으로 씬을 다시 만들거나 저장하지 않습니다.
씬을 재생성하는 `Create Tracking Preview` 메뉴는 수동 미리보기를 새로 만들 때만 사용합니다.

| 테스트 | 확인할 동작 |
| --- | --- |
| DetectsFrontTargetAndMoves | 정면 탐지 후 실제 이동 |
| WallBlocksEvenProximityDetection | 가까운 목표라도 벽 뒤는 탐지하지 않음 |
| RearTargetRequiresProximity | 후방 시야 제외 및 근접 탐지 |
| TraversesStairsUpAndDown | 2m 높은 평지 도착 후 계단 하강 |
| DisconnectedDestinationStopsMovement | 단절 경로에서 이동 중단 |
| InactiveTargetStopsTracking | 비활성 목표의 추적과 경로 정리 |
| DistantTargetStopsTracking | 추적 거리 초과 시 중단 |
| LostSightExpiresWithoutFollowingHiddenTarget | 마지막 목격 위치 유지 및 유예 후 중단 |

## 명령 실행과 결과 확인

PowerShell 7에서 저장소 루트의 아래 명령을 사용합니다. 현재 Unity에서 열어 둔 프로젝트 대신 별도 작업 사본에서 실행합니다.

```powershell
# 전체 8개 검사
./scripts/Test-MonsterTracking.ps1

# 필요한 검사만 다시 실행
./scripts/Test-MonsterTracking.ps1 -TestFilter 'Nomad.Monsters.Tests.MonsterTrackingTests.TraversesStairsUpAndDown'
```

다른 설치 위치는 `-UnityPath`, 다른 작업 사본은 `-ProjectPath`, 실행 제한 시간은 `-TimeoutSeconds`로 지정합니다.
실행기는 `-runTests -testPlatform PlayMode -assemblyNames Nomad.Monsters.PlayModeTests`를 사용하고 `-quit`는 붙이지 않습니다.
프로세스 종료까지 기다린 뒤 통과/실패 수, 실패한 테스트의 메시지, 로그 예외 첫 줄만 JSON으로 출력합니다. 실행 중 반복 로그 조회는 필요하지 않습니다.

실행마다 `UnityProject/Artifacts/tests/monster-tracking/<실행 ID>/`에 `results.xml`과 `unity.log`를 저장합니다. 결과는 추적 제외하며 이전 XML을 재사용하지 않습니다.
테스트 실패·Unity 오류·결과 없음·0개 실행·전체 건너뛰기·타임아웃은 실패 종료 코드로 보고합니다.
실행 시간이 초과되면 이 실행기가 시작한 프로세스와 자식만 종료합니다. 사용 중인 프로젝트의 잠금 파일이 있으면 실행하지 않습니다.
필요한 테스트만 필터로 재실행하고, 최종 변경과 관련된 검사만 추가합니다. 성공한 전체 검사를 이유 없이 반복하지 않습니다.

런타임은 `Runtime/Nomad.Monsters.asmdef`, 테스트는 별도 테스트 어셈블리로 분리했습니다. MonsterTracker 코드와 기존 GUID는 보존했습니다.
기존 lock의 Unity Test Framework 1.8.0을 사용하며 공유 패키지·설정은 변경하지 않았습니다. 테스트 씬 로드에 UnityEditor API를 사용하므로 현재 검증 대상은 Editor의 PlayMode입니다.
실제 생성 필드, 공격/피해 연결, Windows 플레이어 빌드와 재미·난이도 검토는 포함하지 않습니다.

## 검증 이력

2026-11-14 기존 자체 검증에서 같은 경로를 반복 설정할 때 계단 하강 중 멈추던 현상을 수정하고 Play 검사를 통과했습니다.
당시 UnityEditor.Search.SearchDatabase 인덱싱 예외 1건은 별도로 기록했습니다.
이후 Test Runner 전환 결과는 개별 테스트 8개 통과/실패 0개/건너뛰기 0개, 종료 코드 0이며 실행기 요약의 로그 예외도 없었습니다.

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

매 프레임 → 목표 유효성 확인 → 시야각/거리와 장애물 검사 → 마지막 목격 위치 갱신 → 0.2초 간격으로 완전한 경로를 계산해 추적.

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

## 자동 검증

별도 Unity 프로세스에 `-batchmode -nographics -projectPath <UnityProject 절대 경로> -executeMethod Nomad.Monsters.Editor.MonsterTrackingVerification.Run -logFile <로그 절대 경로>`를 전달합니다.
검증기가 Play 모드에 진입하고 종료 코드를 반환하므로 `-quit`는 붙이지 않습니다.

검증 대상은 정면 탐지/이동, 벽 너머 근접 감지 차단, 후방 시야 제외/근접 감지, 2m 계단 왕복, 단절 경로, 목표 비활성, 추적 한계 거리입니다.
성공 로그는 `MONSTER_TRACKING_PLAYMODE_PASS`입니다. 실제 생성 필드, 공격/피해 연결, Windows 플레이어 빌드와 재미·난이도 검토는 이 검사에 포함하지 않습니다.

# 플레이어 외형·애니메이션 프리셋

Issue #134 / PR #135. Synty PolygonFantasyKingdom의 남성 병사 `SM_Chr_Soldier_Male_01`을 사용합니다.

## 실행

Unity 6000.6.9f1에서 `Nomad > Player Visual > Open Preview`를 실행하고 Play합니다. WASD 이동, Space 점프, Shift 구르기입니다. 전용 씬에는 벽·낮은 단차와 정면/측면 비교 모델이 있습니다.

## 프리팹과 생성 도구

- `KingdomSoldierVisual.prefab`: 모델·프로젝트 소유 Avatar·Animator Controller. 외형 높이 1.8m 기준이며 원본 발 루트 높이를 보존합니다.
- `KingdomPlayerRig.prefab`: 기존 `Game/Prefabs/PlayerRig.prefab`의 Variant. 캡슐 외형을 비활성화하고 `Player/Visual`과 `PlayerAnimation`을 추가합니다. 나머지 플레이어 기능을 상속합니다.
- `KingdomPlayerAvatar.asset`: 원본 Avatar의 골반 매핑(Root)을 실제 Hips로 보정합니다.
- `Animations/PlayerLocomotion.controller`: 프로젝트 소유 클립과 재생 속도를 연결합니다.

`Build Preview`는 생성 에셋과 검토 씬을 다시 만듭니다. 수동 변경이 있다면 재생성 전에 보존하세요. `Validate Presets`는 Avatar/Controller·모델·크기·기존 상속·추가 Collider 없음·루트 모션 끄기·재질을 검사합니다.

## 동작 연결

PlayerAnimation은 실제 속도·접지와 PlayerMovement.IsRolling을 읽습니다. 실제 이동·점프 높이·구르기 거리/시간·무적·쿨다운은 변경하지 않습니다. 루트 모션도 적용하지 않습니다. 플레이어 Animator는 AlwaysAnimate로 설정해 카메라 가시성 변화로 포즈 갱신이 생략되지 않게 합니다.

- Idle/Run: ARPG Warrior의 무기 없는 대기와 제자리 달리기. 실제 수평 속도에 따라 달리기 재생 속도를 조절합니다.
- Jump: Mixamo StandingJump의 0.60–0.93초 도약 구간. 입력 즉시 시작하는 물리 점프에 맞춰 선행 쪼그림을 생략합니다.
- Fall: 같은 클립의 0.93–1.27초 공중 구간. 실제 하강이 시작되면 전환합니다.
- Land: 같은 클립의 1.27–2.10초 착지 구간. 실제 접지 시 시작해 0.55초 동안 마무리합니다.
- Roll: Mixamo StandToRoll. 외형 0.8초, 실제 구르기는 기존 3m/0.3초를 유지합니다.

점프를 1.1초 통째로 재생하던 이전 방식은 제거했습니다. 도약/공중 자세는 약 0.35초를 기준으로 재생하고 해당 물리 상태가 끝나면 전환합니다. 착지 마무리와 구르기 외형만 정해진 시간 동안 유지하며 새 점프·구르기는 즉시 우선합니다. 기존 규칙대로 마무리 중에도 이동·공격할 수 있습니다.

원본 ThirdParty는 #136 공유 커밋 7e3cbab을 참조하며 수정하지 않습니다. 필요한 구간만 프로젝트 소유 클립으로 다시 샘플링합니다. 공중 구간의 루트 높이 기준을 맞추고 착지 구간의 몸 낮춤은 자세에 보존합니다.

## 검증과 제한

생성: `unity run <UnityProject 절대 경로> -- -executeMethod Nomad.PlayerVisual.Editor.PlayerVisualAuthoring.Build`

Play 검사: `unity open <UnityProject 절대 경로> --batchmode --args "-executeMethod Nomad.PlayerVisual.Editor.PlayerVisualVerification.Run"`. 검사 완료 후 Editor가 종료됩니다.

검증은 저장 씬의 실제 키 입력, 접지·뼈대 움직임, 도약/하강/착지의 단일 전환, 포즈 연속성, 구르기 외형 유지와 기존 점프·이동·벽 충돌을 확인합니다. 근거는 로컬 Artifacts/player-visual-build.log, player-visual-play.log와 동작별 PNG입니다.

이전 검증에서 Unity Search 시작 색인 예외가 기록된 이력이 있습니다. Windows 빌드·전체 테스트 스위트·사람의 동작 수용은 별도입니다. 공격 애니메이션·무기 손 부착과 정식 Game/마을/필드 연결도 남아 있습니다.

최종 Unity Play 검증: 271프레임, 도약 0.017초→낙하 0.350초→실제 착지 0.683초→대기 1.233초에 각각 한 번 전환했습니다. 점프 구간의 프레임당 골반 위치 변화 최대 0.0456m, 실제 점프 정점 1.200m를 확인했습니다. 이전 설정의 약 0.199m 급변은 이 검증에서 재현되지 않았습니다. 구르기 0.8초 외형과 기존 물리·거리·벽 충돌도 통과했습니다.

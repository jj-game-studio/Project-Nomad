# 플레이어 모델 프리셋

Issue #134. 사용자의 단색 외형 거절 피드백에 따라 의상·갑옷·피부 색상이 있는 모델로 교체했습니다. 기존 Synty PolygonFantasyKingdom의 남성 병사 캐릭터(`SM_Chr_Soldier_Male_01`)를 사용합니다. 원본 ThirdParty는 변경하지 않습니다.

## 사용

- `Nomad > Player Visual > Open Preview`로 `Assets/Nomad/PlayerVisual/PlayerVisualPreview.unity`를 열고 Play합니다.
- WASD 이동, Space 점프, Shift 구르기. 벽과 낮은 단차에서 충돌·접지를 확인합니다.
- 왼쪽에는 정면·측면 외형 비교 모델이 있습니다. 조명과 표현은 검토용입니다.

## 프리팹

- `KingdomSoldierVisual.prefab`: 모델 외형만 담습니다. 높이 1.8m, 바닥 원점, 루트 모션 끄기. 충돌·이동 기능은 없습니다.
- `KingdomPlayerRig.prefab`: 기존 `Game/Prefabs/PlayerRig.prefab`의 Variant입니다. Player의 캡슐 외형만 비활성화하고 `Visual` 자식에 외형 프리팹을 연결합니다. 카메라·이동·체력·장비·인벤토리·HUD 등은 기존 프리팹에서 상속합니다.
- 외형을 수동 교체할 때는 Variant의 Player/Visual을 교체하고 발 위치·높이·정면(+Z)을 확인합니다. 플레이어 루트와 CharacterController의 크기는 외형 조정과 분리합니다.

## 재생성·검증

`Nomad > Player Visual > Build Preview`는 이 폴더의 생성 프리팹과 검토 씬을 다시 만듭니다. 이 에셋을 수동 편집한 경우 재생성 전에 변경을 보존하세요. `Validate Presets`는 상속·모델·발 위치·높이·추가 Collider 없음·루트 모션·재질을 확인합니다.

## 현재 제한

- 대기·이동·점프·낙하·구르기를 연결했습니다. 공격 애니메이션과 무기 손 부착은 별도 후속입니다.
- 기존 Game PlayerRig, GameConfig, TerrainIntegration과 지역 씬은 교체하지 않았습니다. 이 PR의 프리셋 외형 검토 후 실제 게임 연결 범위를 정합니다.
- Windows 배포 빌드와 사람의 외형·크기·조작감 수용 판단은 별도입니다.

## 검증 결과 (2026-11-23)

Unity 6000.6.9f1에서 생성·에셋 검사가 통과했습니다. 저장된 검토 씬의 Play 158프레임에서 실제 WASD/Space/Shift 입력, 접지·이동·회전·점프/착지·구르기·벽 충돌을 확인했습니다. 정면·탑다운 캡처로 모델 표시와 방향을 확인했습니다. 단차는 수동 검토 대상으로 남습니다.

- 생성: `unity run <UnityProject 절대 경로> -- -executeMethod Nomad.PlayerVisual.Editor.PlayerVisualAuthoring.Build`
- Play 검사: `unity open <UnityProject 절대 경로> --batchmode --args "-executeMethod Nomad.PlayerVisual.Editor.PlayerVisualVerification.Run"` (검사 완료 후 Editor 종료)
- 근거: 로컬 `Artifacts/player-visual-build.log`, `player-visual-play.log`, `player-visual-front.png`, `player-visual-top.png`.
- 최초 Play에서 UnityEditor.Search.SearchDatabase 시작 인덱싱의 ArgumentOutOfRangeException이 1회 기록되었습니다. 프리셋 검사는 통과했으나 Editor 로그 전체가 무오류인 것으로 보고하지 않습니다.

## 애니메이션 연결

`PlayerAnimation`은 PlayerMovement의 구르기 여부와 CharacterController의 실제 속도·접지 결과만 읽어 외형 Animator를 전환합니다. 기존 입력·점프 높이·이동 속도·구르기 거리와 충돌은 변경하지 않습니다. 벽에 막혀 실제 수평 속도가 없으면 달리기를 멈추고, 공중에서는 상승/낙하 상태로 바뀝니다.

- Idle: ARPG Warrior `Idle_no_weapon`.
- Run: ARPG Warrior `Run_no_weapon_IPC`. 실제 수평 속도에 따라 재생 속도를 조절합니다.
- Jump: Mixamo `StandingJump`. 외형을 1.1초 동안 재생하며 착지 뒤에도 마무리 자세를 이어갑니다.
- Fall: ARPG Warrior `Airborne`. 낙하 중 재생합니다.
- Roll: Mixamo `StandToRoll`. 외형을 0.8초 동안 재생합니다. 실제 구르기의 이동·무적·공격 제한은 기존 0.3초를 유지합니다.

원본은 #136에서 공유된 ThirdParty 커밋을 참조하며 수정하지 않습니다. 프로젝트 소유 Animations 폴더에 클립 복사본과 PlayerLocomotion Controller를 생성해 반복·루트 이동 설정을 조정합니다. 캐릭터 이동은 기존 코드가 담당하므로 Apply Root Motion은 끄고, 일회 클립의 루트 위치 이동은 적용하지 않습니다.

Build Preview로 Controller·클립·프리셋·씬을 함께 다시 생성할 수 있습니다. 자세와 전환, 특히 실제 행동이 끝난 뒤 이어지는 시각적 마무리는 사람 검토 대상입니다. 정식 Game PlayerRig에는 아직 연결하지 않았습니다.

원본 Avatar의 골반 매핑(Root)을 실제 Hips로 수정한 KingdomPlayerAvatar를 생성합니다. 클립의 원본 기준 높이를 유지하고, 원본 모델의 루트 높이를 보존해 바닥 잠김과 떠 있는 발을 방지합니다. Play 검증은 실제 변형 메시의 최저점으로 대기 자세의 접지를 확인합니다.

## 애니메이션 검증 결과 (2026-11-23)

최신 main의 공유 에셋 7e3cbab을 결합했습니다. Unity 6000.6.9f1 생성·컴파일·Avatar/클립 Humanoid 검사와 저장 씬 Play 214프레임 검증이 통과했습니다. 대기 반복, 실제 변형 메시의 발 접지, 달리기 다리 뼈대 변화, Jump/Fall/Roll 전환, 기존 점프 높이·구르기·벽 충돌을 확인했습니다. 동작별 캡처도 확인했습니다.

캡처: Artifacts/player-visual-front.png, player-visual-top.png, player-animation-run.png, player-animation-jump.png, player-animation-roll.png. Play 로그의 Unity Search 시작 색인 예외는 여전히 기록되며 애니메이션 코드 경로의 예외와 구분합니다. Windows 빌드·공격 동작·사람의 전환/구르기 속도 수용·정식 지역 전환 검증은 포함하지 않습니다.


## 모션 감속 요청

사용자 요청에 따라 애니메이션만 늦췄습니다. 이전 Jump 0.7초 → 1.1초, Roll 0.3초 → 0.8초입니다. `PlayerAnimation.JumpMotionDuration`·`RollMotionDuration`이 Controller 속도와 시각적 마무리 시간을 함께 결정합니다. 새 점프·구르기는 진행 중인 마무리를 즉시 중단하고 새 동작을 시작합니다.

실제 점프 궤적·구르기 3m/0.3초·무적·쿨다운·입력은 변경하지 않았습니다. 따라서 착지나 실제 구르기가 끝나도 외형 동작이 잠시 이어지고, 그동안 기존 규칙대로 이동·공격할 수 있습니다.

감속 후 저장 씬 261프레임 검증에서 모션 진행률·착지/실제 구르기 종료 뒤 마무리 유지·이후 대기 복귀를 확인했습니다. 기존 점프 높이와 구르기 3m 이동·벽 충돌도 재검증했습니다. Windows 빌드와 사람의 모션 속도 수용은 별도입니다.

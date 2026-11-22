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

- 이번 프리셋에는 동작용 Animator Controller를 연결하지 않아 정적 기본 자세입니다. 원본의 데모 Controller 참조는 프리셋에서 해제합니다. 이동·공격 애니메이션 및 무기 손 부착은 별도 후속입니다.
- 기존 Game PlayerRig, GameConfig, TerrainIntegration과 지역 씬은 교체하지 않았습니다. 이 PR의 프리셋 외형 검토 후 실제 게임 연결 범위를 정합니다.
- Windows 배포 빌드와 사람의 외형·크기·조작감 수용 판단은 별도입니다.

## 검증 결과 (2026-11-23)

Unity 6000.6.9f1에서 생성·에셋 검사가 통과했습니다. 저장된 검토 씬의 Play 158프레임에서 실제 WASD/Space/Shift 입력, 접지·이동·회전·점프/착지·구르기·벽 충돌을 확인했습니다. 정면·탑다운 캡처로 모델 표시와 방향을 확인했습니다. 단차는 수동 검토 대상으로 남습니다.

- 생성: `unity run <UnityProject 절대 경로> -- -executeMethod Nomad.PlayerVisual.Editor.PlayerVisualAuthoring.Build`
- Play 검사: `unity open <UnityProject 절대 경로> --batchmode --args "-executeMethod Nomad.PlayerVisual.Editor.PlayerVisualVerification.Run"` (검사 완료 후 Editor 종료)
- 근거: 로컬 `Artifacts/player-visual-build.log`, `player-visual-play.log`, `player-visual-front.png`, `player-visual-top.png`.
- 최초 Play에서 UnityEditor.Search.SearchDatabase 시작 인덱싱의 ArgumentOutOfRangeException이 1회 기록되었습니다. 프리셋 검사는 통과했으나 Editor 로그 전체가 무오류인 것으로 보고하지 않습니다.

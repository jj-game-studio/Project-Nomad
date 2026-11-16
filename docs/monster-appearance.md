# 해골 몬스터 외형 적용 (#60)

2026-11-17 사용자 이미지/역할 지정:큰 Skeleton Golem을 최종보스, 흰 후드 Necromancer를 엘리트, 나머지를 잡몹으로 적용합니다. 기존 용 방향을 Golem으로 대체하는 요청입니다. 보스 공격/난이도/엔딩 조건까지 새로 확정한 것은 아닙니다.

## 현재 가능한 적용과 미완료

| 역할 | 지정 모델 | 파일/적용 상태 |
| --- | --- | --- |
| 잡몹 | Warrior / Mage / Minion / Rogue | FREE1.1 원본4종으로 외형/재질/기본 애니메이션 연결 |
| 엘리트 | 흰 후드 Necromancer | 지정 파일 미확보, 기존 임시 도형 유지 |
| 최종보스 | 큰 Skeleton Golem | 지정 파일 미확보, 보스 기능/모델 미구현 상태 유지 |

실제 확인한 Downloads/KayKit_Skeletons_1.1_FREE.zip의 캐릭터 FBX/GLB는 위4종입니다. 사용자의 “같은 파일에 있어” 답변 후 전체123엔트리·동봉 contents.png·GLB 노드까지 재확인했습니다. Golem/Necromancer는 포함되지 않았습니다. 제작자 페이지 https://kaylousberg.itch.io/kaykit-skeletons 는 이 두 캐릭터가 EXTRA 구성이라고 안내합니다. 무료 캐릭터 확대/재색칠로 지정 보스·엘리트를 대체하거나 완료했다고 보고하지 않습니다. 정확한 EXTRA ZIP/FBX가 확인되면 해당 슬롯에 연결합니다.

## 구현

- 원본 FBX4개/텍스처/기본 애니메이션2개/동봉 라이선스와 파생 재질·프리팹·Animator·외형 catalog는 비공개 에셋 저장소에서 관리합니다. 원본 ZIP/해시는 기존 보관 위치에 유지하고 임포트 경로·상태를 asset-register.json에 추가합니다.
- public에는 외형 참조/생성·애니메이션 제어 코드만 둡니다. MonsterAppearanceSet.normal의4종은 이름 기반의 고정 선택으로 스폰하며 같은 seed/개체명에서 같은 외형입니다. elite/boss는 미확보 상태라 null입니다.
- 통합씬과 일반/엘리트 스폰창은 같은 catalog를 사용합니다. 기본 잡몹 테스트씬의 첫 도형도 Warrior로 교체합니다. 모델은 시각 높이1.8m로 맞추고 기존 충돌체·NavMesh·HP·탐지·공격 규격을 유지합니다.
- 원본 Generic 리그와 제공된 Idle_A/Walking_A/Running_A/Hit_A/Death_A를 사용합니다. root motion은 끄고 실제 NavMeshAgent 속도로 이동 상태를 선택합니다. 사망은 기존 Health에서 읽습니다. 기본 팩에는 별도 근접 공격 클립이 없어 공격 준비·판정은 기존 예고 표시/AI를 유지합니다.
- 여러 SkinnedMeshRenderer 부위 전체에 상태 색을 적용합니다. hit flash 동안 atlas를 임시 흰 텍스처로 덮고 종료 후 원래 atlas로 복원합니다. 공유 재질 자체는 수정하지 않습니다. 기존 숫자/파편/토글을 유지합니다.
- 시각 확인용 SkeletonGallery.unity와 화면은 기본4종만 보여줍니다. 없는 두 EXTRA 모델의 미리보기로 오인하지 않습니다.

## 검증

49개 몬스터 검사에는 기존46개와 외형 검사3개가 포함됩니다. 새 검사는4개 catalog/SkinnedMesh/재질·Avatar/Idle 상태, 모든 부위 flash·atlas 복원·Death 상태, 실제 agent 이동과 Run 상태/실제 뼈 회전/root-motion 비활성을 확인합니다. 지정 보스·엘리트 파일의 부재는 정상 완료가 아닌 남은 요구사항입니다.

2026-11-17 전체49/49 통과(79.9초), 실제 뼈 회전 검사를 보완한 뒤 관련3/3 재검사 통과(8.9초). 실패/건너뛰기/로그 진단0、종료0. 근거 `Artifacts/tests/monster-tracking/20261117-032551-005dd03d` 및 `20261117-032749-98c52bce`. 새 외형을 사용하는 생성 필드의 실제 사격·근접 피해·파밍·사망/재시작도 `skeleton-gameplay-verification.log`의 GAMEPLAY_INTEGRATION_PASS/종료0으로 확인했습니다.

네이티브 시각 검토 시 Windows 잠금 화면을 관측해 화면 조작을 멈추고 사용자에게 잠금 해제를 요청했습니다. 실제 색상/크기 갤러리 화면은 확인 대기이며 자동 검사 통과를 최종 아트 수용으로 간주하지 않습니다. 보스·엘리트 지정 모델 파일과 사람의 시각 확인이 남아 #60은 완료하지 않습니다.

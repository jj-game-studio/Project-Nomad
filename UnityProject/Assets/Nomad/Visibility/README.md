# 플레이어 시야 검토 (#114)

Unity 메뉴 `Nomad > Visibility > Open Visibility Demo` → Play. WASD 이동, 마우스 방향으로 부채꼴 시야를 돌립니다. 빨간 적은 정면/벽 뒤/등 뒤/덤불 안에 배치했습니다.
초기값: 거리24m, 각도120°, 주변 반경3m, 눈높이1.2m, 판정0.08초, 가림 유예0.08초. Player Visibility Inspector에서 조절합니다.
**Nearby Radius**는 주변 원형 반경 슬라이더/수치(0~20m)입니다. 0이면 원형을 끕니다. 원형 안에서도 벽에 가린 적은 숨깁니다.
`Build Field Preview`는 최신 TerrainIntegration을 VisibilityField.unity로 복사합니다. 원본 씬/생성기/몬스터 AI/구매 프리팹은 변경하지 않습니다.
정식 Game/PlayerRig 연결은 #89/PR95 담당자와 조율 후 진행합니다. 모든 게임 씬에 자동 적용하지 않습니다.

## 시야 범위 디버그

Play 중 Game 화면을 클릭하고 **F3**을 누르면 범위 표시를 켜고 끕니다. 기본값은 꺼짐입니다.
Hierarchy의 **Player Visibility**(모듈은 PlayerVisibilityModule) → Player Visibility 컴포넌트 → **디버그 > Show Debug Range**로도 전환합니다.
청록색은 설정된 거리·각도의 최대 부채꼴, 보라색은 주변 원형, 노란색은 장애물에 막히는 눈높이 경계입니다. Game/Scene 뷰 모두 표시되며 Gizmos 토글이 필요하지 않습니다.
선은 플레이어 눈높이에 그립니다. 지형 바닥의 영역이나 적의 머리/몸통 등 여러 3D 표본을 완전히 나타내는 표시는 아닙니다. 실제 적 판정과 비교하며 사용합니다.
표시를 끄면 디버그 선의 추가 충돌 검사는 실행하지 않습니다. 디버그 선은 충돌체/그림자를 만들지 않습니다.

## 표시 규칙

적 몸통/머리/양옆 중 하나라도 전방 부채꼴 또는 주변 원형에 들어오고 장애물 검사를 통과하면 표시합니다. 주변 원형은 방향을 무시하지만 벽을 통과하는 감지 능력은 아닙니다.
Renderer.forceRenderingOff 및 체력바·피격 숫자만 제어하고 Collider·Health·AI·공격은 유지합니다.
새 Renderer도 LateUpdate에서 반영합니다. 스킬 예고는 적 소속 Renderer와 함께 숨깁니다.
이미 발사된 독립 발사체/소리/독립 사망 드롭은 적 표시와 별개입니다.

## 시야 밖 어둠 (#158)

전 방향 눈높이 방사선512개의 도달 거리를 셰이더에 전달합니다. 화면 깊이로 각 픽셀의 월드 위치를 복원해 부채꼴·주변 원형 밖과 장애물 뒤를 어둡게 덮습니다.
VisibilityField Inspector의 **Shade Color**(알파=농도, 초기0.7)와 **Shade Softness**(경계 부드러움, 초기0.8m)로 조절합니다. 모듈을 끄면 사라집니다.
카메라 앞 사각형(`Visibility Shade`)으로 그리며 카메라의 깊이 텍스처 사용을 켭니다(해제 시 원래 값으로 복원).
눈높이 수평 근사라서 눈높이보다 낮은 엄폐물 뒤 바닥은 밝게 남고, 벽 윗면 일부는 어두울 수 있습니다.

## 식생·소품 반투명 (#158)

시야 안에 들어온 나무·덤불·소품은 원래 텍스처/색을 복사한 반투명 재질(Foliage Fade, 초기 불투명도0.35)로 바뀌고, 시야를 벗어나면 원래 재질로 돌아갑니다.
물체 단위로 판정합니다(중심·지면 근처 테두리4곳 중 하나라도 보이면 반투명). 구매 애셋의 `_Albedo_Map`(Synty)도 복사해 흰색으로 깨지지 않습니다.
나무·덤불은 시선을 막지 않습니다(적 판정·어둠·F3 노란 경계선 모두). 줄기 충돌은 그대로입니다.
소품(통·상자·건초·자루·그루터기·울타리)은 반투명만 적용하고 기존 시야 차단을 유지합니다. 바위·돌담은 대상이 아닙니다.
자동 연결은 FieldDressing 루트의 tree/bush/fern/leaves/prop_/stump 이름을 사용합니다. 다른 이름은 VisibilityFoliage를 명시적으로 붙입니다.

## 제한과 검토

- 덕코프 내부 구현 복제가 아닙니다. 각도/거리·가림 유예·식생 표현과 공격 예고 숨김은 사람 플레이 수용 대상입니다.
- 어둠 마스크는 눈높이 수평 근사여서 경사지/높은 수관에서 적의3D 판정과 다를 수 있습니다.
- 반투명 재질은 BaseMap(_MainTex/_Albedo_Map)/색상/UV/Alpha Cutoff를 복사합니다. 특수 바람/금속/노멀 효과는 재현하지 않습니다. 원본을 보존하고 해제 시 복원합니다.
- 한 플레이어/한 시야 모듈이 대상입니다. 다중 관찰자/분할 화면은 제외합니다.
- 생성 필드는 별도 복사본에서 검토합니다. 실제 구매 식생/지역 전환과 성능 수용은 별도 기록합니다.

## 검증

`Nomad > Visibility > Verify Visibility`로 실제 마우스 방향, 벽/낮은 엄폐물/고저차, 숨긴 적의 피해/충돌, 동적 효과, 모듈/관찰자 해제, 식생 반투명·재질 복원, 나무 비차단, 시야 밖 어둠 렌더 밝기를 검사합니다.
`VisibilityFieldVerification.Run`은 생성 필드3세대와 실제 나무의 비차단·원본 텍스처 반투명·소품 차단 유지를 검사합니다.
검증 결과와 렌더는 PR에 기록합니다. 렌더는 사람 조작 검토와 구분합니다.


## 메인 게임 모듈 연결

PlayerVisibilityModule.prefab은 씬 참조 없이 활성 PlayerMovement를 찾아 연결합니다.
GameConfig.modules에 정식 등록되어 마을·필드·재시작에서 영속 플레이어에 자동 연결됩니다.
기존 PlayerRig·DeathRestart·PauseMenu 모듈의 순서와 참조를 유지하고 시야 모듈만 마지막에 추가합니다.

## 정식 연결 후속 검증

#125/PR #126 병합 후 기존 GameConfig에 정식 등록했습니다. Game 생성 도구도 기존 모듈을 보존합니다.
`Nomad > Visibility > Register Main Game Module`은 기존 영속 모듈을 유지하며 시야 프리팹을 한 번만 추가합니다.
`VisibilityGameRegistration.RegisterAndVerify` 배치 진입점은 기존 모듈 보존과 반복 등록 시 설정 파일 불변을 검사합니다.
등록 후 `Nomad > Visibility > Verify Main Game`으로 마을→필드→같은 seed 재생성→마을→필드→전체 재시작을 검사합니다.
영속 관찰자·시야 인스턴스 유지, 중복 모듈/이전 적 잔존 없음, 적 숨김과 전투/충돌 유지 및 새 플레이어 재바인딩을 확인합니다.
`VisibilityGameVerification.RunPreview` 배치 진입점은 등록 전 검증용입니다. Play 중에만 프리팹을 추가하며 GameConfig를 저장하지 않습니다.
PREVIEW 통과는 정식 등록 검증 통과와 구분합니다. 실제 구매 식생·경사지 외형/성능 및 Windows 빌드는 별도 확인 대상입니다.

# 플레이어 HUD 시안

Unity 6000.6.9f1에서 `Nomad > HUD > Open Preview`를 열고 Play합니다.

- WASD 이동, 좌클릭 사격, Shift 구르기, Space 점프.
- H: 실제 체력 25 감소, M: 마나 시안값 변경, T: 스태미나 시안값 변경, R: 초기화.
- 붉은 체력은 기존 Health를 읽습니다. 마나·스태미나·무기 이름은 미리보기 데이터입니다.
- 2~4 슬롯은 배치 확인용이며 장비 전환·아이템 사용·탄약 규칙은 구현하지 않았습니다.
- 기존 필드/몬스터 통합 씬은 수정하지 않았습니다.

다른 플레이어에는 `PlayerHud`를 추가합니다. 같은 오브젝트 또는 부모의 Health를 자동 연결하며 Inspector에서 직접 지정할 수도 있습니다. 자원 시스템은 `SetResources`, 장비 시스템은 `SetWeapon`으로 표시를 갱신할 수 있습니다. 미연결 자원은 `--`로 표시합니다. HUD는 입력을 가로채지 않습니다.

`Nomad > HUD > Verify Preview`는 전용 씬을 다시 만들고 체력·게이지·배치·비활성/재활성·재시작을 검증합니다. 검증용 렌더 결과는 UnityProject/Artifacts/hud에 저장됩니다. 실행 전 변경 중인 씬은 저장해야 합니다.

도형과 무기 실루엣은 코드로 그린 임시 시안입니다. 글꼴은 Unity uGUI 2.6.0 번들 TMP Essential Resources의 Liberation Sans SDF와 원본 TTF입니다. Fonts의 OFL을 함께 보존했습니다. 필요한 TMP Mobile SDF 셰이더와 include는 같은 번들에서 가져왔으며 Shaders/Unity-LICENSE.md를 보존했습니다. 별도 GUID를 사용해 이후 TMP Essentials 가져오기와 충돌하지 않도록 했습니다. 현재 라벨은 영문이며 한국어 표시에는 별도 CJK 글꼴 구성이 필요합니다.

TMP가 처음 사용되는 프로젝트를 위해 Hud/Resources의 기본 TMP Settings를 함께 제공합니다. 기존 TMP 설정이 있는 프로젝트에서는 해당 설정을 우선 사용합니다.

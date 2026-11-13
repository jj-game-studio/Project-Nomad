# B 최소 지형 조각

관련 작업: [#12](https://github.com/jj-game-studio/Project-Nomad/issues/12).
규격 기준: [common-spec.md](common-spec.md).

## 제작 범위

현재 Synty_Prototype으로 평지, 계단, 높은 평지 프로토타입을 제작합니다.
최종 아트는 추후 교체합니다. 구매 애셋 원본과 이를 참조하는 프리팹·씬은 비공개
Project-Nomad-Assets 저장소의 `Nomad/Terrain`에서 관리합니다.
공개 저장소에는 연결 규격, 제작·검증 코드와 서브모듈 참조만 둡니다.

| 조각 | 루트 위치 기준 | 남쪽 연결점 | 북쪽 연결점 | 통행 바닥 |
| --- | --- | --- | --- | --- |
| Flat | XZ 중앙, 기준 높이 0 | (0, 0, -5) | (0, 0, 5) | 10×10m, 높이 0 |
| Stairs | XZ 중앙, 하단 높이 0 | (0, 0, -5) | (0, 2, 5) | 폭 4m, 양 끝 착지면과 중앙 계단 |
| Raised | XZ 중앙, 하단 기준 높이 0 | (0, 2, -5) | (0, 2, 5) | 10×10m, 높이 2 |

모든 루트 배율은 (1,1,1), 배치 회전은 Y축 90도 단위입니다.
계단 타일도 10×10m를 점유하며, 양옆의 빈 공간은 추가 연결 통로가 아닙니다.
높은 평지의 연결 높이는 로컬 2m에 포함되므로 루트 Y를 다시 2m 올리지 않습니다.

## 외형 교체와 연결 계약

- `Visual`: 원본 프리팹을 참조하는 외형. 내부 충돌체는 제거합니다.
- `Collision`: 외형과 독립된 BoxCollider 바닥·계단. 실제 발판과 일치시킵니다.
- `South`, `North`: TerrainConnection. 원점은 통로 바닥, +Z는 바깥 방향입니다.
- TerrainPiece의 LocalOccupancy: 지형 점유 검사에 사용할 로컬 Bounds. 노란 Gizmo로 표시합니다.
- TerrainConnection의 LocalSeamBounds: 연결점 로컬 좌표의 좁은 경계 영역.
  폭 2m, 유효 높이 2.2m, 총 깊이 0.02m입니다. 청록색 Gizmo로 연결 방향을 확인합니다.

연결 유형은 `Walk`, 폭은 2m, 유효 높이는 2.2m입니다.
Matches는 유형·폭·높이 일치, 위치 오차 0.01m 이내, 반대 방향 및 위쪽 방향 오차
0.1도 이내를 확인합니다. 실제 배치와 겹침 거절 로직은 #14 범위입니다.
WorldOccupancy는 규격에 맞는 루트 배율과 회전에서 사용할 월드 AABB입니다.
경계 접촉은 허용하며 연결 영역 밖의 내부 겹침은 허용하지 않습니다.

아트 교체 시 Visual만 바꾸고 루트, 연결점, Collision과 점유 영역은 유지합니다.
새 외형이 통로·머리 위 여유를 침범하거나 기존 발판과 어긋나면 외형을 맞추거나
#10에서 규격 변경을 조율한 뒤 충돌체와 검증을 함께 갱신합니다.
원본 Synty 프리팹을 직접 수정하거나 새 외형의 충돌체를 중복으로 활성화하지 않습니다.

## 실행과 재현

1. 공개 작업 브랜치와 비공개 애셋 작업 브랜치를 모두 확보하고 서브모듈을 초기화합니다.
2. Unity 6000.6.9f1에서 UnityProject를 엽니다.
3. `Assets/ThirdParty/Nomad/Terrain/B_TerrainPieces.unity`를 엽니다.
4. Play하면 B_TraversalProbe_Automatic이 평지 → 계단 → 높은 평지를 왕복하고 멈춥니다.
   Console의 TERRAIN_TRAVERSAL_PASS를 확인합니다.
5. 조각을 선택하고 Gizmos를 켜 연결점과 점유 영역을 확인합니다.

캐릭터는 지형 검증 전용 CharacterController입니다. 높이 1.8m, 반지름 0.35m,
최대 경사 45도, 단차 0.3m, 이동 5m/s, 중력 20m/s²를 적용합니다.
WASD 입력·카메라 추적 등 A의 플레이어 구현을 대체하지 않습니다.
A의 실제 플레이어·적과 연동한 플레이 검토는 #11 및 후속 작업에서 수행합니다.

`Nomad > Terrain > Validate Saved B Scene`은 저장된 씬을 열어 연결점,
점유 영역, 경계 접촉/내부 겹침 구분, 연결 오류 거절, 2m 폭의 바닥 연속성,
머리 위 여유, 단차, CharacterController 왕복을 확인합니다.
검증 중 변경은 씬에 저장하지 않습니다.

`Nomad > Terrain > Create B Prototype Scene`은 생성된 세 프리팹과 씬을 덮어씁니다.
외형 교체·수작업 편집 후에는 Create 대신 Validate를 사용하십시오.
생성 코드는 원본 계단의 수평 단을 읽어 충돌체를 구성하며 단 높이가 0.25m를 넘으면 실패합니다.

batch 재현 명령(프로젝트를 닫은 상태에서 실행):

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.6.9f1/Editor/Unity.exe' -batchmode -quit -projectPath '<checkout>/UnityProject' -executeMethod Nomad.Terrain.Editor.TerrainPieceAuthoring.CreateAndValidate -logFile '<checkout>/terrain-validation.log'
```

검증 성공 시 TERRAIN_VALIDATION_PASS 로그와 UnityProject/Artifacts/terrain-overview.png를 생성합니다.
Artifacts는 실행 산출물이며 커밋하지 않습니다. 랜덤 생성, NavMesh와 적 배치는 이번 범위에 포함하지 않습니다.

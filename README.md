# Project-Nomad

3D 로우폴리 탑다운 싱글플레이 익스트랙션 슈터 프로토타입입니다.
필드에서 얻은 전리품을 어느 마을로 운반할지 선택하며, 마을별 시세와 경로의 위험을 비교합니다.

## 문서

- [게임 기획서 v0.3](docs/game-design.md): 플레이 루프, 교역, 시간 진행, 성장과 지역 해금 방향
- [개발 협업 루프](docs/development-loop.md): Issue·PR 운영과 사람 검토 기준

기획서의 미정 항목은 구현 전에 관련 Issue에서 결정하고 문서에 반영합니다.
기획서의 약 1시간은 성공 런 1회 기준 플레이타임 목표이며, 개발 일정은 협업 문서의 10일 기준입니다.
현재 구현 상태는 Unity 기본 프로젝트와 애셋 서브모듈 구성 단계입니다.

## 개발 환경

Unity **6000.6.9f1**, Universal 3D(URP) 프로젝트입니다.
Unity Hub에서 `UnityProject` 폴더를 추가해 여세요.
기본 장면: `Assets/Scenes/SampleScene.unity`

## 프로젝트 받기

애셋 저장소 접근 권한이 있는 개발자는 다음 명령을 사용합니다.

```sh
git clone --recurse-submodules https://github.com/jj-game-studio/Project-Nomad.git
```

이미 복제한 프로젝트는 저장소 루트에서 업데이트합니다.

```sh
git pull
git submodule update --init --recursive
```

`UnityProject/Assets/ThirdParty`는 비공개 저장소
[Project-Nomad-Assets](https://github.com/jj-game-studio/Project-Nomad-Assets)를 참조합니다.
접근 권한이 없으면 서브모듈 다운로드가 실패합니다. 코드만 확인하려면
`--recurse-submodules` 없이 공개 저장소를 복제하세요.
현재 기본 장면은 외부 애셋에 의존하지 않습니다.

## 애셋 변경하기

서브모듈 업데이트 후 detached HEAD 상태일 수 있으므로 수정 전에 작업 브랜치를 확인하세요.
애셋 파일과 `.meta`를 함께 커밋하고, 애셋 저장소를 먼저 푸시합니다.

```sh
cd UnityProject/Assets/ThirdParty
git switch main
git pull --ff-only
git add .
git commit -m "Update assets"
git push
```

이어서 메인 저장소 루트에서 참조 커밋 변경을 기록합니다.

```sh
git add UnityProject/Assets/ThirdParty
git commit -m "Update asset submodule reference"
git push
```

외부 애셋은 라이선스를 확인한 뒤 비공개 저장소에 추가하세요.
`ThirdParty.meta`는 메인 저장소에서, 내부 애셋의 `.meta`는 애셋 저장소에서 관리합니다.

# CSV 아이템 데이터

Unity 메뉴: Play 중지 → Nomad > Inventory > Import Item CSV → UTF-8 CSV 선택.
샘플: Assets/Nomad/Inventory/Data/items-example.csv. 샘플은 자동으로 게임에 적용되지 않습니다.

열 순서: item_id,display_name,description,max_stack. 헤더 필수.
ID는 영문/숫자로 시작하고 영문·숫자·밑줄·하이픈만 사용합니다. 대소문자만 다른 ID도 중복으로 거절합니다.
이름 필수, 설명은 빈 값 허용, 최대 중첩 수는 1 이상의 정수입니다.
쉼표·개행이 있는 문구는 큰따옴표로 감싸고, 문구 안 큰따옴표는 두 번 씁니다.
UTF-8 BOM과 CRLF/LF를 지원합니다. 빈 레코드와 추가 열은 오류로 처리합니다.
오류 메시지의 레코드 번호는 인용 문구 안 개행을 제외한 논리 행 번호입니다.

기존 Assets/Nomad 아래 ItemDefinition을 ID로 찾아 이름·설명·중첩 수만 갱신합니다.
객체/GUID·아이콘은 유지하며 CSV에 없는 아이템은 삭제하지 않습니다.
새 아이템은 Assets/Nomad/Inventory/Resources/CsvItems에 생성합니다.
새 아이템의 아이콘은 Inspector에서 지정하고, 사용할 PlayerInventory의 Items 목록에 연결하세요.
기존 아이템을 새 CSV로 다시 생성하거나 다른 ID로 바꾸지 마세요.
데이터 검증과 대상 경로 충돌 검사는 전체 적용 전에 수행합니다.
디스크 오류 등 적용 도중의 I/O 장애까지 원자적으로 복원하는 도구는 아닙니다.
기존 아이템 중첩 수를 줄이면 기존 테스트/보관 데이터와 호환되는지 먼저 확인하세요.

아이콘·프리팹은 Unity 에셋 참조로 관리하고 CSV에는 넣지 않습니다.
장비 10칸(무기2·머리·신체·얼굴·장신구·반지·가방·토템2)은 #96의 후속 작업입니다.
분류·장착 부위·능력치 CSV 열, 장착·전투 연결, 장비 UI·툴팁은 이번 도구에 포함하지 않습니다.

검증 진입점: Nomad.Inventory.Editor.ItemCsvVerification.Run (Unity batchmode).
기존 회귀: Nomad.Inventory.Editor.InventoryVerification.Run.

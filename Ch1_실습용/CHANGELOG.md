# CHANGELOG

## 2026-10-27 10:37 — 매출 대시보드 제작 (HTML 시안 → Windows 네이티브 앱으로 전환)

### 목표
`sales.xlsx` 기반 매출 대시보드를 팀 에이전트로 만들고, 같은 엑셀 포맷이면 다른 파일도 불러와 동작하게 하며 PDF 저장 기능을 넣는다. 도중에 사용자가 "HTML이 아니라 Windows 프로그램"으로 방향을 바꿨다.

### 변경 사항
- `sales_dashboard.html` — 1차 산출물(단일 HTML, 패널 6개 + 주문표, 필터). 엑셀 불러오기/PDF는 **구현하지 않고 중단**(방향 전환). 데이터는 `sales.xlsx` 값이 내장된 정적 상태.
- `SalesDashboardApp/` — 최종 산출물(WinForms, C#). 엑셀 불러오기(버튼·드래그앤드롭), 지역/제품/채널 필터, PDF 저장.
  - `Models.cs` 모델·테마색, `XlsxReader.cs` xlsx 파서/검증, `Widgets.cs` GDI+ 차트, `DashboardRenderer.cs` 화면·PDF 공용 렌더러, `PdfWriter.cs` 이미지 기반 PDF 작성, `MainForm.cs`/`Program.cs` UI, `build.bat` 빌드, `SalesDashboard.exe` 빌드 결과물.
- `CLAUDE.md`, `semiconductor_news_dashboard.html` — 이 세션 이전부터 git 상 변경/미추적 상태. 이 세션에서 수정하지 않음.
- 중간 산출물(HTML 패널 JS, 계약서, 테스트 코드)은 스크래치패드에만 있고 폴더에는 없음.

### 결정 사항 / 이유
- **WinForms 네이티브 exe 선택**(사용자 선택). 대안 Electron(200MB 다운로드), WebView2(NuGet DLL 필요)는 제외. 이 PC에는 .NET SDK가 없고 .NET Framework 4의 `csc.exe`만 있어 **C# 5 문법만 가능**(`$""`, `?.`, 식 본문 멤버 금지).
- **PDF는 외부 라이브러리 없이 직접 작성**: 페이지를 비트맵으로 그려 JPEG로 넣는 방식. 한글 폰트 임베딩 없이 되지만 **PDF 안 글자는 선택·검색 불가**. 블록(패널) 단위로 페이지를 채워 표가 페이지 경계에서 잘리지 않게 함(주문표는 인쇄용 13행씩 분할, 블록마다 머리글 반복).
- 월별 축은 데이터의 최소~최대 월로 자동 생성, 제목은 기간에서 상반기/하반기/연간 자동 판별.
- 엑셀 파서는 공유 문자열과 inlineStr 둘 다 지원(실제 엑셀은 공유 문자열). 필수 컬럼이 없으면 불러오지 않고 기존 화면 유지.
- 에이전트 운용: Haiku 3명이 파일 단위로 분담(A 엑셀 읽기, B 차트, C PDF), 계약서 `CONTRACT.md`를 먼저 작성. 보고는 그대로 믿지 않고 직접 재검증.

### 현재 상태
- 완료: 위 앱 전체(빌드·기동·렌더링·PDF 생성).
- 검증(실제 실행):
  - `build.bat` → `BUILD OK`, exe 54KB.
  - `sales.xlsx` → 120건 / 334,570,000원 / 경고 0. `sales_new.xlsx` → 120건 / 326,110,000원 / "오늘 이후 날짜 57건" 경고 1.
  - 공유 문자열 변환본, 컬럼 누락 파일, 가짜 `.xlsx` 모두 예외 없이 처리(에이전트 A가 실행, 보고 기준).
  - 렌더 결과 PNG를 직접 확인(KPI·월별·채널·지역·제품·담당자·주문표). 실제 창 캡처로 `sales_new.xlsx` 상태("2026 하반기", 7~12월 축, 경고 줄) 확인.
  - PDF 구조(xref 24개 오프셋 일치, 7쪽, JPEG 7개) Node 스크립트로 확인.
- **미검증**: 실제 PDF 뷰어에서 열었을 때의 모양(Edge 헤드리스 캡처 실패), 엑셀 열기/PDF 저장 대화상자와 필터 콤보 클릭 등 **GUI 조작 전반**(기동·자동 불러오기·렌더링까지만 확인), 다른 PC에서의 실행, 사용자가 실제 엑셀에서 저장한 파일.
- 미완: 없음(요청 범위 기준). `sales_dashboard.html`에는 엑셀/PDF 기능이 없음.

### 다음 할 일
1. `SalesDashboardApp/SalesDashboard.exe`를 직접 실행해 [엑셀 불러오기], 필터, [PDF 저장] → 저장된 PDF를 뷰어로 열어 확인. 이상 시 `PdfWriter.cs` / `DashboardRenderer.RenderPages`부터 점검.
2. 실제 업무 엑셀(공유 문자열 방식, 컬럼 순서 다른 파일)로 불러오기 시험.
3. `sales_dashboard.html`을 유지할지 정리(Windows 앱으로 대체됨).
4. (선택) 다른 PC 배포 시 SmartScreen 경고 안내, DPI 150% 이상에서 흐릿함 여부 확인(앱에 dpiAware 매니페스트 없음).

### 주의 / 알려진 문제
- 컴파일러가 C# 5 한정이므로 새 코드에 `$""`, `?.` 등 사용 금지. 빌드 명령은 `SalesDashboardApp/build.bat`(ASCII 메시지; 한글 메시지 bat은 이 환경에서 실행 실패했음).
- Bash 도구의 `cd` 후 상대 경로 실행이 이 환경에서 불안정함 → 빌드/실행은 절대 경로 + PowerShell 사용.
- `sales_new.xlsx`의 주문번호(ORD-0001~0120)는 `sales.xlsx`와 겹침. 앱은 파일 하나씩만 불러오므로 영향 없으나 합치는 기능은 없음.
- `sales.xlsx`에서 담당자와 지역이 1:1 대응이라 두 패널이 같은 숫자를 보여줌(데이터 특성).
- 폴더에 `매출대시보드_20261027.pdf`(2,634,333바이트, 10:20 생성)가 있음. 이 세션의 테스트 산출물 `h2.pdf`와 바이트 수가 같지만 **누가 어떻게 만들었는지 확인하지 못함**. 구조는 정상(7쪽). 필요 없으면 삭제 여부를 사용자에게 확인할 것.
- 중간 산출물 위치: 스크래치패드 `…/scratchpad/app/`(계약서, 테스트 하니스 `test_main/`, `pdfcheck.js`), `…/scratchpad/sd/`(HTML 패널 소스·`build.js`). 세션 종료 시 사라질 수 있음.

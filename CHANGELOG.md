# CHANGELOG

## 2026-10-27 16:20 — Ch5 업무 문서 LLM 위키 구축 + 그래프 뷰어(wiki_graph.html)

### 목표
`실습자료/Ch5_실습용/documents/`의 업무 지식 문서 8건을 LLM Wiki 방식(원문 → 링크된 마크다운 페이지)으로 컴파일하고, 그래프로 탐색할 수 있는 단독 HTML을 만든다.

### 변경 사항
- `실습자료/Ch5_실습용/build_wiki.py` — 위키 페이지·관계 정의(NODES/EDGES)와 HTML 템플릿을 모두 담은 생성 스크립트. 인자 없이 `Ch5_실습용/`에서 `python build_wiki.py` 실행 시 `wiki/`와 `wiki_graph.html`을 재생성
- `실습자료/Ch5_실습용/wiki/` — 페이지 62개(유형별 폴더: 설비·공정·사건·원인·개념·규정·불량·인물·문서·점검) + `index.md`, `log.md`, `SCHEMA.md`. 페이지 하단에 `## 관계`, `## 역링크`, frontmatter에 `sources`
- `실습자료/Ch5_실습용/wiki_graph.html` — 외부 라이브러리 없는 단독 그래프 뷰어(힘 기반 배치, 유형 필터, 검색, 노드 클릭 시 본문·관계·근거 문서 표시, 이름 마스킹 토글)
- `CHANGELOG.md`, `실습자료/CHANGELOG.md` — 본 항목 (두 파일은 같은 내용으로 유지)

### 결정 사항 / 이유
- 원문(`documents/`)은 수정하지 않음. 위키 내용은 사람이 읽고 정리한 것이라 자동 추출이 아니라 스크립트 안에 직접 기술(NODES/EDGES). 새 문서를 넣으면 스크립트에 노드·관계를 추가해야 함
- 관계는 정규식 자동 추출 대신 명시적 라벨(근본 원인, 재발 방지, 징후 간과 등)로 정의 → 그래프에서 인과 사슬이 보이게 함
- 8/3 압력 편차(4%대)가 신 기준 ±3%였다면 초과였을 것이라는 서술은 **추정**이며 페이지에 그렇게 표기
- 이름 마스킹 토글은 요청 외 추가 기능. 기준은 `Ch5_실습용/masking-list.txt` (이름 첫 글자 + ○○)
- `file://`·로컬 서버 방식의 브라우저 확장 확인은 접속 오류로 불가 → 헤드리스 Edge 스크린샷으로 대체

### 현재 상태
- 완료: 위키 62페이지·관계 111개 생성, 그래프 첫 화면 렌더링 확인, 커밋·푸시 요청됨(이 항목과 함께 커밋. 해시는 `git log` 참고)
- 검증: `python -I build_wiki.py` → `pages=62 edges=111`. 생성된 JS는 `node --check` 통과. 헤드리스 Edge 스크린샷에서 노드·라벨·패널 정상 표시(최초 렌더는 노드 뭉침·폭주 문제가 있어 척력/속도 상한/초기 배치·화면맞춤 수정 후 재확인)
- 미검증: 노드 클릭, 드래그, 줌, 검색, 유형 필터, 마스킹 토글의 실제 동작(헤드리스 정적 캡처만 확인). 좁은 화면 레이아웃도 미확인

### 다음 할 일
1. `Ch5_실습용/wiki_graph.html`을 브라우저에서 직접 열어 클릭·검색·마스킹 동작 확인
2. 문서가 추가되면 `build_wiki.py`의 NODES/EDGES에 반영 후 재실행, `wiki/log.md`에 ingest 기록
3. `wiki/점검/확인 필요·모순 사항.md`의 6건(매뉴얼 개정일 불일치 등)을 담당자에게 확인

### 주의 / 알려진 문제
- 위키·HTML에 실명(masking-list의 7명)이 그대로 들어 있음. 외부 공유 시 마스킹 토글은 화면 표시만 가림(wiki/*.md는 가려지지 않음)
- 저장소(`실습자료/.git`)에는 이번 작업과 무관한 미추적·수정 파일이 다수 남아 있음(`CLAUDE.md` 수정, `.claude/skills/anomaly-excel-report/`, `Ch2_실습용/` 일부, `Ch5_실습용/find_anomaly.py`, `이상데이터_분석.xlsx`, `내 업무/` 등). 이번 커밋에는 포함하지 않음
- `Ch5_실습용/.env`는 `.gitignore`(`**/.env`)로 제외됨. 내용은 열람·기록하지 않음
- PowerShell에서 `python`이 경로 오류를 내는 경우가 있어 Bash에서 실행함

## 2026-10-27 13:05 — newsapi_clone 초기 스캐폴딩 + Hero(블랙홀) 구현 (REA 분석 기반)

### 목표
REA로 분석한 newapi.ai 마케팅 사이트(`newsapi_clone/docs/01_ANALYSIS.md`)를 Next.js로 재현하는 학습용 클론의 첫 단계(스캐폴딩 + 홈 Hero).

### 변경 사항
- `newsapi_clone/docs/01_ANALYSIS.md`, `02_PLAN.md`, `home_reference.png` — 사이트 분석, 구현 계획, 기준 스크린샷 (11:11 생성)
- `newsapi_clone/package.json`, `tsconfig.json`, `next.config.mjs`, `postcss.config.mjs` — Next 15 / React 19 / Tailwind 4 / TS 설정
- `newsapi_clone/app/{layout,page}.tsx`, `app/globals.css` — 루트 레이아웃과 홈 (현재 `app/[locale]/` 구조 아님)
- `newsapi_clone/components/Header.tsx`, `Hero.tsx` — 헤더, Hero 섹션
- `newsapi_clone/components/BlackHole.tsx`, `blackhole.frag.ts` — 가르강튀아풍 블랙홀 배경(WebGL 프래그먼트 셰이더 추정, 코드 미검토)
- `실습자료/Ch2_실습용/news-dashboard.html` — 11:45에 수정됨 (이번 작업과의 관련·내용 미확인)

### 결정 사항 / 이유
- 클론 대상은 게이트웨이 제품이 아닌 **마케팅 사이트**(분석 문서 상단 참고). 브랜딩·카피·로고는 자체 대체(`02_PLAN.md`).
- 계획상 i18n(`app/[locale]/`, en/ko), `next-themes`, `data/plugins.json`은 **아직 미적용**.

### 현재 상태
- 완료: 스캐폴딩, Header, Hero, BlackHole 컴포넌트 파일 생성 (확인: 파일 존재)
- 미완: Plugins / Privacy Gateway / AI Gateway(Product guide) 페이지, 푸터, 홈의 Product Tour·로고 마퀴·Features·Process·CTA 섹션, i18n, 테마 전환
- 검증: `.next/`(dev 산출물, 11:46)가 있어 `next dev`가 한 번 실행된 흔적은 있으나, 이번 handoff 시점에 빌드·화면 확인·테스트를 **직접 실행하지 않음 → 미검증**
- git: `newsapi_clone`은 저장소가 아님. 실제 저장소는 `실습자료/.git`. 이번 작업은 **커밋·푸시하지 않음**(요청 없음).

### 다음 할 일
1. `cd newsapi_clone && npm run build` 로 타입/빌드 오류 확인, `npm run dev`로 Hero를 `docs/home_reference.png`와 비교
2. `02_PLAN.md` 순서대로 홈 나머지 섹션 → 푸터 → 나머지 3개 페이지 → i18n/테마
3. 커밋이 필요하면 저장소 위치(`실습자료/.git`)에 복사할지 먼저 결정

### 주의 / 알려진 문제
- 이 항목은 파일 타임스탬프와 문서 내용으로 재구성한 것이라 세션 중 의도·시행착오는 반영되지 않음. 이전 세션 담당자 확인 권장.
- `newsapi_clone/node_modules`, `.next`는 커밋 대상 제외.
- 터미널(PowerShell)에서 이 파일을 보면 한글이 깨져 보일 수 있음(파일은 UTF-8).
- `실습자료/CHANGELOG.md`에 같은 시각의 사본이 있으나 이번에는 갱신하지 않음.

## 2026-10-27 11:41 — k-skill 스크린샷 분석 스킬·문서 생성

### 목표
`ETC/image.png`(NomaDamas/k-skill GitHub 카드)를 분석해 보고서를 쓰고, 스킬 폴더와 관련 문서를 서브에이전트로 병렬 작성.

### 변경 사항
- `.claude/skills/k-skill-analysis/SKILL.md` — 문서 묶음을 가리키는 스킬 정의 (직접 작성)
- `.claude/skills/k-skill-analysis/docs/01_분석_보고서.md` — 스크린샷 분석 (직접 작성)
- `.claude/skills/k-skill-analysis/docs/02_스킬_카탈로그.md` — 분야별 스킬 표, DS 실습용 Top 5 (서브에이전트)
- `.claude/skills/k-skill-analysis/docs/03_활용_가이드.md` — 호출 예시 10개, 실습 과제 3개 (서브에이전트)
- `.claude/skills/k-skill-analysis/docs/04_리스크_평가.md` — 도입 평가표·체크리스트 (서브에이전트)
- `CHANGELOG.md` — 본 파일

### 결정 사항 / 이유
- 스크린샷에서 확인된 값만 사실로 기재: Stars 2k, Forks 131, Issues 12, Contributors 3. 언어(노란·파란 막대)는 추정으로 표기.
- 설치 명령은 근거가 없어 "저장소 README 확인 필요"로 둠.
- k-skill은 이미 전역(`~/.claude/skills`)에 설치돼 있어 재설치하지 않음. (`npx skills find`로 `nomadamas/k-skill` 검색만 확인)

### 현재 상태
- 완료: 위 문서 5개 생성 (파일 존재 확인).
- 검증: 서브에이전트 작성분(02~04)은 내용을 직접 읽어 검수하지 않음 → **미검증**. 04는 서브에이전트가 스크린샷 원본을 못 보고 프롬프트의 수치를 사용.
- git: 상위 폴더(`삼성전자_DS_실습자료`)는 저장소가 아니고, `실습자료/.git`(origin: rock8181/A_track_AI_making_261027, main)이 실제 저장소. 문서를 이 저장소로 복사해 커밋·푸시함 (결과는 아래 커밋 로그 참고).

### 다음 할 일
1. `.claude/skills/k-skill-analysis/docs/02_스킬_카탈로그.md`, `04_리스크_평가.md` 검수 (지어낸 스킬명·수치 여부).
2. 카탈로그의 "확인 필요" 항목(`korean-stock-search`, `bok-ecos-stats` 등 API키 여부)을 각 `SKILL.md`에서 확인.

### 주의 / 알려진 문제
- `Ch2_실습용/`의 미추적 파일(`.claude/`, `CLAUDE.md`, `data/`, `news-dashboard.html`)은 이 작업과 무관해 커밋하지 않음.

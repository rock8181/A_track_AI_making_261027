# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 개요

삼성전자 DS 교육용 Claude Code 실습자료 모음입니다. 하나의 앱이 아니라 챕터별로 독립된 폴더이며, 빌드/린트/테스트 체계와 git 저장소는 없습니다. `*_실습용`은 수강생이 시작하는 폴더, `*_완성본`은 정답 예시입니다. 문서·주석·응답은 한국어로 작성합니다.

| 폴더 | 내용 |
|---|---|
| `Ch1_실습용/` | `sales.xlsx`, `sales_new.xlsx` (엑셀 처리 실습 입력) |
| `Ch2_실습용/samples/` | 결과물 형태 참고용 HTML (dashboard / doc / slide) |
| `Ch2_완성본/` | AI 반도체 시장조사 멀티 서브에이전트 예제 (자체 `CLAUDE.md`와 `.claude/agents/` 포함) |
| `Ch5_실습용/` | RAG 실습 시작 폴더 (원문 문서, 로그 CSV, `masking-list.txt`, `RAG보조자료.html`) |
| `Ch5_완성본/` | 동작하는 로컬 RAG 검색 시스템 (아래 참고) |
| `내 업무/guide/` | 업무를 Agent로 설계하는 단계별 가이드 (`업무설계_가이드.md`, `router.md`, `parallel.md`, `gen-eval.md`) |

## 작업 규칙

- 커밋·푸시는 직접 하지 않고 Haiku 서브에이전트(`model: "haiku"`)에 맡깁니다. 서브에이전트에는 변경 파일 목록과 커밋 메시지 방향을 넘기고, `.env`·API 키 등 비밀값이 스테이징되지 않았는지 확인시킨 뒤 결과(커밋 해시, 푸시 여부)를 보고받습니다.

## Ch5_완성본 (RAG) 명령어

Node 22.5+ 필요 (`node:sqlite`, `process.loadEnvFile` 사용). 외부 npm 의존성이 없어 `npm install`은 필요 없습니다. 실행은 `Ch5_완성본/`에서 합니다.

```
node scripts/build-rag.js                         # documents/ → Chunking → Embedding → rag.db 재생성 (API 호출)
node scripts/search-rag.js "질문" [--top 5] [--json]   # CLI 검색
node server.js                                    # http://localhost:3000 검색 UI (PORT 환경변수로 변경)
node scripts/test-embedding.js ["문장" ...]        # 임베딩 API 연결 테스트
node scripts/test-chunking.js [파일]               # 3가지 Chunking 비교 → output/chunking-comparison.html
```

테스트 프레임워크는 없으며 `test-*.js`는 수동 확인용 스크립트입니다.

## Ch5_완성본 아키텍처

- `scripts/build-rag.js`: `FILES` 배열에 하드코딩된 5개 문서(`documents/*.txt`)만 인덱싱합니다. 문서를 추가하면 이 배열도 고쳐야 합니다. 실행할 때마다 `chunks` 테이블을 DROP 후 재생성합니다.
  - Chunking은 정규식으로 제목 구조를 인식합니다 (`LEVEL1`: `제N장`/`N.`, `LEVEL2`: `N.N`/`[이름 - 팀]`). 본문이 `MIN_BODY`(40자) 미만인 섹션은 같은 장의 인접 섹션과 합칩니다.
  - 모든 Chunk 앞에 `[제목 · 문서번호 · 작성일 · 관련 설비]` 문서 컨텍스트를 붙입니다. 회의록은 본문에 설비 정보가 없어 파일명에서 설비 ID를 추출합니다 (`_ETCH-02_` 형태).
- `scripts/search-rag.js`: `rag.db`를 메모리에 올려(파일 mtime이 바뀌면 재로딩) 질문을 임베딩한 뒤 코사인 유사도로 순위를 매깁니다. 모듈(`search`)과 CLI를 겸하며 `server.js`가 모듈로 가져다 씁니다.
- `server.js`: `127.0.0.1` 전용 `http` 서버. `GET /`는 `rag-search.html`, `GET /api/search?q=&k=`는 JSON을 반환합니다 (k는 1~20으로 제한).
- 임베딩은 외부 API(`defect-rag-embedding-api.vercel.app`)에 `Authorization: Bearer $EMBED_API_KEY`로 요청합니다. 키는 `Ch5_*/.env`에 있으며 절대 출력·커밋하지 마세요. `data/*.csv`(검사로그, 설비센서로그)는 RAG 인덱스에 들어가지 않습니다.

## 서브에이전트 예제 (Ch2_완성본)

`market-analyst`, `supplier-analyst`, `demand-analyst`를 병렬 호출 → `report-writer` → `report-reviewer` → `report-writer` 1회 수정 순서입니다. 그 폴더의 `CLAUDE.md`는 해당 폴더 밖 접근을 금지합니다. 이 규칙은 그 폴더에서 작업할 때 적용됩니다.

## 업무설계 가이드 (내 업무/guide)

`업무설계_가이드.md`는 STEP 1~4를 사용자가 요청한 단계만 진행하며 결과를 `업무설계서.md`에 누적 저장합니다. 사용자가 확인하기 전에는 파일을 수정하지 않고, 기존 내용은 요청 없이 고치지 않습니다. 실습 입력 자료는 `data/`, 결과물 샘플은 `reference/example.html`(standalone HTML)에 저장합니다. `router.md`, `parallel.md`, `gen-eval.md`는 이후 단계에서 쓰는 패턴 가이드입니다.

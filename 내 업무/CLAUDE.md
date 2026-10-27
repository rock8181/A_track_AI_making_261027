# CLAUDE.md — 내 업무 (운영 시스템 VOC 처리 Agent)

접수된 VOC 티켓을 분석해 티켓별 원인·조치·답변 초안을 담은 **VOC 처리 검토 보고서(HTML)**를 만드는 프로젝트입니다. 응답·문서·주석은 한국어로 작성합니다.

## 실행

VOC 처리 요청("VOC 처리해줘", "오늘 VOC 정리" 등)이 오면 **`voc-process` 스킬을 실행**하고 그 순서를 그대로 따릅니다. 스킬 없이 임의 순서로 처리하지 않습니다.

- 모든 명령은 이 폴더(`내 업무/`)에서 실행합니다.
- Python 3 + `openpyxl` 필요. 외부 API 호출은 없습니다.
- 결과: `output/VOC_검토보고서_<YYYYMMDD>.html` (완료 후 열어서 사용자에게 안내)

```
python scripts/voc_tools.py load      # 코드 1: 티켓 + 요청자 정보 (output/work 초기화)
python scripts/voc_tools.py context   # 코드 3: 티켓별 로그·DB 조회 (classified.json 필요)
python scripts/voc_tools.py status    # 티켓별 진행 상태 (다음에 할 일 판단용)
python scripts/voc_tools.py report    # 코드 6: 보고서 생성
python data/make_data.py              # 실습용 가상 입력 자료 재생성
```

## 설계 문서 (기준 문서)

| 파일 | 역할 |
|---|---|
| `업무설계서.md` | 업무·입력 자료·결과물·Task 정의 (STEP 1~4) |
| `workflow.md` | 확정된 Workflow: Parallel ③ · Router · Gen / Eval (재작성 최대 2회) |
| `my-agent-team.html` | Workflow 시각화 |
| `reference/example.html` | 결과물 샘플 (보고서 구조·완성 수준 기준) |

구현(스킬·에이전트·스크립트)이 설계와 어긋나면 **`workflow.md`가 기준**입니다. Workflow 자체를 바꿀 때는 사용자 확인 후 `workflow.md` → `my-agent-team.html` → 구현 순서로 함께 고칩니다.

## 구성

| Workflow Task | 실행 주체 | 구현 |
|---|---|---|
| 1 티켓 + 요청자 정보 | 코드 | `scripts/voc_tools.py load` |
| 2 분류·우선순위·중복 식별 (Router 조건) | AI | `voc-classifier` (전체 1회) |
| 3 로그·DB 조회 | 코드 | `scripts/voc_tools.py context` |
| 4 원인·조치 판단 | AI | `voc-analyst` (장애·문의만, 티켓별 병렬) |
| 5 답변 초안 (Generator) | AI | `voc-answer-writer` (티켓별 병렬) |
| 5-1 답변 검토 (Evaluator) | AI | `voc-answer-reviewer` (티켓별 병렬) |
| 6 검토 보고서 | 코드 | `scripts/voc_tools.py report` |
| 7 검토·답변 확정 | 사람 | 사용자 |

- 서브에이전트는 다른 서브에이전트를 호출할 수 없습니다. **메인 대화가 스킬을 따라 순서를 지휘**하고, 병렬 단계는 한 메시지에 여러 Agent 호출을 함께 보냅니다.
- 단계 간 데이터는 `output/work/`의 JSON 파일로만 주고받습니다. 서브에이전트에게는 티켓번호만 넘깁니다.

```
output/work/
  tickets.json  history.json  classified.json
  context/<티켓>.json   analysis/<티켓>.json   answer/<티켓>.json   review/<티켓>.json
```

- Router 경로 값은 정확히 `장애·문의` / `중복 VOC` / `개선 요청` 중 하나입니다 (`voc_tools.py`의 `ROUTES`).
- Gen / Eval 반복 상태는 `answer`와 `review`의 `revision` 값으로 판단합니다. 재작성 횟수 상한은 `voc_tools.py`의 `MAX_REVISIONS`(=2)입니다.

## 반드시 지킬 규칙

- **실제 조치를 하지 않습니다.** 답변 발송, 계정 잠금 해제, 결재선 재지정, 권한 부여, 배치 재실행은 모두 사람(Task 7)의 몫이고, Agent는 보고서에 제안만 합니다.
- 서브에이전트 결과 JSON을 메인 대화가 대신 고쳐 쓰지 않습니다. 문제가 있으면 해당 에이전트를 다시 호출합니다.
- 근거는 `output/work/context/`에 조회된 데이터와 `data/`의 문서에서만 가져옵니다. 없는 로그·원인·일정을 지어내지 않습니다.
- `data/`의 입력 자료와 `reference/example.html`은 처리 중에 수정하지 않습니다.
- 기준값(조회 기간, SLA, 재작성 횟수)은 `scripts/voc_tools.py` 상단 상수에서만 바꿉니다.
- `data/`의 모든 자료는 실습용 가상 데이터입니다 (가상 시스템 "OPS-PR 구매요청·결재 시스템").

## 수정할 때 같이 고칠 곳

| 바꾸는 것 | 함께 확인할 파일 |
|---|---|
| JSON 필드 (분류·분석·답변·검토) | 해당 `.claude/agents/*.md`의 출력 형식 + `voc_tools.py`의 `build_item` / `render_card` |
| 답변 검토 기준 | `voc-answer-writer.md`와 `voc-answer-reviewer.md` 양쪽 + `workflow.md` 4장 |
| Router 경로 | `voc-classifier.md`, `voc_tools.py`(`ROUTES`, `build_item`), 스킬 4단계, `workflow.md` 3장 |
| 입력 자료 파일 이름·열 | `voc_tools.py` 상단 경로 상수와 `cmd_load` / `cmd_context` |

## 업무설계 가이드

`guide/업무설계_가이드.md`(STEP 1~4)와 패턴 가이드(`parallel.md` → `router.md` → `gen-eval.md`)는 이미 진행을 마쳤습니다. 다시 진행할 때는 사용자가 요청한 단계만 진행하고, 사용자 확인 전에는 `업무설계서.md`와 `workflow.md`를 수정하지 않습니다.

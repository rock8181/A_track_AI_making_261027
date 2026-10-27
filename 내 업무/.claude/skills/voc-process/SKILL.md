---
name: voc-process
description: 접수된 운영 시스템 VOC 티켓을 workflow.md 설계대로 처리해 'VOC 처리 검토 보고서' HTML을 만든다. 티켓 분류·중복 식별 → 로그·DB 조회 → 티켓별 원인 분석과 답변 초안(병렬) → 답변 검토·재작성(최대 2회) → 보고서 생성. "VOC 처리해줘", "오늘 VOC 정리", "VOC 답변 초안 만들어줘" 같은 요청에 사용.
---

# VOC 처리 (workflow.md 실행)

`내 업무/` 폴더에서 실행합니다. 설계는 `workflow.md`, 결과물 형태는 `reference/example.html`을 따릅니다.
코드 Task는 `scripts/voc_tools.py`, AI Task는 `.claude/agents/`의 서브에이전트가 맡습니다.
모든 중간 결과는 `output/work/`에 JSON으로 남고, 단계끼리는 파일로만 주고받습니다.

| Task | 실행 | 결과 |
|---|---|---|
| 코드 1 티켓 + 요청자 정보 | `python scripts/voc_tools.py load` | `output/work/tickets.json`, `history.json` |
| AI 2 분류·우선순위·중복 | `voc-classifier` 1회 | `output/work/classified.json` |
| 코드 3 로그·DB 조회 | `python scripts/voc_tools.py context` | `output/work/context/<티켓>.json` |
| AI 4 원인·조치 | `voc-analyst` 티켓별 병렬 (장애·문의만) | `output/work/analysis/<티켓>.json` |
| AI 5 답변 초안 | `voc-answer-writer` 티켓별 병렬 | `output/work/answer/<티켓>.json` |
| AI 5-1 답변 검토 | `voc-answer-reviewer` 티켓별 병렬 | `output/work/review/<티켓>.json` |
| 코드 6 보고서 | `python scripts/voc_tools.py report` | `output/VOC_검토보고서_<날짜>.html` |
| 사람 7 검토·확정 | 사용자 | - |

## 진행 순서

### 1. 코드 1
`python scripts/voc_tools.py load` 실행. 이전 `output/work/`는 지워지고 새로 만들어집니다. 처리할 티켓이 0건이면 사용자에게 알리고 끝냅니다.

### 2. AI 2
`voc-classifier`를 1회 호출합니다 (전체 티켓을 한 번에 봐야 중복을 찾을 수 있음).

### 3. 코드 3
`python scripts/voc_tools.py context` 실행.

### 4. 티켓별 Parallel + Router
`classified.json`의 route를 보고 분기합니다.

1. route가 `장애·문의`인 티켓마다 `voc-analyst`를 호출합니다. **모든 호출을 한 메시지에 함께 보내 병렬로 실행**합니다. 프롬프트: `티켓 <티켓번호>를 분석해줘.`
2. 분석이 끝나면 **모든 티켓**(세 경로 전부)에 대해 `voc-answer-writer`를 한 메시지에 병렬 호출합니다. 프롬프트: `티켓 <티켓번호>의 답변 초안을 작성해줘.`
   - `중복 VOC`, `개선 요청` 티켓은 AI 4를 거치지 않으므로 1번과 같은 메시지에서 바로 호출해도 됩니다.

### 5. Gen / Eval (재작성 최대 2회)
1. 답변이 있는 모든 티켓에 `voc-answer-reviewer`를 한 메시지에 병렬 호출합니다. 프롬프트: `티켓 <티켓번호>의 답변을 검토해줘.`
2. `python scripts/voc_tools.py status`로 상태를 확인합니다.
   - `재작성 필요` 티켓만 `voc-answer-writer`를 다시 호출합니다. 프롬프트: `티켓 <티켓번호>의 답변을 검토 결과에 맞게 다시 작성해줘.` 그 뒤 해당 티켓만 다시 검토합니다.
   - 모든 티켓이 `검토 통과` 또는 `검토 미통과`가 될 때까지 반복합니다. `검토 미통과`는 재작성 2회 후에도 FAIL인 건이며, 더 고치지 않고 보고서로 넘깁니다.

### 6. 코드 6
`python scripts/voc_tools.py report` 실행. 끝나지 않은 티켓이 있다고 나오면 4~5단계로 돌아가 마무리합니다.

### 7. 사람 7에게 넘기기
보고서를 열고 사용자에게 아래만 짧게 알립니다.
- 보고서 경로
- 먼저 볼 건(우선순위 높음)과 처리 기한
- 직접 조치 / 이관 건수
- `검토 미통과` 티켓이 있으면 그 목록

## 규칙
- 답변 발송, 계정 잠금 해제, 결재선 재지정 같은 실제 조치는 하지 않습니다. 결정과 실행은 사람 7의 몫입니다.
- 서브에이전트의 결과 JSON을 메인에서 대신 고쳐 쓰지 않습니다. 문제가 있으면 해당 에이전트를 다시 호출합니다.
- 서브에이전트가 파일을 저장하지 못했으면 같은 티켓만 한 번 다시 호출하고, 그래도 실패하면 보고서 생성 후 사용자에게 알립니다.

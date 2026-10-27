# CHANGELOG

## 2026-10-27 16:59 — my-agent-team.html을 5개 섹션 standalone 페이지로 재구성

### 목표
`05_보고서.md` 요청대로 `my-agent-team.html` 한 파일에 업무·입력 자료·Sub Agent 역할·Workflow·결과물 샘플이 모두 보이도록 만든다. 샘플(`reference/example.html`)은 경로 참조 없이 파일 안에 포함한다.

### 변경 사항
- `my-agent-team.html` — 기존 Workflow 시각화 페이지를 확장. 상단 요약 4칸과 고정 목차를 추가했고, 섹션은 1) 업무(적용 전후 비교, Task 1~7 표), 2) 입력 자료 5종 표, 3) Sub Agent 구성도와 역할 표, 4) 기존 흐름도(Agent 이름 표시)와 패턴 표, 답변 검토 기준 표, 5) 결과물 샘플 iframe으로 구성
- `scripts/embed_example.py` (신규) — `reference/example.html`을 HTML 이스케이프해 `<iframe id="example" srcdoc="...">`에 넣는 스크립트. 인자 없이 다시 실행 가능

### 결정 사항 / 이유
- 샘플은 iframe `srcdoc`에 포함: example.html의 CSS(`.card`, `.panel` 등)가 본 페이지 CSS와 충돌하지 않게 분리하려는 것. `<template>`+JS 방식은 `<html>/<head>` 태그가 사라져 버림
- 22KB 원문을 직접 이스케이프하면 실수가 생기기 쉬워 스크립트로 처리. 작업 규칙(결과물과 스크립트 함께 저장)에 따라 `scripts/`에 보관

### 현재 상태
- 완료: 페이지 작성, 샘플 포함(약 60KB), `embed_example.py` 2회 연속 실행으로 재실행 안전성 확인
- 검증: 브라우저로 열기만 했고 화면 렌더링(iframe 높이 자동 조정, 다크 모드)은 **미검증**
- git: 이 폴더(`내 업무/`)의 나머지 파일(`업무설계서.md`, `workflow.md`, `.claude/`, `data/`, `scripts/voc_tools.py` 등)은 아직 미추적 상태이며, 이번 커밋에는 위 2개 파일과 이 CHANGELOG만 포함

### 다음 할 일
1. 브라우저에서 `my-agent-team.html`을 열어 5번 샘플의 iframe 높이와 모바일 폭(약 375px) 레이아웃 확인
2. `reference/example.html`이 바뀌면 `내 업무/`에서 `python scripts/embed_example.py` 실행
3. `내 업무/` 나머지 파일의 커밋 여부를 사용자와 결정

### 주의 / 알려진 문제
- 이 PC에서 `python`은 Windows 스토어 바로가기로 연결돼 실행이 실패함 → `C:\Users\User\AppData\Local\Python\bin\python.exe`를 전체 경로로 지정해 실행
- `my-agent-team.html`의 `srcdoc` 속성을 직접 고치지 말 것. 스크립트를 실행하면 덮어써짐
- Workflow를 바꿀 때는 `workflow.md` → `my-agent-team.html` → 구현 순서로 함께 수정 (`CLAUDE.md` 규칙)

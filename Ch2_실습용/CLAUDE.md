# CLAUDE.md

다분야 뉴스를 수집해 대시보드(HTML)로 만드는 프로젝트입니다.

## 모델
- 메인(오케스트레이터)은 Opus로 실행합니다. 서브에이전트는 각 정의 파일의 `model`(sonnet)을 따릅니다.

## 서브에이전트 위임 규칙
1. 분야별 에이전트 6개를 동시에 병렬 호출합니다: news-politics-society, news-economy, news-it-ai, news-semiconductor, news-science-bio, news-world.
2. 모두 끝나면 `data/*.json`을 읽어 통합합니다. 실패하거나 비어 있는 분야는 대시보드에 "수집 실패"로 표시합니다.
3. `samples/dashboard-sample.html` 스타일을 참고해 `news-dashboard.html`(단일 standalone HTML)을 만듭니다. 분야별 탭 또는 카드, 헤드라인, 요약, 원문 링크를 포함합니다.

## 규칙
- 기사 내용과 링크는 수집 결과에 있는 것만 사용하고 지어내지 않습니다.
- 문서·응답은 한국어로 작성합니다.

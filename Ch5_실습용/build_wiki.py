"""documents/ 원문 → LLM Wiki(마크다운) + 그래프 시각화 HTML 생성.

구조 (Karpathy식 LLM Wiki)
  documents/        원문(raw source, 수정하지 않음)
  wiki/             원문을 컴파일한 위키 페이지 (SCHEMA.md, index.md, log.md + 유형별 폴더)
  wiki_graph.html   위키 전체를 보여주는 단독 실행 그래프 뷰어

사용: python build_wiki.py   (이 폴더에서 실행. 페이지 내용은 아래 NODES/EDGES를 고쳐 갱신)
"""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent
WIKI = ROOT / "wiki"
NAMES = [l.strip() for l in (ROOT / "masking-list.txt").read_text(encoding="utf-8").splitlines() if l.strip()]

TYPE_COLOR = {
    "설비": "#3b82f6", "공정": "#14b8a6", "사건": "#ef4444", "원인": "#f97316",
    "개념": "#eab308", "규정": "#a855f7", "불량": "#ec4899", "인물": "#22c55e",
    "문서": "#94a3b8", "점검": "#64748b",
}

# id: (유형, 제목, 한 줄 요약, 본문(markdown, [[id]]로 다른 페이지 링크), 출처 id 목록)
NODES = {}


def N(id_, type_, title, summary, body, src=()):
    NODES[id_] = dict(id=id_, type=type_, title=title, summary=summary, body=body.strip(), sources=list(src))


EDGES = []


def E(a, rel, b):
    EDGES.append((a, rel, b))


# ───────────── 문서 (원문 출처) ─────────────
N("s_man", "문서", "설비운영매뉴얼 통합본(MAN-EQ-000)", "전 공정 설비의 관리 파라미터·점검 순서·공통 유의사항 (개정일 2026-01-15)",
  """원본: `documents/설비운영매뉴얼_통합본.txt`

- 문서번호 MAN-EQ-000, 개정일 2026-01-15, 적용 범위 전 공정 8개 영역
- 제1~8장은 공정별(식각·박막증착·금속증착·연마·세정·포토·이온주입·검사) 관리 기준, 제9장은 전 공정 공통 유의사항
- 사건 3건의 재발방지 대책(압력 ±3%, 2회 이상 반복 시 보고, PM 승인 등)이 이 매뉴얼에 반영되어 있음
- 확인 필요: 개정일은 1월인데 본문에 "2026년 9월 개정" 문구가 있음 → [[x_check]]""")
N("s_q71", "문서", "QIR-2026-0071 ETCH-02 냉각수 온도상승", "ETCH-02 불량률 1~2%→6~9% 급등, 근본 원인은 냉각수 펌프 임펠러 마모 (2026-07-20)",
  """원본: `documents/이슈보고서_ETCH-02_냉각수온도상승_20260720.txt` · 작성 [[p_park]]

- 기간: 7/8~7/19, 불량률 1~2% → 6~9%, 유형 박막두께불량
- 7/7 야간 유량 센서 알람을 단순 노이즈로 판단해 미조치
- 근본 원인: 펌프 임펠러 마모 → 유량 저하 → 챔버 +4.5℃ → 식각 속도 산포
- 간접 원인: PM 주기 3→4개월 임의 연장
- 조치: 7/19 펌프 교체, 7/20 온도 정상(±0.5℃) 확인, 유량 알람 기준 상향, ETCH-01~03 펌프 점검 3개월로 환원""")
N("s_q83", "문서", "QIR-2026-0083 CVD-01 챔버압력 편차", "CVD-01 불량률 1%→5~8%, 압력 조절 밸브 씰 손상으로 파티클 발생 (2026-08-14)",
  """원본: `documents/이슈보고서_CVD-01_챔버압력편차_20260814.txt` · 작성 [[p_choi]]

- 기간: 8/4~8/13, 불량률 1% → 5~8%, 유형 파티클
- 8/3 정기 점검에서 압력 편차를 경미하다고 보고 공정 재개
- 근본 원인: 압력 조절 밸브 내부 씰 미세 손상 → 압력 ±8% 변동 → 부산물 내벽 축적 → 파티클
- 조치: 8/12 씰 교체 + 챔버 전체 클리닝, 8/13 ±1% 안정화, 8/15 CVD-02 예방 교체
- 재발 방지: 허용 오차 ±5→±3%, 2회 연속 초과 시 자동 정지 인터록 검토, 내벽 육안 점검 절차 신설""")
N("s_m0715", "문서", "품질회의록 2026-07-15 ETCH-02", "ETCH-02 불량 급증 공유, 냉각수 계통 정밀 점검(7/17까지) 결정",
  """원본: `documents/품질회의록_20260715_ETCH-02.txt` · 작성 [[p_lee]]

- 참석: [[p_lee]], [[p_park]], [[p_jung]], [[p_choi]]
- [[p_jung]]: 챔버 온도 상승 확인, 냉각 계통 이상 의심
- [[p_choi]]: 개선 없으면 7/18부터 가동 중단·긴급 점검 제안
- 결정: 냉각수 순환 계통 점검 7/17까지, 필요 시 펌프 교체, 결과는 7/20 이슈보고서로 공유
- 다음 회의 2026-07-21""")
N("s_m0818", "문서", "품질회의록 2026-08-18 CVD-01", "CVD-01 조치 결과 공유, 압력 허용오차 ±3% 강화 및 2회 반복 시 의무 보고 결정",
  """원본: `documents/품질회의록_20260818_CVD-01.txt` · 작성 [[p_lee]]

- 참석: [[p_lee]], [[p_choi]], [[p_jung]], [[p_kim]]
- [[p_kim]]: ETCH-02와 CVD-01 모두 "초기 이상 신호를 놓치는 패턴" → [[k_signal]]
- 결정: 압력 허용오차 ±5→±3% (9/1 적용), 경미한 이상 2회 이상 반복 시 설비기술팀 의무 보고
- 절차 개정 초안은 [[p_kim]]이 8/25까지 작성
- 다음 회의 2026-09-01""")
N("s_a0707", "문서", "메일 2026-07-07 ETCH-02 냉각수 유량알람", "야간조가 유량 경고 알람 3회를 보고, 설비기술팀은 센서 노이즈로 판단 (사건의 최초 징후)",
  """원본: `documents/mails/메일_20260707_ETCH-02_냉각수유량알람.txt`

- [[p_han]](L1 야간조) → [[p_jung]]: 22:14·22:51·23:27 `Cooling Water Flow Low`(Warning) 3회, 회당 20~40초, 온도 +0.3℃
- [[p_jung]] 답신(7/8): 자동 해제·온도 허용 범위 내 → 센서 노이즈 판단. 작년 커넥터 접촉 불량 이력, 물량 많아 정지 불가, 다음 PM 때 커넥터 점검
- 결과적으로 이 알람이 [[i_etch02]]의 첫 징후였음""")
N("s_a0811", "문서", "메일 2026-08-11 CVD-01 파티클 불량 급증", "생산기술팀이 CVD-01 정지·점검 요청, 설비기술팀이 씰 손상 확인·교체 회신",
  """원본: `documents/mails/메일_20260811_CVD-01_파티클불량급증.txt`

- [[p_choi]] → [[p_jung]], [[p_lee]] (참조 [[p_kim]], [[p_oh]]): 8/4부터 불량 5~8%, 압력 최대 ±8%, 5% 초과 일 3~5회
- 8/3 점검 때 이미 4%대 편차, 파티클 필터는 8/3 교체라 막힘 가능성 낮음
- 매뉴얼 2.1(2회 이상 초과 시 정지)·2.2(내벽 확인) 근거로 18시 이후 정지 요청
- [[p_jung]] 회신(8/12): 씰 손상 확인·교체, 내벽 클리닝, MFC 정상, ±1% 안정화. CVD-02는 8/15 예방 교체""")
N("s_a0925", "문서", "메일 2026-09-25 CLN-03 질소퍼지 유량저하", "개정 절차(2시간 내 현장 확인)를 적용해 불량 발생 전 필터 교체를 결정한 사례",
  """원본: `documents/mails/메일_20260925_CLN-03_질소퍼지유량저하.txt`

- [[p_han]](9월부터 L5) → [[p_jung]]: 01:05·02:40 `N2 Purge Flow Low` 2회, 최저 88%
- 현장 확인(02:50~03:10): 메인 압력 정상, 필터 차압 평소의 약 1.7배, 최종 교체 2026-06-10, 누설 없음
- [[p_jung]] 회신: "7월 건은 제 판단 실수", 당일 10시 정지 후 필터 교체, CLN-01·02는 차압 1.5배 이상이면 교체, [[p_kim]]이 1주일 불량 모니터링
- 상세: [[i_cln03]]""")

# ───────────── 공정 ─────────────
N("etch", "공정", "식각 공정(ETCH)", "L1 라인 · 온도 관리가 핵심, 불량은 박막두께·패턴",
  "- 대상 설비: ETCH-01~03 (L1)\n- 챔버 온도: 설정값 ±0.5℃ 이내 → [[r_temp]]\n- 온도 이상 시 점검 순서 → [[r_tempchk]]\n- 온도 이상은 식각 속도 산포를 일으켜 박막두께불량·패턴불량으로 이어짐 → [[k_temp]]", ["s_man"])
N("cvd", "공정", "박막증착 공정(CVD)", "L2 라인 · 챔버 압력·파티클 관리가 핵심",
  "- 대상 설비: CVD-01~03 (L2)\n- 압력 허용오차 ±3% → [[r_press]]\n- 파티클 필터 월 1회 교체 → [[r_pfilter]]\n- 압력 불안정은 내벽 부산물 축적을 가속해 파티클을 유발 → [[k_press]]", ["s_man"])
N("pvd", "공정", "금속증착 공정(PVD)", "L3 라인 · 스퍼터링 챔버 회전 베어링 진동 관리",
  "- 대상 설비: PVD-01, 02 (L3)\n- 진동 증가는 박막두께 불균일의 주요 원인 → [[r_pvd]]", ["s_man"])
N("cmp", "공정", "연마 공정(CMP)", "L4 라인 · 연마 패드/벨트 소모품과 연마압력 관리",
  "- 대상 설비: CMP-01, 02 (L4)\n- 연마압력 불균일은 스크래치의 주요 원인 → [[r_cmp]]", ["s_man"])
N("cln", "공정", "세정 공정(CLN)", "L5 라인 · 질소 퍼지 가스 유량·배관 관리",
  "- 대상 설비: CLN-01~03 (L5)\n- 질소 퍼지 필터 분기 점검, 유량 저하 시 즉시 교체 → [[r_n2]]\n- 유량 저하는 잔류물·표면오염 불량으로 이어짐", ["s_man"])
N("photo", "공정", "포토 공정(PHOTO)", "L6 라인 · 클린룸 습도와 렌즈 오염 관리",
  "- 대상 설비: PHOTO-01, 02 (L6)\n- 노광 챔버 습도 40~50% 유지 → [[r_photo]]", ["s_man"])
N("imp", "공정", "이온주입 공정(IMP)", "L7 라인 · 파워서플라이 전압 안정성(도즈 제어)",
  "- 대상 설비: IMP-01, 02 (L7)\n- 전압 불안정 → 도즈 편차 → 치수불량 → [[r_imp]]", ["s_man"])
N("insp", "공정", "검사 공정(INSP)", "L8 라인 · 비전 카메라 캘리브레이션 주기 관리",
  "- 대상 설비: INSP-01~03 (L8)\n- 월 1회 캘리브레이션, 이력은 품질시스템 등록 → [[r_insp]]\n- 주기 초과 시 측정 오차(오검출/미검출) 발생", ["s_man"])

# ───────────── 설비 ─────────────
N("etch02", "설비", "ETCH-02", "L1 식각 설비 · 2026-07 냉각수 유량 저하로 박막두께불량 급증",
  "- 라인 L1, 식각 공정\n- 이력: [[i_etch02]] (7/8~7/19 불량 6~9%)\n- 7/19 냉각수 순환 펌프 교체, 7/20 온도 정상 복귀", ["s_q71", "s_m0715", "s_a0707"])
N("cvd01", "설비", "CVD-01", "L2 박막증착 설비 · 2026-08 압력 편차로 파티클 불량 급증",
  "- 라인 L2, 박막증착 공정\n- 이력: [[i_cvd01]] (8/4~8/13 불량 5~8%)\n- 8/12 압력 조절 밸브 씰 교체, 챔버 클리닝", ["s_q83", "s_m0818", "s_a0811"])
N("cvd02", "설비", "CVD-02", "CVD-01과 같은 밸브 사용 · 8/15 예방 교체",
  "- CVD-01과 동일 압력 조절 밸브를 사용 → 8/15 부품 예방 교체\n- 교체 후 압력 트렌드 안정 유지(8/18 회의 기준)", ["s_q83", "s_m0818", "s_a0811"])
N("cln03", "설비", "CLN-03", "L5 세정 설비 · 2026-09 질소 퍼지 필터 막힘 초기에 선제 교체",
  "- 라인 L5, 세정 공정\n- 이력: [[i_cln03]]\n- 9/25 10시 정지 후 퍼지 라인 필터 교체, 불량률은 평소 수준(1% 미만) 유지", ["s_a0925"])
N("cln01", "설비", "CLN-01", "L5 세정 설비 · CLN-03 건 이후 퍼지 필터 차압 측정 대상",
  "- 차압이 평소의 1.5배 이상이면 필터 교체 (9/25 [[p_jung]] 지시)", ["s_a0925"])
N("cln02", "설비", "CLN-02", "L5 세정 설비 · CLN-03 건 이후 퍼지 필터 차압 측정 대상",
  "- 차압이 평소의 1.5배 이상이면 필터 교체 (9/25 [[p_jung]] 지시)", ["s_a0925"])

# ───────────── 불량 유형 ─────────────
for id_, t, body in [
    ("d_thk", "박막두께불량", "식각 속도 산포·스퍼터링 진동 등으로 두께가 기준을 벗어나는 불량. 주요 사례: [[i_etch02]]"),
    ("d_pat", "패턴불량", "식각 온도 이상, 포토 렌즈 오염 등으로 발생"),
    ("d_par", "파티클", "웨이퍼 표면 미세 입자. 필터 막힘·내벽 부산물 낙하가 원인. 주요 사례: [[i_cvd01]]"),
    ("d_sur", "표면오염", "증착 부산물·세정 유량 저하로 잔류물이 남는 불량. 관련: [[i_cln03]]"),
    ("d_scr", "미세스크래치", "연마압력 불균일·소모품 마모로 발생"),
    ("d_dim", "치수불량", "도즈 편차(이온주입)·비전 오검출/미검출(검사)로 발생"),
]:
    N(id_, "불량", t, body.split(". ")[0], "- " + body.replace(". ", "\n- "), ["s_man"])

# ───────────── 사건 ─────────────
N("i_etch02", "사건", "ETCH-02 냉각수 유량 저하 사건", "2026-07 · 유량 알람을 노이즈로 처리 → 챔버 +4.5℃ → 박막두께불량 6~9%",
  """### 타임라인
- 07-07 야간: 유량 경고 알람 3회 보고 ([[s_a0707]]) → 07-08 노이즈로 판단, 다음 PM 때 점검
- 07-08~07-19: 챔버 평균 +4.5℃, 불량률 1~2% → 6~9%
- 07-15: 품질회의, 7/17까지 냉각수 계통 점검 결정 ([[s_m0715]])
- 07-19: 냉각수 순환 펌프 교체, 07-20: 온도 정상(±0.5℃) 복귀 ([[s_q71]])

### 인과 사슬
PM 주기 연장 → 펌프 임펠러 마모 → 유량 저하 → 챔버 온도 상승 → 식각 속도 산포 → 박막두께불량

### 재발 방지
알람 2시간 내 현장 확인, PM 변경 승인제, 온도 대시보드 자동 알림""", ["s_q71", "s_m0715", "s_a0707"])
N("i_cvd01", "사건", "CVD-01 챔버압력 편차 사건", "2026-08 · 압력 편차를 경미로 간주 → 부산물 축적 → 파티클 5~8%",
  """### 타임라인
- 08-03: 정기 점검에서 압력 편차(4%대) 관찰, 당시 허용 ±5% 이내라 조치 없이 재개
- 08-04~08-13: 압력 최대 ±8%, 불량률 1% → 5~8%
- 08-11: 생산기술팀 정지·점검 요청 ([[s_a0811]])
- 08-12: 밸브 씰 교체·챔버 클리닝, 08-13 ±1% 안정화
- 08-15: CVD-02 동일 부품 예방 교체, 08-18: 품질회의에서 기준 강화 결정 ([[s_m0818]])

### 인과 사슬
밸브 씰 손상 → 압력 불안정 → 반응 부산물 내벽 축적 → 파티클 → 불량

### 재발 방지
허용오차 ±3%, 경미한 이상 2회 반복 시 의무 보고, 내벽 육안 점검 신설""", ["s_q83", "s_m0818", "s_a0811"])
N("i_cln03", "사건", "CLN-03 질소 퍼지 유량 저하 대응", "2026-09-25 · 개정 절차대로 2시간 내 확인, 불량 발생 전 필터 교체 (선제 대응 성공 사례)",
  """### 경과
- 01:05, 02:40 `N2 Purge Flow Low` 2회 (최저 88%)
- 02:50~03:10 현장 확인: 필터 차압 평소 대비 약 1.7배, 메인 압력·누설 정상
- 08:05 설비기술팀 회신, 10:00 CLN-03 정지 후 필터 교체
- CLN-01·02 차압 측정(1.5배 이상이면 교체), 교체 후 1주일 불량 모니터링

### 의미
7월 ETCH-02 건에서 알람 보고가 묵살된 경험이 있던 운영조가, 개정 절차를 근거로 선제 조치를 이끌어낸 사례. 9월 품질회의에서 공유 예정.""", ["s_a0925"])

# ───────────── 원인 ─────────────
N("c_pump", "원인", "냉각수 순환 펌프 임펠러 마모", "ETCH-02 유량 저하의 근본 원인",
  "- 임펠러 마모 → 냉각수 유량 저하 → 챔버 온도 제어 실패\n- 7/19 펌프 교체로 해소", ["s_q71"])
N("c_seal", "원인", "압력 조절 밸브 씰 손상", "CVD-01 압력 불안정의 근본 원인",
  "- 밸브 내부 씰의 미세 손상으로 압력 제어가 불안정해짐\n- 동일 밸브를 쓰는 CVD-02도 예방 교체", ["s_q83", "s_a0811"])
N("c_filter", "원인", "퍼지 라인 필터 막힘", "CLN-03 질소 유량 저하의 원인(초기 단계)",
  "- 필터 차압 약 1.7배 상승, 최종 교체 2026-06-10\n- 분기 점검 대상이지만 유량 저하 시 즉시 교체 → [[r_n2]]", ["s_a0925"])
N("c_pm", "원인", "PM 주기 임의 연장(3→4개월)", "ETCH-02 사건의 간접 원인",
  "- 예방정비 주기가 승인 없이 3개월에서 4개월로 연장되어 있었음\n- 대응: 식각 설비 펌프 점검 3개월로 환원, 변경 승인제 도입 → [[r_pm]]", ["s_q71"])
N("c_deposit", "원인", "반응 부산물 챔버 내벽 축적", "압력 불안정이 파티클로 이어지는 매개 원인",
  "- 압력 불안정 상태의 증착이 부산물 축적을 가속, 이후 파티클 형태로 낙하\n- 8/12 챔버 전체 클리닝, 내벽 육안 점검 절차 신설", ["s_q83", "s_man"])

# ───────────── 개념 ─────────────
N("k_signal", "개념", "초기 이상신호 간과 패턴", "세 건 모두 '경미해 보이는 초기 신호'가 분기점 — 놓치면 불량 확산, 잡으면 선제 대응",
  """**공통 패턴**: 경미한 신호는 정상 범위·노이즈로 해석되기 쉽다.

- ETCH-02: 7/7 유량 알람 3회 → "센서 노이즈"로 처리 → 불량 6~9% ([[i_etch02]])
- CVD-01: 8/3 압력 4%대 편차 → "허용 범위 내" → 불량 5~8% ([[i_cvd01]])
- CLN-03: 9/25 유량 알람 2회 → 2시간 내 확인·선제 교체 → 불량 없음 ([[i_cln03]])

**조직의 대응**: 2회 이상 반복 시 의무 보고 ([[r_report2]]), 경고 알람 2시간 내 확인 ([[r_alarm2h]]), 허용오차 강화 ([[r_press]]).
제안자: [[p_kim]] (8/18 회의).""", ["s_m0818", "s_a0811", "s_a0925"])
N("k_temp", "개념", "챔버 온도와 식각 속도 산포", "챔버 온도 상승 → 식각 속도 불균일 → 박막 두께 편차",
  "- 설정값 대비 ±0.5℃ 이내가 정상 ([[r_temp]])\n- ETCH-02는 평균 +4.5℃ 상승 상태로 가동 → 동일 LOT 내 두께 편차 확대\n- 사례: [[i_etch02]]", ["s_man", "s_q71", "s_m0715"])
N("k_press", "개념", "압력 불안정과 파티클 인과 사슬", "압력 편차 → 부산물 내벽 축적 → 파티클 → 표면 오염",
  "- 압력 불안정은 반응 부산물의 내벽 축적을 가속 → 파티클성 불량\n- 압력 편차 발생 시 내벽 오염 상태도 함께 확인 (매뉴얼 2.2)\n- 사례: [[i_cvd01]]", ["s_man", "s_q83"])

# ───────────── 규정 / 절차 ─────────────
N("r_temp", "규정", "챔버 온도 관리 기준", "설정값 ±0.5℃ 정상, ±1℃ 초과 10분 지속 시 1차 경고, 존간 편차 1.5℃ 초과 시 히터 점검",
  "- 정상: 설정값 대비 ±0.5℃ 이내\n- ±1℃ 초과 10분 이상 → 1차 경고 알람\n- 히터 존간 편차 1.5℃ 초과 → 히터 컨트롤러 점검\n- 매뉴얼 1.1", ["s_man"])
N("r_tempchk", "규정", "온도 이상 점검 순서", "냉각수 유량 → 순환 펌프 → 센서 캘리브레이션 이력 → 히터 존별 출력",
  "1. 냉각수 유량 확인\n2. 냉각수 순환 펌프 상태 확인\n3. 온도 센서 캘리브레이션 이력 확인\n4. 히터 컨트롤러 존별 출력 편차 확인\n\n- 매뉴얼 1.2. ETCH-02 사건의 실제 원인이 1~2번에 해당 ([[i_etch02]])", ["s_man"])
N("r_press", "규정", "챔버 압력 관리 기준(±3%)", "허용오차 ±3% (9/1 적용, 기존 ±5%), 2회 이상 초과 시 일시 정지 후 밸브·MFC 점검",
  "- 허용오차 설정값 ±3% (2026-09-01부터, 기존 ±5%)\n- 2회 이상 반복 초과 시 설비 일시 정지, 압력 조절 밸브·MFC 점검\n- 2회 연속 초과 시 자동 정지 인터록은 '검토' 단계 ([[s_q83]])\n- 8/3의 4%대 편차는 구 기준(±5%)에서는 정상, 신 기준(±3%)에서는 초과 → 조기 발견 가능했을 것으로 추정", ["s_man", "s_m0818", "s_q83"])
N("r_pfilter", "규정", "파티클 필터 월 1회 교체", "필터 막힘은 파티클 불량의 주요 원인",
  "- 권장 교체 주기(월 1회) 준수\n- CVD-01은 8/3에 교체해 필터 원인 가능성은 낮다고 판단 ([[s_a0811]])", ["s_man", "s_a0811"])
N("r_report2", "규정", "경미한 이상 2회 반복 시 의무 보고", "온도·압력·진동·유량의 경미한 이상도 2회 이상이면 설비기술팀에 즉시 보고",
  "- 2026-08-18 품질회의에서 결정, 초안 작성 [[p_kim]] (8/25까지)\n- 매뉴얼 제9장에 반영\n- 목적: [[k_signal]] 차단", ["s_m0818", "s_man"])
N("r_alarm2h", "규정", "경고 알람 2시간 내 현장 확인", "냉각수 유량 등 경고 알람 발생 시 2시간 이내 현장 확인 의무",
  "- QIR-2026-0071의 재발 방지 대책으로 도입\n- 첫 적용 성공 사례: [[i_cln03]] (알람 01:05 → 현장 확인 02:50)", ["s_q71", "s_a0925"])
N("r_pm", "규정", "PM 주기 변경 승인제", "예방정비 주기는 설비기술팀 승인 없이 변경 불가",
  "- ETCH-02 사건의 간접 원인(3→4개월 임의 연장)에 대한 대책\n- 매뉴얼 제9장", ["s_q71", "s_man"])
N("r_dash", "규정", "온도 트렌드 대시보드 자동 알림", "임계값 초과 시 자동 알림이 발생하도록 설정 유지",
  "- 챔버 온도 트렌드 대시보드에 임계값 초과 자동 알림 추가 (QIR-2026-0071)\n- 매뉴얼 제9장: 주요 공정 파라미터 모니터링 대시보드 설정 유지", ["s_q71", "s_man"])
N("r_qir", "규정", "QIR 작성·품질시스템 등록", "조치 및 원인 분석 결과는 QIR 양식으로 작성해 등록",
  "- 매뉴얼 제9장\n- 사례: QIR-2026-0071, QIR-2026-0083", ["s_man"])
N("r_n2", "규정", "질소 퍼지 필터 분기 점검", "분기별 점검, 유량 저하 시 즉시 교체",
  "- 매뉴얼 5.1\n- CLN-03은 6/10 교체 후 약 3.5개월 만에 차압 1.7배 → 선제 교체 ([[i_cln03]])", ["s_man", "s_a0925"])
N("r_pvd", "규정", "PVD 회전 베어링 분기 진동 측정", "기준치 초과 시 베어링 교체 검토", "- 매뉴얼 3.1\n- 진동 증가는 박막두께 불균일의 원인", ["s_man"])
N("r_cmp", "규정", "CMP 소모품 교체주기 준수", "연마벨트/패드 장력·마모 점검, 연마압력 불균일은 스크래치 원인", "- 매뉴얼 4.1", ["s_man"])
N("r_photo", "규정", "노광 챔버 습도 40~50% 유지", "국소 습도 상승 시 렌즈 오염 점검, 정기 클리닝", "- 매뉴얼 6.1\n- 렌즈 오염은 패턴불량의 주요 원인", ["s_man"])
N("r_imp", "규정", "파워서플라이 전압 점검", "정기 점검, 노후 장비는 예방 교체 검토", "- 매뉴얼 7.1\n- 전압 불안정 → 도즈 편차 → 치수불량", ["s_man"])
N("r_insp", "규정", "비전 카메라 월 1회 캘리브레이션", "이력은 품질시스템 등록, 주기 초과 시 자동 경고", "- 매뉴얼 8.1", ["s_man"])

# ───────────── 인물 ─────────────
N("p_han", "인물", "한승우", "L1 야간 운영조 → 2026-09부터 L5 운영조 · 두 알람 보고의 발신자",
  "- 7/7 ETCH-02 유량 알람 보고 ([[s_a0707]]) — 노이즈로 처리됨\n- 9/25 CLN-03 알람 확인·선제 교체 요청 ([[s_a0925]]) — 7월 경험이 대응에 영향", ["s_a0707", "s_a0925"])
N("p_jung", "인물", "정민재", "설비기술팀 · 세 사건 모두의 조치 책임자",
  "- 회의록에는 '팀장', 메일 서명 직함은 '책임님'으로 호칭됨\n- 7/8 알람을 노이즈로 판단, 9/25 \"7월 건은 제 판단 실수\"라고 인정\n- 7/17까지 냉각수 계통 점검, 8/12 CVD-01 씰 교체 주도", ["s_m0715", "s_m0818", "s_a0707", "s_a0811", "s_a0925"])
N("p_choi", "인물", "최유진", "생산기술팀 · QIR-2026-0083 작성자, 8/11 정지 요청",
  "- 7/15 회의에서 가동 중단안 제안, 8/11 CVD-01 긴급 정지·점검 요청\n- 메일에서 \"ETCH-02 건처럼 초기 신호를 놓친 사례 반복\"을 우려", ["s_q83", "s_m0715", "s_m0818", "s_a0811"])
N("p_lee", "인물", "이서연", "품질팀장 · 품질회의 주관·회의록 작성",
  "- 2026-07-15, 08-18 회의록 작성\n- 8/11 긴급 메일 수신", ["s_m0715", "s_m0818", "s_a0811"])
N("p_park", "인물", "박지훈", "공정품질팀 · QIR-2026-0071 작성자",
  "- 동일 LOT 내 두께 편차가 온도 불균일에 의한 식각 속도 산포와 일치함을 지적(7/15)", ["s_q71", "s_m0715"])
N("p_kim", "인물", "김도윤", "공정품질팀 · '초기 이상신호 간과 패턴' 제기, 절차 개정 초안 담당",
  "- 8/18 회의에서 ETCH-02·CVD-01의 공통 패턴 지적 → [[k_signal]]\n- 절차 개정 초안 8/25까지 작성 → [[r_report2]]\n- 9/25 이후 CLN-03 불량률 1주일 모니터링 담당", ["s_m0818", "s_a0925", "s_a0811"])
N("p_oh", "인물", "오세훈", "L2 설비운영 · 8/11 메일 참조자 (다른 문서에서의 활동 없음)",
  "- [[s_a0811]] 참조 수신자로만 등장", ["s_a0811"])

# ───────────── 점검 (lint) ─────────────
N("x_check", "점검", "확인 필요·모순 사항", "문서 간 불일치, 빠진 경위, 추정에 의존한 서술 목록",
  """위키를 만들며 발견한 항목 (원문은 수정하지 않았고 확인이 필요한 것만 기록)

1. **매뉴얼 개정일**: 개정일 2026-01-15인데 2.1항에 "2026년 9월 개정(±5%→±3%)" 문구 → 개정일 갱신 누락 가능 ([[s_man]])
2. **ETCH-02 점검 vs 교체 시점**: 7/15 회의는 점검 7/17 완료로 결정, 실제 펌프 교체는 7/19 → 7/17~19 경위가 문서에 없음 ([[s_m0715]], [[s_q71]])
3. **8/3 압력 편차**: QIR은 '미세', 메일은 '4%대'. 구 기준 ±5% 이내라 정상 처리 → 신 기준(±3%)이면 초과 (위키 서술은 추정) ([[r_press]])
4. **정민재 직함**: 회의록 '팀장' / 메일 '책임' — 호칭 차이로 보이나 확인 불가 ([[p_jung]])
5. **자동 정지 인터록**: QIR-0083은 '추가 검토', 9월 이후 도입 여부 문서에 없음 ([[r_press]])
6. **9/1 회의 결과**: 8/18 회의의 다음 회의(9/1) 결과 문서 없음""", ["s_man", "s_m0715", "s_q71"])

# ───────────── 관계 ─────────────
for p, eq in [("etch", ["etch02"]), ("cvd", ["cvd01", "cvd02"]), ("cln", ["cln03", "cln01", "cln02"])]:
    for e in eq:
        E(e, "속함", p)
for p, ds in [("etch", ["d_thk", "d_pat"]), ("cvd", ["d_par", "d_sur"]), ("pvd", ["d_thk"]), ("cmp", ["d_scr"]),
              ("cln", ["d_sur"]), ("photo", ["d_pat"]), ("imp", ["d_dim"]), ("insp", ["d_dim"])]:
    for d in ds:
        E(p, "관련 불량", d)
for p, r in [("etch", "r_temp"), ("etch", "r_tempchk"), ("cvd", "r_press"), ("cvd", "r_pfilter"), ("cln", "r_n2"),
             ("pvd", "r_pvd"), ("cmp", "r_cmp"), ("photo", "r_photo"), ("imp", "r_imp"), ("insp", "r_insp")]:
    E(p, "관리 기준", r)
for p in ["etch", "cvd", "pvd", "cmp", "cln", "photo", "imp", "insp"]:
    E("s_man", "다룸", p)
for r in ["r_report2", "r_pm", "r_dash", "r_qir"]:
    E("s_man", "공통 유의사항", r)

# 사건
E("i_etch02", "발생 설비", "etch02"); E("i_cvd01", "발생 설비", "cvd01"); E("i_cln03", "대응 설비", "cln03")
E("i_cvd01", "예방 교체", "cvd02"); E("i_cln03", "차압 점검", "cln01"); E("i_cln03", "차압 점검", "cln02")
E("i_etch02", "결과 불량", "d_thk"); E("i_cvd01", "결과 불량", "d_par"); E("i_cvd01", "부수 불량", "d_sur")
E("i_cln03", "방지한 불량", "d_sur")
E("i_etch02", "근본 원인", "c_pump"); E("i_etch02", "간접 원인", "c_pm")
E("i_cvd01", "근본 원인", "c_seal"); E("i_cvd01", "매개 원인", "c_deposit"); E("i_cln03", "원인", "c_filter")
E("i_etch02", "징후 간과", "k_signal"); E("i_cvd01", "징후 간과", "k_signal"); E("i_cln03", "징후 포착(개선 사례)", "k_signal")
E("i_etch02", "설명 개념", "k_temp"); E("i_cvd01", "설명 개념", "k_press")
E("k_temp", "적용 기준", "r_temp"); E("k_press", "적용 기준", "r_press"); E("c_deposit", "연결", "k_press")
E("i_etch02", "재발 방지", "r_alarm2h"); E("i_etch02", "재발 방지", "r_pm"); E("i_etch02", "재발 방지", "r_dash")
E("i_cvd01", "재발 방지", "r_press"); E("i_cvd01", "재발 방지", "r_report2")
E("i_cln03", "첫 적용", "r_alarm2h"); E("i_cln03", "적용 규정", "r_n2")
E("k_signal", "대응 규정", "r_report2"); E("c_pm", "대응 규정", "r_pm")
E("c_filter", "관련 규정", "r_n2")
E("c_pump", "점검 순서 해당", "r_tempchk")
E("r_press", "관련 필터 기준", "r_pfilter")

# 문서 ↔ 사건/규정
for s, i in [("s_q71", "i_etch02"), ("s_m0715", "i_etch02"), ("s_a0707", "i_etch02"),
             ("s_q83", "i_cvd01"), ("s_m0818", "i_cvd01"), ("s_a0811", "i_cvd01"), ("s_a0925", "i_cln03")]:
    E(s, "기록", i)
E("s_m0818", "결정", "r_press"); E("s_m0818", "결정", "r_report2"); E("s_m0818", "제기", "k_signal")
E("s_q71", "수립", "r_alarm2h"); E("s_q71", "수립", "r_pm"); E("s_q71", "수립", "r_dash")
E("s_q83", "수립", "r_press")
E("s_a0925", "적용 사례", "r_alarm2h")
E("s_man", "점검 대상", "x_check")

# 인물
for p, d, rel in [
    ("p_park", "s_q71", "작성"), ("p_choi", "s_q83", "작성"),
    ("p_lee", "s_m0715", "작성"), ("p_lee", "s_m0818", "작성"),
    ("p_park", "s_m0715", "참석"), ("p_jung", "s_m0715", "참석"), ("p_choi", "s_m0715", "참석"),
    ("p_choi", "s_m0818", "참석"), ("p_jung", "s_m0818", "참석"), ("p_kim", "s_m0818", "참석"),
    ("p_han", "s_a0707", "발신"), ("p_jung", "s_a0707", "수신·답신"),
    ("p_choi", "s_a0811", "발신"), ("p_jung", "s_a0811", "수신·답신"), ("p_lee", "s_a0811", "수신"),
    ("p_kim", "s_a0811", "참조"), ("p_oh", "s_a0811", "참조"),
    ("p_han", "s_a0925", "발신"), ("p_jung", "s_a0925", "수신·답신"), ("p_kim", "s_a0925", "참조"),
]:
    E(p, rel, d)
E("p_kim", "초안 담당", "r_report2"); E("p_kim", "제안", "k_signal")


# ───────────── 빌드 ─────────────
def resolve(text):
    def sub(m):
        key = m.group(1)
        if key not in NODES:
            raise KeyError(f"unknown link id: {key}")
        return f"[[{NODES[key]['title']}]]"
    return re.sub(r"\[\[([^\]]+)\]\]", sub, text)


for n in NODES.values():
    n["body"] = resolve(n["body"])
for a, _, b in EDGES:
    assert a in NODES and b in NODES, (a, b)
assert len({n["title"] for n in NODES.values()}) == len(NODES), "duplicate titles"

edges = [dict(s=a, t=b, rel=r) for a, r, b in EDGES]
out_ = {i: [] for i in NODES}
in_ = {i: [] for i in NODES}
for e in edges:
    out_[e["s"]].append(e)
    in_[e["t"]].append(e)


def write_wiki():
    if WIKI.exists():
        for p in WIKI.rglob("*.md"):
            p.unlink()
    for n in NODES.values():
        d = WIKI / n["type"]
        d.mkdir(parents=True, exist_ok=True)
        lines = ["---", f"type: {n['type']}", f"summary: {n['summary']}"]
        if n["sources"]:
            lines.append("sources: [" + ", ".join(NODES[s]["title"] for s in n["sources"]) + "]")
        lines += ["---", f"# {n['title']}", "", n["body"], ""]
        if out_[n["id"]]:
            lines += ["## 관계"] + [f"- {e['rel']} → [[{NODES[e['t']]['title']}]]" for e in out_[n["id"]]] + [""]
        if in_[n["id"]]:
            lines += ["## 역링크"] + [f"- [[{NODES[e['s']]['title']}]] — {e['rel']}" for e in in_[n["id"]]] + [""]
        (d / f"{n['title']}.md").write_text("\n".join(lines), encoding="utf-8")

    by_type = {}
    for n in NODES.values():
        by_type.setdefault(n["type"], []).append(n)
    idx = ["# 위키 인덱스", "", f"페이지 {len(NODES)}개 · 관계 {len(edges)}개 · 원문 {len(by_type['문서'])}건", ""]
    for t in TYPE_COLOR:
        if t in by_type:
            idx += [f"## {t} ({len(by_type[t])})"] + [f"- [[{n['title']}]] — {n['summary']}" for n in by_type[t]] + [""]
    (WIKI / "index.md").write_text("\n".join(idx), encoding="utf-8")

    (WIKI / "SCHEMA.md").write_text("""# 위키 규칙 (Schema)

이 위키는 `documents/`의 원문을 LLM이 읽고 컴파일한 것이다. 원문은 수정하지 않는다.

## 구조
- `wiki/<유형>/<제목>.md` — 유형: 설비·공정·사건·원인·개념·규정·불량·인물·문서·점검
- `index.md` 전체 목록, `log.md` 작업 이력, `SCHEMA.md` 이 규칙
- 페이지 간 링크는 `[[제목]]`, 관계는 페이지 하단 `## 관계`(나가는 링크)·`## 역링크`(들어오는 링크)

## 작성 원칙
1. 원문에 있는 사실만 쓴다. 추정은 '추정'이라고 명시한다.
2. 사건 페이지에는 타임라인과 인과 사슬을 적는다.
3. 모든 페이지는 `sources`로 근거 문서를 가리킨다.
4. 문서 간 불일치는 `점검/확인 필요·모순 사항`에 모은다.

## 갱신 절차
- Ingest: 새 원문 → 관련 페이지 갱신 + 새 페이지 생성 + `log.md` 기록
- Query: 질문은 index → 관련 페이지 순으로 읽어 답한다
- Lint: 모순·고아 페이지·출처 없는 서술 점검

## 재생성
`python build_wiki.py` (페이지 내용은 스크립트의 NODES/EDGES에서 관리)
""", encoding="utf-8")

    log = ["# 작업 로그", "", "## 2026-10-27 · 최초 구축 (ingest)"]
    for s in by_type["문서"]:
        log.append(f"- ingest: [[{s['title']}]]")
    log += [f"- 생성 페이지 {len(NODES)}개, 관계 {len(edges)}개", "- lint: [[확인 필요·모순 사항]] 6건 기록", ""]
    (WIKI / "log.md").write_text("\n".join(log), encoding="utf-8")


HTML = r"""<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>설비 품질 지식 위키 그래프</title>
<style>
:root{--bg:#f7f8fa;--panel:#fff;--fg:#1f2937;--mute:#6b7280;--line:#d6dae1;--edge:#b6bdc9;--accent:#2563eb}
@media (prefers-color-scheme:dark){:root{--bg:#0f1115;--panel:#181b21;--fg:#e5e7eb;--mute:#9aa3b2;--line:#2a2f39;--edge:#475063;--accent:#60a5fa}}
*{box-sizing:border-box}
body{margin:0;font:14px/1.55 system-ui,"Malgun Gothic",sans-serif;background:var(--bg);color:var(--fg);height:100vh;display:grid;grid-template-columns:1fr 400px;grid-template-rows:auto 1fr}
header{grid-column:1/3;display:flex;gap:12px;align-items:center;flex-wrap:wrap;padding:10px 16px;background:var(--panel);border-bottom:1px solid var(--line)}
header h1{font-size:16px;margin:0 8px 0 0}
header input[type=search]{padding:6px 10px;border:1px solid var(--line);border-radius:8px;background:var(--bg);color:var(--fg);width:200px}
.chips{display:flex;gap:6px;flex-wrap:wrap}
.chip{display:inline-flex;align-items:center;gap:5px;padding:2px 9px;border:1px solid var(--line);border-radius:999px;cursor:pointer;user-select:none;font-size:12px}
.chip i{width:9px;height:9px;border-radius:50%;display:inline-block}
.chip.off{opacity:.35}
label.mask{font-size:12px;color:var(--mute);display:flex;gap:4px;align-items:center;cursor:pointer}
#stage{position:relative;overflow:hidden}
svg{width:100%;height:100%;display:block;cursor:grab}
svg.pan{cursor:grabbing}
.edge{stroke:var(--edge);stroke-width:1.2;fill:none}
.edge.hl{stroke:var(--accent);stroke-width:2}
.elabel{font-size:10px;fill:var(--accent);text-anchor:middle;paint-order:stroke;stroke:var(--bg);stroke-width:3px}
.node{cursor:pointer}
.node circle{stroke:var(--bg);stroke-width:2}
.node.sel circle{stroke:var(--fg);stroke-width:3}
.node text{font-size:11px;fill:var(--fg);text-anchor:middle;paint-order:stroke;stroke:var(--bg);stroke-width:3px;pointer-events:none}
.dim{opacity:.12}
aside{background:var(--panel);border-left:1px solid var(--line);overflow:auto;padding:16px 18px}
aside h2{margin:2px 0 4px;font-size:18px}
aside h3{font-size:13px;margin:16px 0 4px;color:var(--mute);text-transform:none}
aside ul{margin:4px 0;padding-left:18px}
aside .sum{color:var(--mute);margin:0 0 8px}
.badge{display:inline-block;font-size:11px;padding:1px 8px;border-radius:999px;color:#fff;margin-bottom:6px}
a.wl{color:var(--accent);cursor:pointer;text-decoration:none;border-bottom:1px dotted var(--accent)}
code{background:var(--bg);padding:1px 5px;border-radius:4px;font-size:12px}
.rel{color:var(--mute);font-size:12px}
.hint{color:var(--mute);font-size:12px}
.stats{position:absolute;left:12px;bottom:10px;font-size:12px;color:var(--mute);background:var(--panel);padding:3px 9px;border-radius:8px;border:1px solid var(--line)}
@media (max-width:820px){body{grid-template-columns:1fr;grid-template-rows:auto 55vh 1fr;height:auto;min-height:100vh}header{grid-column:1}aside{border-left:0;border-top:1px solid var(--line)}}
</style>
</head>
<body>
<header>
  <h1>설비 품질 지식 위키</h1>
  <input type="search" id="q" placeholder="검색 (Enter: 이동)">
  <div class="chips" id="chips"></div>
  <label class="mask"><input type="checkbox" id="mask"> 이름 마스킹</label>
</header>
<div id="stage">
  <svg id="svg"><g id="view"><g id="edges"></g><g id="elabels"></g><g id="nodes"></g></g></svg>
  <div class="stats" id="stats"></div>
</div>
<aside id="panel"></aside>
<script>
const DATA = __DATA__;
const NS = "http://www.w3.org/2000/svg";
const $ = id => document.getElementById(id);
const byId = Object.fromEntries(DATA.nodes.map(n => [n.id, n]));
const byTitle = Object.fromEntries(DATA.nodes.map(n => [n.title, n]));
const state = {sel: null, hidden: new Set(), mask: false, q: ""};
const deg = {};
DATA.edges.forEach(e => { deg[e.s] = (deg[e.s]||0)+1; deg[e.t] = (deg[e.t]||0)+1; });
const out = {}, inn = {};
DATA.edges.forEach(e => { (out[e.s] ||= []).push(e); (inn[e.t] ||= []).push(e); });

const M = s => { if (!state.mask) return s; DATA.names.forEach(n => { s = s.split(n).join(n[0] + "○○"); }); return s; };
const esc = s => s.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;");
const inline = s => esc(s).replace(/`([^`]+)`/g,"<code>$1</code>").replace(/\*\*([^*]+)\*\*/g,"<b>$1</b>");
function rich(text){
  return text.split(/\[\[(.+?)\]\]/).map((p,i) => {
    if (i % 2) { const n = byTitle[p]; return n ? `<a class="wl" data-id="${n.id}">${esc(M(p))}</a>` : esc(M(p)); }
    return inline(M(p));
  }).join("");
}
function md(body){
  const out = []; let list = false;
  for (const raw of body.split("\n")) {
    const line = raw.trimEnd();
    const li = line.match(/^(?:- |\d+\. )(.*)/);
    if (li) { if (!list) { out.push("<ul>"); list = true; } out.push("<li>" + rich(li[1]) + "</li>"); continue; }
    if (list) { out.push("</ul>"); list = false; }
    if (line.startsWith("### ")) out.push("<h3>" + rich(line.slice(4)) + "</h3>");
    else if (line.trim()) out.push("<p>" + rich(line) + "</p>");
  }
  if (list) out.push("</ul>");
  return out.join("");
}

/* ── 그래프 ── */
const svg = $("svg"), view = $("view");
let W = 800, H = 600, tf = {x: 0, y: 0, k: 1};
const nodes = DATA.nodes.map((n, i) => {
  const a = i / DATA.nodes.length * Math.PI * 2;
  return Object.assign(n, {x: 400 + Math.cos(a)*260 + (Math.random()-.5)*40, y: 300 + Math.sin(a)*200 + (Math.random()-.5)*40, vx: 0, vy: 0, r: 7 + Math.min(14, (deg[n.id]||0) * 1.1)});
});
const mk = (tag, attrs, parent) => { const el = document.createElementNS(NS, tag); for (const k in attrs) el.setAttribute(k, attrs[k]); parent && parent.appendChild(el); return el; };
const edgeEls = DATA.edges.map(e => { const el = mk("line", {class:"edge"}, $("edges")); return {e, el}; });
const labelEls = DATA.edges.map(e => { const el = mk("text", {class:"elabel", display:"none"}, $("elabels")); el.textContent = e.rel; return el; });
const nodeEls = nodes.map(n => {
  const g = mk("g", {class:"node"}, $("nodes"));
  mk("circle", {r:n.r, fill:DATA.colors[n.type]}, g);
  const t = mk("text", {y:n.r + 12}, g); t.textContent = n.title;
  g.addEventListener("pointerdown", ev => startDrag(ev, n));
  g.addEventListener("mouseenter", () => hover(n));
  g.addEventListener("mouseleave", () => hover(null));
  return g;
});
function fit(){
  const r = $("stage").getBoundingClientRect(); W = r.width; H = r.height;
}
fit(); addEventListener("resize", () => { fit(); apply(); });

let alpha = 1;
function step(){
  const vis = nodes.filter(n => !state.hidden.has(n.type));
  const cx = W/2, cy = H/2;
  for (let i = 0; i < vis.length; i++) for (let j = i+1; j < vis.length; j++) {
    const a = vis[i], b = vis[j]; let dx = a.x-b.x, dy = a.y-b.y, d2 = Math.max(dx*dx+dy*dy, 1600);
    if (d2 > 360000) continue;
    const f = 16000 / d2 * alpha, d = Math.sqrt(d2); dx /= d; dy /= d;
    a.vx += dx*f; a.vy += dy*f; b.vx -= dx*f; b.vy -= dy*f;
  }
  DATA.edges.forEach(e => {
    const a = byId[e.s], b = byId[e.t];
    if (state.hidden.has(a.type) || state.hidden.has(b.type)) return;
    const dx = b.x-a.x, dy = b.y-a.y, d = Math.sqrt(dx*dx+dy*dy)+.01, f = (d-150)*.03*alpha;
    a.vx += dx/d*f; a.vy += dy/d*f; b.vx -= dx/d*f; b.vy -= dy/d*f;
  });
  vis.forEach(n => {
    n.vx += (cx-n.x)*.0025*alpha; n.vy += (cy-n.y)*.0025*alpha;
    const sp = Math.hypot(n.vx, n.vy); if (sp > 25) { n.vx *= 25/sp; n.vy *= 25/sp; }
    if (!n.fixed) { n.x += n.vx; n.y += n.vy; }
    n.vx *= .6; n.vy *= .6;
  });
  alpha = Math.max(alpha*.985, .02);
}
function fitView(){
  const vis = nodes.filter(n => !state.hidden.has(n.type));
  const x0 = Math.min(...vis.map(n => n.x)), x1 = Math.max(...vis.map(n => n.x));
  const y0 = Math.min(...vis.map(n => n.y)), y1 = Math.max(...vis.map(n => n.y));
  const k = Math.min(1.2, (W-120)/Math.max(x1-x0, 1), (H-100)/Math.max(y1-y0, 1));
  tf = {k, x: W/2 - (x0+x1)/2*k, y: H/2 - (y0+y1)/2*k};
}
function tick(){ step(); draw(); requestAnimationFrame(tick); }
function draw(){
  view.setAttribute("transform", `translate(${tf.x} ${tf.y}) scale(${tf.k})`);
  nodes.forEach((n, i) => nodeEls[i].setAttribute("transform", `translate(${n.x} ${n.y})`));
  edgeEls.forEach(({e, el}, i) => {
    const a = byId[e.s], b = byId[e.t];
    el.setAttribute("x1", a.x); el.setAttribute("y1", a.y); el.setAttribute("x2", b.x); el.setAttribute("y2", b.y);
    labelEls[i].setAttribute("x", (a.x+b.x)/2); labelEls[i].setAttribute("y", (a.y+b.y)/2);
  });
}

/* ── 상호작용 ── */
let drag = null, pan = null, moved = false;
function toWorld(ev){ const r = svg.getBoundingClientRect(); return {x:(ev.clientX-r.left-tf.x)/tf.k, y:(ev.clientY-r.top-tf.y)/tf.k}; }
function startDrag(ev, n){ ev.stopPropagation(); drag = n; moved = false; n.fixed = true; svg.setPointerCapture(ev.pointerId); alpha = Math.max(alpha, .4); }
svg.addEventListener("pointerdown", ev => { if (drag) return; pan = {sx: ev.clientX, sy: ev.clientY, x: tf.x, y: tf.y}; svg.classList.add("pan"); svg.setPointerCapture(ev.pointerId); moved = false; });
svg.addEventListener("pointermove", ev => {
  if (drag) { const p = toWorld(ev); drag.x = p.x; drag.y = p.y; moved = true; alpha = Math.max(alpha, .3); }
  else if (pan) { tf.x = pan.x + ev.clientX - pan.sx; tf.y = pan.y + ev.clientY - pan.sy; moved = true; }
});
svg.addEventListener("pointerup", () => {
  if (drag) { if (!moved) select(drag.id); drag.fixed = false; drag = null; }
  else if (pan) { if (!moved) select(null); pan = null; svg.classList.remove("pan"); }
});
svg.addEventListener("wheel", ev => {
  ev.preventDefault();
  const r = svg.getBoundingClientRect(), mx = ev.clientX-r.left, my = ev.clientY-r.top;
  const k = Math.min(3, Math.max(.3, tf.k * (ev.deltaY < 0 ? 1.1 : 1/1.1)));
  tf.x = mx - (mx-tf.x)*k/tf.k; tf.y = my - (my-tf.y)*k/tf.k; tf.k = k;
}, {passive:false});

let hov = null;
function hover(n){ hov = n; apply(); }
function neighbors(id){ const s = new Set([id]); (out[id]||[]).forEach(e => s.add(e.t)); (inn[id]||[]).forEach(e => s.add(e.s)); return s; }
function apply(){
  const focus = hov ? hov.id : state.sel;
  const nb = focus ? neighbors(focus) : null;
  const q = state.q.trim();
  const match = q ? new Set(nodes.filter(n => (M(n.title)+M(n.summary)).includes(q)).map(n => n.id)) : null;
  nodes.forEach((n, i) => {
    const el = nodeEls[i], hide = state.hidden.has(n.type);
    el.style.display = hide ? "none" : "";
    el.classList.toggle("sel", n.id === state.sel);
    el.classList.toggle("dim", (nb && !nb.has(n.id)) || (match && !match.has(n.id)));
    el.querySelector("text").textContent = M(n.title);
  });
  edgeEls.forEach(({e, el}, i) => {
    const hide = state.hidden.has(byId[e.s].type) || state.hidden.has(byId[e.t].type);
    const on = focus && (e.s === focus || e.t === focus);
    el.style.display = hide ? "none" : "";
    el.classList.toggle("hl", !!on);
    el.classList.toggle("dim", !!focus && !on);
    labelEls[i].setAttribute("display", on && !hide ? "" : "none");
  });
  const vn = nodes.filter(n => !state.hidden.has(n.type)).length;
  $("stats").textContent = `페이지 ${vn} · 관계 ${DATA.edges.filter(e => !state.hidden.has(byId[e.s].type) && !state.hidden.has(byId[e.t].type)).length}`;
}
function select(id, center){
  state.sel = id; renderPanel(); apply();
  if (id && center) { const n = byId[id]; tf.x = W/2 - n.x*tf.k; tf.y = H/2 - n.y*tf.k; }
}
function renderPanel(){
  const p = $("panel"), n = state.sel && byId[state.sel];
  if (!n) {
    const counts = {}; nodes.forEach(x => counts[x.type] = (counts[x.type]||0)+1);
    p.innerHTML = `<h2>${esc("설비 품질 지식 위키")}</h2><p class="sum">원문 ${counts["문서"]}건을 컴파일한 위키 페이지 ${nodes.length}개와 관계 ${DATA.edges.length}개</p>
      <p class="hint">노드를 클릭하면 내용과 연결된 페이지가 보입니다. 드래그로 이동, 휠로 확대·축소, 배경 드래그로 화면 이동. 위 칩으로 유형을 숨길 수 있습니다.</p>
      <h3>시작하기 좋은 페이지</h3><ul>${["i_etch02","i_cvd01","i_cln03","k_signal","x_check"].map(id => `<li><a class="wl" data-id="${id}">${esc(M(byId[id].title))}</a> <span class="rel">— ${esc(M(byId[id].summary))}</span></li>`).join("")}</ul>`;
    return;
  }
  const rel = (list, key, arrow) => list.map(e => `<li><span class="rel">${esc(e.rel)} ${arrow}</span> <a class="wl" data-id="${e[key]}">${esc(M(byId[e[key]].title))}</a></li>`).join("");
  const srcs = n.sources.map(s => `<li><a class="wl" data-id="${s}">${esc(M(byId[s].title))}</a></li>`).join("");
  p.innerHTML = `<span class="badge" style="background:${DATA.colors[n.type]}">${n.type}</span>
    <h2>${esc(M(n.title))}</h2><p class="sum">${esc(M(n.summary))}</p>${md(n.body)}
    ${out[n.id] ? `<h3>이 페이지에서 나가는 관계</h3><ul>${rel(out[n.id], "t", "→")}</ul>` : ""}
    ${inn[n.id] ? `<h3>이 페이지를 가리키는 관계 (역링크)</h3><ul>${rel(inn[n.id], "s", "←")}</ul>` : ""}
    ${srcs ? `<h3>근거 문서</h3><ul>${srcs}</ul>` : ""}
    <p class="hint">위키 파일: wiki/${n.type}/${esc(M(n.title))}.md</p>`;
  p.scrollTop = 0;
}
$("panel").addEventListener("click", ev => { const a = ev.target.closest("a.wl"); if (a) select(a.dataset.id, true); });

Object.keys(DATA.colors).forEach(t => {
  if (!nodes.some(n => n.type === t)) return;
  const c = document.createElement("span"); c.className = "chip"; c.innerHTML = `<i style="background:${DATA.colors[t]}"></i>${t}`;
  c.onclick = () => { state.hidden.has(t) ? state.hidden.delete(t) : state.hidden.add(t); c.classList.toggle("off"); alpha = Math.max(alpha, .5); apply(); };
  $("chips").appendChild(c);
});
$("mask").onchange = ev => { state.mask = ev.target.checked; renderPanel(); apply(); };
$("q").addEventListener("input", ev => { state.q = ev.target.value; apply(); });
$("q").addEventListener("keydown", ev => {
  if (ev.key !== "Enter") return;
  const q = state.q.trim(), m = nodes.find(n => !state.hidden.has(n.type) && (M(n.title)+M(n.summary)).includes(q));
  if (m) select(m.id, true);
});
for (let i = 0; i < 500; i++) step();
alpha = .1; fitView();
renderPanel(); apply(); tick();
</script>
</body>
</html>
"""


def write_html():
    data = dict(
        nodes=[dict(id=n["id"], type=n["type"], title=n["title"], summary=n["summary"], body=n["body"], sources=n["sources"])
               for n in NODES.values()],
        edges=edges, colors=TYPE_COLOR, names=NAMES)
    blob = json.dumps(data, ensure_ascii=False).replace("</", "<\\/")
    (ROOT / "wiki_graph.html").write_text(HTML.replace("__DATA__", blob), encoding="utf-8")


write_wiki()
write_html()
print(f"pages={len(NODES)} edges={len(edges)}")

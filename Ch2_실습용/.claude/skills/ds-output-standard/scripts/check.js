#!/usr/bin/env node
// 팀 표준 결과물 검사기
// 사용법: node check.js <파일.html> [doc|slide|dashboard]
// 종료 코드: ERROR가 있으면 1, 없으면 0
const fs = require('fs');
const path = require('path');

const file = process.argv[2];
if (!file) {
  console.error('사용법: node check.js <파일.html> [doc|slide|dashboard]');
  process.exit(2);
}
const html = fs.readFileSync(file, 'utf8');

// 유형 자동 판별 (인자로 지정하지 않은 경우)
let type = process.argv[3];
if (!type) {
  if (/class="page"/.test(html)) type = 'doc';
  else if (/class="deck"/.test(html)) type = 'slide';
  else type = 'dashboard';
}

const PALETTE = new Set([
  // 기본색
  '#0A1547', '#1428A0', '#7C9BFF', '#FFFFFF',
  // 데이터 계열
  '#4B5CC0', '#909BD6', '#C2C8E8', '#DCE0E9', '#98A3D2',
  // 상태색
  '#B0472F', '#E29A80', '#C98A2E',
  // 텍스트
  '#1B1F2A', '#2D3A52', '#4A5568', '#5A6478', '#6E7688', '#6B7280', '#7D8493', '#8B92A2', '#A8B4C8', '#9BA2B2',
  '#E8EBFA', '#CFD6F0', '#AEB8DE', '#8590BC',
  // 선·면·배경
  '#C8CCD8', '#D5D9E3', '#E1E4EC', '#EEF0F5', '#F1F2F7', '#F2F3F7', '#EDF0FB', '#E4E7EF', '#B4BAC9',
  '#E9EBF0', '#05091F',
]);

const errors = [];
const warns = [];
const err = (m) => errors.push(m);
const warn = (m) => warns.push(m);

// 1. 기본 구조
if (!/<html[^>]*lang="ko"/.test(html)) err('<html lang="ko">가 없습니다.');
if (!/<meta charset="utf-8">/i.test(html)) err('<meta charset="utf-8">가 없습니다.');
const title = (html.match(/<title>([^<]*)<\/title>/) || [])[1];
if (!title || !title.trim()) err('<title>이 비어 있습니다.');
else if (/샘플|견본|sample/i.test(title)) err(`<title>에 템플릿 문구가 남아 있습니다: "${title}"`);

// 2. 외부 리소스: Google Fonts만 허용
for (const m of html.matchAll(/<(script|link|img|iframe)\b[^>]*?(?:src|href)="([^"]+)"/gi)) {
  const url = m[2];
  if (!/^https?:\/\//.test(url)) {
    if (m[1].toLowerCase() === 'img' && !url.startsWith('data:')) err(`외부 파일 이미지 참조(standalone 아님): ${url}`);
    continue;
  }
  if (!/^https:\/\/fonts\.(googleapis|gstatic)\.com/.test(url)) err(`허용되지 않은 외부 리소스: <${m[1]}> ${url}`);
}
if (!/family=Gothic\+A1/.test(html) || !/Noto\+Sans\+KR/.test(html)) err('표준 서체(Gothic A1, Noto Sans KR) 링크가 없습니다.');

// 3. 팔레트 밖 색상
const offPalette = new Map();
for (const m of html.matchAll(/#[0-9a-fA-F]{6}\b|#[0-9a-fA-F]{3}\b/g)) {
  let c = m[0].toUpperCase();
  if (c.length === 4) c = '#' + [...c.slice(1)].map((x) => x + x).join('');
  if (!PALETTE.has(c)) offPalette.set(c, (offPalette.get(c) || 0) + 1);
}
for (const [c, n] of offPalette) err(`팔레트 밖 색상 ${c} (${n}회) — references/design-system.md 참고`);

// 4. 템플릿 예시 문구·수치가 남았는지
const LEFTOVERS = ['4,812', '1,240', 'DSI-2026-0831', '김수현', '교육생 실습용 템플릿', '실습 자료 · 슬라이드 견본',
  '레이아웃 안내', '예시 수치입니다', '생성형 AI 실무 활용', 'SW개발 1팀'];
for (const s of LEFTOVERS) if (html.includes(s)) warn(`템플릿 예시 문구가 남아 있습니다: "${s}" (입력 자료와 같다면 무시)`);

const pending = (html.match(/\[확인 필요\]/g) || []).length;

// 5. 유형별 검사
if (type === 'doc') {
  const pages = (html.match(/<section class="page"/g) || []).length;
  if (pages === 0) err('A4 페이지(<section class="page">)가 없습니다.');
  if (!/@page\s*\{\s*size:\s*A4/.test(html)) err('@page { size: A4 } 인쇄 설정이 없습니다.');
  if (!/<h1[\s>]/.test(html)) err('제목(h1)이 없습니다.');
  if (!/[ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ]\./.test(html)) warn('로마 숫자 장 번호(Ⅰ. Ⅱ. …)가 없습니다.');
  if (!/문서번호/.test(html)) warn('꼬리말 문서번호가 없습니다.');
  const pageNums = (html.match(/- \d+ -/g) || []).length;
  if (pageNums !== pages) warn(`쪽번호(- n -) ${pageNums}개 / 페이지 ${pages}개 — 일치하지 않습니다.`);
  if (!/background: #F2F3F7; border: 0\.75pt solid #D5D9E3/.test(html)) warn('1쪽 요약 박스가 보이지 않습니다.');
  if (/Gothic A1'/.test(html.replace(/<link[^>]*>/g, ''))) warn('보고서 본문에 Gothic A1이 쓰였습니다 (Noto Sans KR로 통일).');
} else if (type === 'slide') {
  const slides = [...html.matchAll(/<section\b([^>]*)>/g)].map((m) => m[1]);
  if (slides.length === 0) err('슬라이드(<section>)가 없습니다.');
  slides.forEach((attrs, i) => {
    for (const a of ['data-label', 'data-screen-label', 'data-speaker-notes'])
      if (!new RegExp(`${a}="[^"]+"`).test(attrs)) err(`${i + 1}번째 장에 ${a}가 없습니다.`);
    if (!/width/.test(attrs) && !/padding: 1[1-4]0px/.test(attrs)) warn(`${i + 1}번째 장의 여백이 표준(110px 120px 90px)과 다릅니다.`);
  });
  if (!/function fitDeck/.test(html)) err('화면 맞춤 스크립트(fitDeck)가 없습니다.');
  for (const m of html.matchAll(/font-size:\s*(\d+)px/g)) {
    if (+m[1] < 24) { warn(`24px 미만 글자 크기(${m[1]}px)가 있습니다.`); break; }
  }
  if (slides.length > 14) warn(`장 수가 ${slides.length}장입니다 (기본 6~11장).`);
} else if (type === 'dashboard') {
  if (!/<header\b/.test(html)) err('헤더(<header>)가 없습니다.');
  if (!/<h1[\s>]/.test(html)) err('제목(h1)이 없습니다.');
  if (!/기준/.test(html)) err('데이터 기준 시점 표기("~ 기준")가 없습니다.');
  if (!/분석 요약/.test(html)) warn('분석 요약 카드가 없습니다.');
  if (!/border-radius: 12px/.test(html)) warn('표준 카드(border-radius: 12px)가 보이지 않습니다.');
} else {
  err(`알 수 없는 유형: ${type} (doc|slide|dashboard)`);
}

// 결과 출력
console.log(`검사 대상: ${path.basename(file)} (유형: ${type})`);
errors.forEach((m) => console.log(`  ERROR  ${m}`));
warns.forEach((m) => console.log(`  WARN   ${m}`));
if (pending) console.log(`  INFO   [확인 필요] 표시 ${pending}곳 — 사용자에게 보고하세요.`);
console.log(errors.length ? `\n결과: 실패 (ERROR ${errors.length}, WARN ${warns.length})` : `\n결과: 통과 (WARN ${warns.length})`);
process.exit(errors.length ? 1 : 0);

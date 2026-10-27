"""reference/example.html 전체를 my-agent-team.html 의 샘플 iframe(srcdoc)에 넣는다.

경로로 불러오지 않고 한 파일 안에 포함하기 위한 스크립트입니다.
example.html 이 바뀌면 `내 업무/`에서 다시 실행: python scripts/embed_example.py
"""
import html
import re
from pathlib import Path

BASE = Path(__file__).resolve().parent.parent
EXAMPLE = BASE / "reference" / "example.html"
TARGET = BASE / "my-agent-team.html"
IFRAME_ID = "example"


def main():
    sample = EXAMPLE.read_text(encoding="utf-8")
    page = TARGET.read_text(encoding="utf-8")
    pattern = re.compile(r'(<iframe id="%s"[^>]*?srcdoc=")[^"]*(")' % IFRAME_ID)
    if not pattern.search(page):
        raise SystemExit(f'{TARGET.name}에서 <iframe id="{IFRAME_ID}" ... srcdoc=""> 를 찾지 못했습니다.')
    escaped = html.escape(sample, quote=True)
    page = pattern.sub(lambda m: m.group(1) + escaped + m.group(2), page, count=1)
    TARGET.write_text(page, encoding="utf-8")
    print(f"{EXAMPLE.name} ({len(sample):,}자) → {TARGET.name} 샘플 영역에 포함")


if __name__ == "__main__":
    main()

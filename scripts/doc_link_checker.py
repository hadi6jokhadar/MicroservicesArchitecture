import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "Doc"
link_pattern = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
missing = {}

for md in sorted(ROOT.glob("*.md")):
    text = md.read_text(encoding="utf-8")
    for match in link_pattern.findall(text):
        if match.startswith("http") or match.startswith("#"):
            continue
        target = match.split("#")[0]
        target_path = (md.parent / target).resolve()
        if not target_path.exists():
            missing.setdefault(md.name, set()).add(target)

if not missing:
    print("All Markdown links resolve to existing files.")
else:
    for src, targets in missing.items():
        print(f"{src}: missing -> {', '.join(sorted(targets))}")

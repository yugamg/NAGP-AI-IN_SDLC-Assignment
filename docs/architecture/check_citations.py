"""Check that every citation row in the architecture docs points at real text.

Usage: python3 -I docs/architecture/check_citations.py DOC [DOC ...]
A checked row looks like: | C1 | element | `path` | 9-12 | `expected text` |
Rows whose source is not a repo file (the assignment brief) only define the ID.
Fails if a diagram in any DOC cites an ID that no DOC defines.
"""
import re
import sys
from pathlib import Path

CHECKED_ROW = re.compile(r"^\|\s*([CSTE]\d+)\s*\|[^|]*\|\s*`([^`]+)`\s*\|\s*(\d+)(?:-(\d+))?\s*\|\s*`(.+)`\s*\|\s*$")
ANY_ROW = re.compile(r"^\|\s*([CSTE]\d+)\s*\|")
CITED = re.compile(r"\[([CSTE]\d+)\]")
MERMAID = re.compile(r"```mermaid\n(.*?)```", re.S)


def check(doc: Path, repo_root: Path, defined: set, cited: dict, failures: list) -> int:
    text = doc.read_text()
    checked = 0
    for line in text.splitlines():
        row = ANY_ROW.match(line)
        if row:
            defined.add(row.group(1))
        match = CHECKED_ROW.match(line)
        if not match:
            continue
        checked += 1
        citation_id, path, start, end, expected = match.groups()
        expected = expected.replace("\\`", "`")
        source = repo_root / path
        if not source.exists():
            failures.append(f"{doc.name} {citation_id}: {path} does not exist")
            continue
        first, last = int(start), int(end or start)
        window = "\n".join(source.read_text().splitlines()[first - 1:last])
        if expected not in window:
            failures.append(f"{doc.name} {citation_id}: '{expected}' not found in {path}:{first}-{last}")
    for diagram in MERMAID.findall(text):
        for citation_id in CITED.findall(diagram):
            cited.setdefault(citation_id, doc.name)
    return checked


def main(doc_paths: list) -> int:
    repo_root = Path(__file__).resolve().parents[2]
    defined, cited, failures = set(), {}, []
    checked = sum(check(Path(path), repo_root, defined, cited, failures) for path in doc_paths)
    for citation_id in sorted(cited.keys() - defined):
        failures.append(f"{cited[citation_id]} {citation_id}: cited in a diagram but has no citation row")
    for failure in failures:
        print("FAIL", failure)
    print(f"{checked} file citations checked, {len(defined)} IDs defined, {len(failures)} failures")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

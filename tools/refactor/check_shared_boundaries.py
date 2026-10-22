#!/usr/bin/env python3
"""Reject partial types whose implementations span Core/module ownership roots."""
from collections import defaultdict
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
NAMESPACE = re.compile(r"\bnamespace\s+([\w.]+)")
PARTIAL = re.compile(r"\bpartial\s+(?:class|struct|interface)\s+(\w+)")


def cross_owner_partials(source):
    """Return distributed partial types, retaining every contributing source path."""
    types = defaultdict(list)
    for path in sorted(source.rglob("*.cs")):
        relative = path.relative_to(source)
        if relative.parts[0] == "Core":
            owner = "Core"
        elif relative.parts[0] == "Modules":
            owner = relative.parts[1]
        else:
            continue
        text = path.read_text(encoding="utf-8-sig")
        namespaces = list(NAMESPACE.finditer(text))
        for declaration in PARTIAL.finditer(text):
            namespace = next((n[1] for n in reversed(namespaces)
                              if n.start() < declaration.start()), "")
            types[(namespace, declaration[1])].append((owner, relative.as_posix()))
    return {key: rows for key, rows in types.items()
            if len({owner for owner, _ in rows}) > 1}


def main():
    findings = cross_owner_partials(ROOT / "src")
    for (namespace, name), rows in findings.items():
        print(f"{namespace}.{name}")
        for owner, path in rows:
            print(f"  {owner}: src/{path}")
    print(f"Cross-owner partial types: {len(findings)}")
    return bool(findings)


if __name__ == "__main__":
    raise SystemExit(main())

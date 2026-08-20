#!/usr/bin/env python3
"""Analyze StaticResource usage in Wintangle.App XAML files.

For each .xaml file under src/Wintangle.App:
  1. Collect every `{StaticResource KEY}` usage.
  2. Collect every local definition `x:Key="KEY"` in the same file.
  3. Report keys USED but NEVER DEFINED anywhere (global app-level definitions
     come from App.xaml, Themes/*.xaml, Themes/Controls.xaml; the analysis
     collects definitions from ALL xaml files, so "defined anywhere" includes
     every file).
  4. Detect FORWARD references within a single file: key used at line N,
     defined at line M > N in the SAME file (StaticResource cannot
     forward-reference).

Exit code 0 when clean, 1 when issues found.
"""
import re
import sys
from pathlib import Path

# Resolve the app XAML root relative to this script (repo/build/..).
ROOT = Path(__file__).resolve().parent.parent / "src" / "Wintangle.App"

USE_RE = re.compile(r"\{StaticResource\s+([^}]+)\}")
DEF_RE = re.compile(r'x:Key\s*=\s*"([^"]+)"')
FORWARD_REF_RE = re.compile(r"\{StaticResource\s+([^}]+)\}")


def analyze():
    xaml_files = sorted(ROOT.rglob("*.xaml"))
    usage_by_file = {}   # file -> list[(key, line)]
    defs_by_file = {}    # file -> dict[key -> line]
    all_defs = {}        # key -> (file, line)

    for f in xaml_files:
        lines = f.read_text(encoding="utf-8").splitlines()
        uses = []
        defs = {}
        for i, line in enumerate(lines, start=1):
            for m in USE_RE.finditer(line):
                uses.append((m.group(1).strip(), i))
            for m in DEF_RE.finditer(line):
                key = m.group(1)
                defs[key] = i
                all_defs[key] = (f, i)
        usage_by_file[f] = uses
        defs_by_file[f] = defs

    print(f"== XAML analysis: {len(xaml_files)} files ==")

    # 1. Missing keys (used but never defined anywhere)
    missing = []
    for f, uses in usage_by_file.items():
        for key, line in uses:
            if key not in all_defs:
                missing.append((f, key, line))

    print(f"\n[1] Keys USED but NEVER DEFINED anywhere: {len(missing)}")
    for f, key, line in sorted(missing, key=lambda t: (str(t[0]), t[2])):
        print(f"    {f.relative_to(ROOT)}:{line}  ->  {key}")

    # 2. Forward references within the same file
    forward = []
    for f, uses in usage_by_file.items():
        defs = defs_by_file[f]
        for key, line in uses:
            if key in defs and defs[key] > line:
                forward.append((f, key, line, defs[key]))

    print(f"\n[2] FORWARD references (used before defined in same file): {len(forward)}")
    for f, key, line, defline in sorted(forward, key=lambda t: (str(t[0]), t[2])):
        print(f"    {f.relative_to(ROOT)}:{line}  ->  {key}  (defined at line {defline})")

    # Summary: all definitions per file (for reference)
    print("\n[3] Definitions per file:")
    for f in sorted(defs_by_file):
        defs = defs_by_file[f]
        if defs:
            keys = ", ".join(sorted(defs))
            print(f"    {f.relative_to(ROOT)} ({len(defs)}): {keys}")

    total = len(missing) + len(forward)
    print(f"\n== RESULT: {'CLEAN' if total == 0 else f'{total} ISSUE(S) FOUND'} ==")
    return 1 if total else 0


if __name__ == "__main__":
    sys.exit(analyze())

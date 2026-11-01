"""Fails the build when line coverage in a merged Cobertura report is below a threshold.

Usage: python3 scripts/coverage_gate.py <Cobertura.xml> <minimum line %>

Prints one GitHub Actions notice per assembly (package) and one for the total, so the
numbers show up as annotations on the run without opening the logs.
"""

import sys
import xml.etree.ElementTree as ElementTree


def main() -> int:
    report_path, minimum = sys.argv[1], float(sys.argv[2])
    root = ElementTree.parse(report_path).getroot()

    for package in root.iter("package"):
        rate = float(package.get("line-rate", 0)) * 100
        print(f"::notice title=Coverage {package.get('name')}::{rate:.1f}% of lines")

    covered, valid = int(root.get("lines-covered", 0)), int(root.get("lines-valid", 0))
    total = covered / valid * 100 if valid else 0.0
    summary = f"{total:.1f}% of lines ({covered}/{valid}), minimum {minimum:.0f}%"

    if total < minimum:
        print(f"::error title=Coverage below minimum::{summary}")
        return 1

    print(f"::notice title=Coverage total::{summary}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

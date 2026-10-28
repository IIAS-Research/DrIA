#!/usr/bin/env python3
"""Check the application version across release, citation, package and image files."""

import argparse
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path


def check_version(root: Path, expected_tag: str | None = None) -> str:
    version = (root / "version.txt").read_text().strip()
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise ValueError("version.txt must contain a stable semantic version.")

    package = json.loads((root / "client/package.json").read_text())
    lock = json.loads((root / "client/package-lock.json").read_text())
    manifest = json.loads((root / ".release-please-manifest.json").read_text())
    project = ET.parse(root / "Server/server.csproj")
    citation_versions = re.findall(
        r"^version:[ \t]*(.*)$",
        (root / "CITATION.cff").read_text(),
        re.MULTILINE,
    )
    if len(citation_versions) != 1:
        raise ValueError("CITATION.cff must declare one top-level stable semantic version.")
    citation_version = re.fullmatch(
        r"(['\"]?)(\d+\.\d+\.\d+)\1(?:[ \t]+#.*)?[ \t]*", citation_versions[0]
    )
    if citation_version is None:
        raise ValueError("CITATION.cff must declare one top-level stable semantic version.")
    versions = {
        ".release-please-manifest.json": manifest.get("."),
        "CITATION.cff": citation_version.group(2),
        "client/package.json": package.get("version"),
        "client/package-lock.json": lock.get("version"),
        "client/package-lock.json packages['']": lock.get("packages", {}).get("", {}).get("version"),
        "Server/server.csproj": project.findtext("./PropertyGroup/Version"),
    }
    for filename in ("Server/Dockerfile", "client/Dockerfile"):
        defaults = re.findall(r"^ARG DRIA_VERSION=(\S+)\s*$", (root / filename).read_text(), re.MULTILINE)
        if len(defaults) != 1:
            raise ValueError(f"{filename} must declare one DRIA_VERSION default.")
        versions[filename] = defaults[0]

    mismatches = [f"{path}: {value!r}" for path, value in versions.items() if value != version]
    if mismatches:
        raise ValueError(f"Expected version {version}; inconsistent files: " + "; ".join(mismatches))
    if expected_tag is not None and expected_tag != f"v{version}":
        raise ValueError(f"Expected release tag v{version}, got {expected_tag!r}.")
    return version


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--expected-tag", help="Also check a release tag such as v1.0.0.")
    args = parser.parse_args()
    try:
        version = check_version(Path(__file__).resolve().parents[2], args.expected_tag)
    except (ValueError, OSError, ET.ParseError) as error:
        parser.exit(1, f"Version check failed: {error}\n")
    print(f"DrIA version {version} is consistent.")


if __name__ == "__main__":
    main()

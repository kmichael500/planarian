#!/usr/bin/env python3
"""Validate repository-owned documentation contracts without external dependencies."""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import unquote

REPO_ROOT = Path(__file__).resolve().parents[1]
LOCAL_API_ORIGIN = "https://localhost:7111"
LOCAL_WEB_ORIGIN = "https://localhost:3000"
LEDGER_PATH = REPO_ROOT / "CAVE_REVISIONS_COMPLETION_STATUS.md"


def git(*args: str) -> str:
    return subprocess.check_output(
        ["git", *args], cwd=REPO_ROOT, text=True, stderr=subprocess.DEVNULL
    ).strip()


def tracked_markdown_files() -> list[Path]:
    output = git("ls-files", "--", "*.md")
    return [REPO_ROOT / path for path in output.splitlines() if path]


def markdown_without_fenced_code(text: str) -> str:
    kept: list[str] = []
    fence: str | None = None
    for line in text.splitlines(keepends=True):
        marker = re.match(r"^\s*(```+|~~~+)", line)
        if marker:
            token = marker.group(1)[0]
            if fence is None:
                fence = token
            elif fence == token:
                fence = None
            kept.append("\n")
            continue
        kept.append("\n" if fence else line)
    return "".join(kept)


def github_slug(value: str) -> str:
    value = re.sub(r"<[^>]+>", "", value)
    value = value.replace("`", "").replace("*", "")
    value = value.strip().lower()
    value = re.sub(r"[^\w\s-]", "", value, flags=re.UNICODE)
    value = re.sub(r"\s+", "-", value)
    return value


def markdown_anchors(path: Path) -> set[str]:
    counts: dict[str, int] = {}
    anchors: set[str] = set()
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        match = re.match(r"^#{1,6}\s+(.+?)\s*#*\s*$", line)
        if not match:
            continue
        base = github_slug(match.group(1))
        suffix = counts.get(base, 0)
        counts[base] = suffix + 1
        anchors.add(base if suffix == 0 else f"{base}-{suffix}")
    return anchors


def markdown_destination(raw: str) -> str:
    raw = raw.strip()
    if raw.startswith("<"):
        closing = raw.find(">")
        return raw[1:closing] if closing >= 0 else raw[1:]
    return raw.split(maxsplit=1)[0]


def validate_internal_markdown_links() -> list[str]:
    errors: list[str] = []
    anchor_cache: dict[Path, set[str]] = {}
    link_pattern = re.compile(r"!?\[[^\]]*\]\(([^)]+)\)")

    for path in tracked_markdown_files():
        text = markdown_without_fenced_code(path.read_text(encoding="utf-8", errors="replace"))
        for match in link_pattern.finditer(text):
            destination = markdown_destination(match.group(1))
            if not destination or re.match(r"^[a-zA-Z][a-zA-Z0-9+.-]*:", destination):
                continue

            line = text.count("\n", 0, match.start()) + 1
            path_part, separator, anchor = destination.partition("#")
            path_part = unquote(path_part)
            anchor = unquote(anchor).lower()
            target = path if not path_part else (path.parent / path_part).resolve()

            if not target.exists():
                errors.append(
                    f"{path.relative_to(REPO_ROOT)}:{line}: missing link target {destination!r}"
                )
                continue

            if separator and anchor and target.suffix.lower() == ".md":
                anchors = anchor_cache.setdefault(target, markdown_anchors(target))
                if anchor not in anchors:
                    errors.append(
                        f"{path.relative_to(REPO_ROOT)}:{line}: missing Markdown anchor #{anchor} in "
                        f"{target.relative_to(REPO_ROOT)}"
                    )

    return errors


def parse_env_file(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        value = value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in "'\"":
            value = value[1:-1]
        values[key.strip()] = value
    return values


def validate_native_https_contract() -> list[str]:
    errors: list[str] = []
    env_path = REPO_ROOT / "Planarian.Web/.env.example"
    env = parse_env_file(env_path)

    if env.get("HTTPS", "").lower() != "true":
        errors.append("Planarian.Web/.env.example must keep HTTPS=true for native authentication testing")

    try:
        mappings = json.loads(env.get("REACT_APP_API_ORIGIN_MAPPINGS", ""))
    except json.JSONDecodeError as exc:
        errors.append(f"Planarian.Web/.env.example has invalid REACT_APP_API_ORIGIN_MAPPINGS JSON: {exc}")
        mappings = {}

    if mappings.get("localhost") != LOCAL_API_ORIGIN:
        errors.append(
            "Planarian.Web/.env.example localhost API mapping must remain "
            f"{LOCAL_API_ORIGIN!r}; update docs/code together if the topology changes"
        )

    root_readme = (REPO_ROOT / "README.md").read_text(encoding="utf-8")
    web_readme = (REPO_ROOT / "Planarian.Web/README.md").read_text(encoding="utf-8")
    for label, text in (("README.md", root_readme), ("Planarian.Web/README.md", web_readme)):
        if LOCAL_API_ORIGIN not in text:
            errors.append(f"{label} must document the native API origin {LOCAL_API_ORIGIN}")
    if LOCAL_WEB_ORIGIN not in root_readme:
        errors.append(f"README.md must document the native web origin {LOCAL_WEB_ORIGIN}")
    if ".env.example" not in web_readme or "HTTPS" not in web_readme:
        errors.append("Planarian.Web/README.md must explain the .env.example HTTPS setup")

    cookie_options = (REPO_ROOT / "Planarian/Planarian/Modules/Authentication/Services/PlanarianCookieOptions.cs").read_text(
        encoding="utf-8"
    )
    if not re.search(r"SameSite\s*=\s*SameSiteMode\.Lax", cookie_options):
        errors.append("PlanarianCookieOptions no longer clearly configures SameSite=Lax; review native setup docs")
    if not re.search(r"SecurePolicy\s*=\s*CookieSecurePolicy\.Always", cookie_options):
        errors.append("PlanarianCookieOptions no longer clearly requires Secure cookies; review native setup docs")

    workflow = (REPO_ROOT / ".github/workflows/pr-validation.yml").read_text(encoding="utf-8")
    if LOCAL_API_ORIGIN not in workflow:
        errors.append("PR validation frontend build no longer uses the documented localhost API origin")

    return errors


def emit_warning(message: str) -> None:
    if os.environ.get("GITHUB_ACTIONS") == "true":
        print(f"::warning file={LEDGER_PATH.name}::{message}")
    else:
        print(f"WARNING: {message}")


def parse_ledger_sha(text: str, label_pattern: str) -> str | None:
    match = re.search(label_pattern + r"\s*`([0-9a-f]{7,40})`", text, re.IGNORECASE)
    return match.group(1) if match else None


def warn_if_completion_ledger_is_stale(base_sha: str | None) -> list[str]:
    if not LEDGER_PATH.exists():
        return []

    errors: list[str] = []
    text = LEDGER_PATH.read_text(encoding="utf-8")
    reviewed_head = parse_ledger_sha(text, r"\*\*Completion HEAD reviewed:\*\*")
    reviewed_main = parse_ledger_sha(text, r"\*\*Current `main` reviewed:\*\*")
    if not reviewed_head or not reviewed_main:
        errors.append(
            "CAVE_REVISIONS_COMPLETION_STATUS.md must retain parseable Completion HEAD reviewed and Current `main` reviewed SHAs"
        )
        return errors

    current_head = git("rev-parse", "HEAD")
    if not current_head.startswith(reviewed_head):
        # A ledger-refresh commit cannot contain its own final commit SHA. Allow the
        # common case where the only committed change after the reviewed SHA is the
        # ledger itself; any other committed change makes the snapshot stale.
        try:
            changed_since_review = {
                path
                for path in git("diff", "--name-only", f"{reviewed_head}..{current_head}").splitlines()
                if path
            }
        except subprocess.CalledProcessError:
            changed_since_review = {"<unknown>"}
        if changed_since_review - {LEDGER_PATH.name}:
            emit_warning(
                "Cave-revisions status ledger is stale: reviewed completion HEAD "
                f"{reviewed_head} != repository HEAD {current_head}. Refresh status claims before relying on them."
            )

    if base_sha is None:
        try:
            base_sha = git("rev-parse", "main")
        except subprocess.CalledProcessError:
            base_sha = None

    if base_sha and not base_sha.startswith(reviewed_main) and not reviewed_main.startswith(base_sha):
        emit_warning(
            "Cave-revisions status ledger is stale: reviewed main "
            f"{reviewed_main} != current merge-target SHA {base_sha}. Refresh merge-target-dependent claims."
        )

    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--base-sha",
        default=os.environ.get("PLANARIAN_DOC_BASE_SHA"),
        help="Current PR merge-target SHA; defaults to local main when available.",
    )
    args = parser.parse_args()

    errors = [
        *validate_internal_markdown_links(),
        *validate_native_https_contract(),
        *warn_if_completion_ledger_is_stale(args.base_sha),
    ]

    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1

    print("Repository documentation validation passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

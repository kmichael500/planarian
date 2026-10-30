#!/usr/bin/env python3
"""Choose Planarian release components from the last successful release commit.

No GitHub API/network/database access. The workflow supplies the last
successful release history; absent history deploys everything. Release refs may
move between branches, while already-deployed EF migrations remain immutable.
"""
import argparse
import fnmatch
import json
import subprocess
from datetime import datetime
from pathlib import Path

from release_tasks import select_release_tasks

MIGRATIONS_DIR = "Planarian/Planarian.Migrations/Migrations"
MODEL_SNAPSHOT = f"{MIGRATIONS_DIR}/PlanarianDbContextModelSnapshot.cs"
TRACKING_TASK = "planarian-coordinated-release"
SUCCESSFUL_DEPLOYMENT_STATES = {"SUCCESS", "ACTIVE"}
CLIENT_ROUTE_GENERATOR = "scripts/generate-client-routes.mjs"
WEB_BUILD_SCRIPTS = (
    CLIENT_ROUTE_GENERATOR,
    "scripts/configure-static-web-app-csp.mjs",
    "scripts/prepare-pdfjs-assets.mjs",
)


def match(path: str, pattern: str) -> bool:
    return fnmatch.fnmatchcase(path, pattern)


def require_commit(ref: str) -> None:
    available = subprocess.run(
        ["git", "cat-file", "-e", f"{ref}^{{commit}}"],
        capture_output=True,
        check=False,
    )
    if available.returncode != 0:
        raise ValueError(
            f"Release-history commit {ref} is not available in this checkout; "
            "refusing to infer deployment state"
        )


def validate_migration_safety(baseline: str, head: str) -> None:
    """Protect migration history and require a fresh migration for snapshot changes."""
    require_commit(baseline)
    require_commit(head)
    immutable = subprocess.run(
        ["git", "diff", "--name-only", "--diff-filter=MDR", "-z",
         baseline, head, "--", MIGRATIONS_DIR],
        check=True, capture_output=True,
    )
    migration_changes = [
        path.decode() for path in immutable.stdout.split(b"\0")
        if path and path.decode() != MODEL_SNAPSHOT
    ]
    if migration_changes:
        raise ValueError(
            "EF migration files from a prior coordinated release attempt are "
            "immutable; add a new migration instead of modifying/deleting: "
            + ", ".join(migration_changes)
        )

    changed = subprocess.run(
        ["git", "diff", "--name-only", "-z", baseline, head, "--", MODEL_SNAPSHOT],
        check=True, capture_output=True,
    )
    if MODEL_SNAPSHOT not in [path.decode() for path in changed.stdout.split(b"\0") if path]:
        return

    added = subprocess.run(
        ["git", "diff", "--name-only", "--diff-filter=A", "-z",
         baseline, head, "--", MIGRATIONS_DIR],
        check=True, capture_output=True,
    )
    added_migrations = [
        path.decode() for path in added.stdout.split(b"\0")
        if path
        and path.decode().endswith(".cs")
        and path.decode() != MODEL_SNAPSHOT
        and not path.decode().endswith(".Designer.cs")
    ]
    if not added_migrations:
        raise ValueError(
            "EF model snapshot changed without a new migration since the "
            "protected migration baseline; generate a migration instead of "
            "updating the snapshot alone"
        )


def diff_paths(
    previous: str | None,
    head: str,
    *,
    allow_non_ancestor: bool = False,
) -> list[str] | None:
    if not previous:
        return None
    require_commit(previous)
    require_commit(head)
    ancestor = subprocess.run(
        ["git", "merge-base", "--is-ancestor", previous, head],
        capture_output=True,
        check=False,
    )
    if ancestor.returncode != 0 and not allow_non_ancestor:
        raise ValueError(
            "Previously deployed SHA is not an ancestor of this release; "
            "refusing an implicit rollback/divergent production release"
        )
    # Development may deliberately move between branches. Compare the two
    # deployed trees directly. Migration safety is stricter: any migration file
    # seen by a prior coordinated attempt is immutable because a failed run may
    # already have applied it before failing later in the release.
    validate_migration_safety(previous, head)

    diff = subprocess.run(
        ["git", "diff", "--name-only", "-z", previous, head],
        check=True, capture_output=True,
    )
    return [p.decode() for p in diff.stdout.split(b"\0") if p]


def release_plan(paths: list[str] | None, *, force_all: bool = False) -> dict[str, object]:
    # The first release has no trusted baseline. Default to a full, safe deployment.
    first = paths is None
    paths = paths or []
    has = lambda *patterns: any(match(path, pattern) for path in paths for pattern in patterns)
    api = first or force_all or has(
        "Planarian/Planarian/**", "Planarian/Planarian.Library/**", "Contracts/**",
        "Planarian/Planarian.Model/**", CLIENT_ROUTE_GENERATOR
    )
    web = first or force_all or has("Planarian.Web/**", "Contracts/**", *WEB_BUILD_SCRIPTS)
    database = first or force_all or has(
        "Planarian/Planarian.Migrations/**", "Planarian/Planarian.Model/**",
        ".config/dotnet-tools.json"
    )
    tasks = select_release_tasks(paths, first_release=first)
    requirements = set().union(*(task.requires for task in tasks)) if tasks else set()
    api |= "api" in requirements
    web |= "web" in requirements
    database |= "database" in requirements
    maintenance_tasks = [task for task in tasks if task.maintenance]
    if len(maintenance_tasks) > 1:
        raise ValueError(
            "A release may select at most one maintenance task; selected: "
            + ", ".join(task.task_id for task in maintenance_tasks)
        )
    maintenance = bool(maintenance_tasks)

    # Generic deployment tooling changes re-exercise app deployment without
    # touching the database. One-off task definitions/scripts are deliberately
    # excluded: their own manifests decide what they require and when they run.
    infrastructure = has(".github/workflows/Planarian.Deploy.yml") or any(
        path.startswith("scripts/deploy/")
        and not path.startswith("scripts/deploy/tasks/")
        and not Path(path).name.startswith("test_")
        for path in paths
    )
    api |= infrastructure
    web |= infrastructure

    task_ids = [task.task_id for task in tasks]
    has_tasks = bool(task_ids)
    return {"api": bool(api), "web": bool(web), "database": bool(database),
            "tasks": task_ids, "has_tasks": has_tasks, "maintenance": maintenance,
            "any": bool(api or web or database or has_tasks),
            "first": first, "changed": paths}


def coordinated_deployments(payload: object) -> list[dict]:
    """Parse durable GitHub Deployment records returned by paginated GraphQL."""
    if not isinstance(payload, list) or not payload:
        raise ValueError("GitHub deployment history must be a non-empty paginated array")

    deployments: list[dict] = []
    declared_totals: set[int] = set()
    last_has_next = False

    for page in payload:
        if not isinstance(page, dict):
            raise ValueError("GitHub deployment history contains a non-object page")
        data = page.get("data")
        repository = data.get("repository") if isinstance(data, dict) else None
        connection = repository.get("deployments") if isinstance(repository, dict) else None
        if not isinstance(connection, dict):
            raise ValueError("GitHub deployment history page has no deployments connection")

        total_count = connection.get("totalCount")
        if not isinstance(total_count, int) or total_count < 0:
            raise ValueError("GitHub deployment history has an invalid totalCount")
        declared_totals.add(total_count)

        nodes = connection.get("nodes")
        if not isinstance(nodes, list):
            raise ValueError("GitHub deployment history page has no nodes list")
        deployments.extend(nodes)

        page_info = connection.get("pageInfo")
        if not isinstance(page_info, dict) or not isinstance(page_info.get("hasNextPage"), bool):
            raise ValueError("GitHub deployment history page has invalid pageInfo")
        last_has_next = page_info["hasNextPage"]

    if len(declared_totals) != 1:
        raise ValueError("GitHub deployment history pages disagree on totalCount")
    total_count = next(iter(declared_totals))
    if last_has_next or len(deployments) != total_count:
        raise ValueError("GitHub deployment history is incomplete; refusing to infer deployment state")

    matching: list[dict] = []
    for deployment in deployments:
        if not isinstance(deployment, dict):
            raise ValueError("GitHub deployment history contains a non-object deployment")
        if deployment.get("task") != TRACKING_TASK:
            continue

        sha = deployment.get("commitOid")
        if not isinstance(sha, str) or len(sha) != 40 or not all(
            char in "0123456789abcdefABCDEF" for char in sha
        ):
            raise ValueError("Matching deployment history has an invalid commit SHA")

        created_at = deployment.get("createdAt")
        if not isinstance(created_at, str):
            raise ValueError("Matching deployment history has no createdAt")
        try:
            parsed_created_at = datetime.fromisoformat(created_at.replace("Z", "+00:00"))
        except ValueError as error:
            raise ValueError("Matching deployment history has an invalid createdAt") from error
        if parsed_created_at.tzinfo is None:
            raise ValueError("Matching deployment history has a timezone-less createdAt")

        state = deployment.get("state")
        if state is not None and (not isinstance(state, str) or not state):
            raise ValueError("Matching deployment history has an invalid state")

        validated = dict(deployment)
        validated["state"] = state or "UNKNOWN"
        validated["_planarian_created_at"] = parsed_created_at
        matching.append(validated)

    matching.sort(key=lambda item: item["_planarian_created_at"], reverse=True)
    return matching


def previous_successful_sha(payload: object) -> str:
    """Choose the newest durable successful coordinated deployment."""
    for deployment in coordinated_deployments(payload):
        if deployment["state"] in SUCCESSFUL_DEPLOYMENT_STATES:
            return deployment["commitOid"]
    return ""


def attempted_release_shas(payload: object) -> list[str]:
    """Every newer deployment record may have crossed the database mutation boundary."""
    attempts: list[str] = []
    for deployment in coordinated_deployments(payload):
        if deployment["state"] in SUCCESSFUL_DEPLOYMENT_STATES:
            break
        sha = deployment["commitOid"]
        if sha not in attempts:
            attempts.append(sha)
    return attempts


def validate_maintenance_resume(payload: object, head: str) -> None:
    """Resume quiesced maintenance only from the exact newest unsuccessful release commit."""
    deployments = coordinated_deployments(payload)
    if not deployments or deployments[0]["state"] in SUCCESSFUL_DEPLOYMENT_STATES:
        raise ValueError(
            "resume_maintenance requires a newer unsuccessful coordinated release attempt"
        )

    attempted_sha = deployments[0]["commitOid"]
    if attempted_sha.lower() != head.lower():
        raise ValueError(
            "resume_maintenance must use the same release commit as the newest "
            f"unsuccessful coordinated attempt ({attempted_sha})"
        )


def main() -> None:
    parser = argparse.ArgumentParser()
    prior = parser.add_mutually_exclusive_group()
    prior.add_argument("--previous", default="")
    prior.add_argument("--deployments-file")
    parser.add_argument("--head", required=True)
    parser.add_argument("--force-all", action="store_true")
    parser.add_argument("--allow-non-ancestor", action="store_true")
    parser.add_argument("--resume-maintenance", action="store_true")
    parser.add_argument("--github-output")
    args = parser.parse_args()

    if args.resume_maintenance and not args.deployments_file:
        parser.error("--resume-maintenance requires --deployments-file")

    previous = args.previous
    if args.deployments_file:
        with open(args.deployments_file, encoding="utf-8") as deployments:
            payload = json.load(deployments)
        previous = previous_successful_sha(payload)
        if args.resume_maintenance:
            validate_maintenance_resume(payload, args.head)
        for attempted_sha in attempted_release_shas(payload):
            validate_migration_safety(attempted_sha, args.head)
    plan = release_plan(
        diff_paths(previous or None, args.head, allow_non_ancestor=args.allow_non_ancestor),
        force_all=args.force_all,
    )
    # A first release can span hundreds of files; do not flood CI logs.
    summary = {key: plan[key] for key in (
        "api", "web", "database", "tasks", "has_tasks", "maintenance", "any", "first"
    )}
    summary["changed_count"] = len(plan["changed"])
    print(json.dumps(summary, sort_keys=True))
    if args.github_output:
        with open(args.github_output, "a", encoding="utf-8") as output:
            for key in ("api", "web", "database", "has_tasks", "maintenance", "any", "first"):
                output.write(f"{key}={str(plan[key]).lower()}\n")
            output.write("tasks=" + json.dumps(plan["tasks"], separators=(",", ":")) + "\n")
    else:
        print("No GitHub output file supplied; plan is informational.")


if __name__ == "__main__":
    main()

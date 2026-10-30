#!/usr/bin/env python3
"""Small registry loader for explicit one-off release tasks.

The deployment planner remains task-agnostic. Each task owns a strict JSON
manifest and shell script under scripts/deploy/tasks/. Unknown files fail closed.
"""
from __future__ import annotations

from dataclasses import dataclass
import fnmatch
import json
from pathlib import Path
import re

TASKS_DIR = Path("scripts/deploy/tasks")
_ALLOWED_REQUIREMENTS = {"api", "web", "database"}
_MANIFEST_FIELDS = {
    "id",
    "script",
    "triggers",
    "requires",
    "maintenance",
    "run_on_first_release",
}


@dataclass(frozen=True)
class ReleaseTask:
    task_id: str
    script: str
    trigger_paths: tuple[str, ...]
    requires: frozenset[str]
    maintenance: bool
    run_on_first_release: bool


def _relative(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def load_release_tasks(root: Path | str = ".") -> list[ReleaseTask]:
    root = Path(root).resolve()
    tasks_dir = root / TASKS_DIR
    if not tasks_dir.exists():
        return []

    unexpected_entries = sorted(
        path.name
        for path in tasks_dir.iterdir()
        if not path.is_file() or path.is_symlink()
    )
    if unexpected_entries:
        raise ValueError(
            "Release task directory must contain only regular files: "
            + ", ".join(unexpected_entries)
        )

    tasks: list[ReleaseTask] = []
    registered_scripts: set[str] = set()
    seen_ids: set[str] = set()

    for manifest_path in sorted(tasks_dir.glob("*.json")):
        data = json.loads(manifest_path.read_text(encoding="utf-8"))
        if not isinstance(data, dict) or set(data) != _MANIFEST_FIELDS:
            missing = sorted(_MANIFEST_FIELDS - set(data)) if isinstance(data, dict) else sorted(_MANIFEST_FIELDS)
            unknown = sorted(set(data) - _MANIFEST_FIELDS) if isinstance(data, dict) else []
            raise ValueError(
                f"Release task manifest {manifest_path} has invalid fields; "
                f"missing={missing}, unknown={unknown}"
            )

        task_id = data["id"]
        script = data["script"]
        triggers = data["triggers"]
        requires = data["requires"]
        maintenance = data["maintenance"]
        run_on_first_release = data["run_on_first_release"]

        if (
            not isinstance(task_id, str)
            or re.fullmatch(r"[a-z0-9][a-z0-9-]*", task_id) is None
            or task_id in seen_ids
        ):
            raise ValueError(f"Invalid or duplicate release task id in {manifest_path}")
        if (
            not isinstance(script, str)
            or not script.endswith(".sh")
            or Path(script).is_absolute()
            or chr(92) in script
            or script.startswith("./")
            or ".." in Path(script).parts
            or not script.startswith(f"{TASKS_DIR.as_posix()}/")
        ):
            raise ValueError(f"Release task {task_id} has an invalid script path")
        script_path = (root / script).resolve()
        try:
            script_path.relative_to(tasks_dir.resolve())
        except ValueError as error:
            raise ValueError(
                f"Release task {task_id} script must stay under {TASKS_DIR.as_posix()}"
            ) from error
        if not script_path.is_file():
            raise ValueError(f"Release task {task_id} script is missing: {script}")
        canonical_script = _relative(script_path, root)
        if canonical_script != script:
            raise ValueError(f"Release task {task_id} script path is not canonical")
        script = canonical_script
        if script in registered_scripts:
            raise ValueError(f"Release task script is registered more than once: {script}")
        if not isinstance(triggers, list) or not all(isinstance(item, str) and item for item in triggers):
            raise ValueError(f"Release task {task_id} has invalid trigger paths")
        if len(set(triggers)) != len(triggers):
            raise ValueError(f"Release task {task_id} has duplicate trigger paths")
        for trigger in triggers:
            if (
                trigger.startswith("/")
                or trigger.startswith("./")
                or chr(92) in trigger
                or ".." in Path(trigger).parts
            ):
                raise ValueError(f"Release task {task_id} has a non-canonical trigger path: {trigger}")
        if not isinstance(requires, list) or not all(isinstance(item, str) for item in requires):
            raise ValueError(f"Release task {task_id} has invalid requirements")
        if len(set(requires)) != len(requires):
            raise ValueError(f"Release task {task_id} has duplicate requirements")
        unknown_requirements = set(requires) - _ALLOWED_REQUIREMENTS
        if unknown_requirements:
            raise ValueError(
                f"Release task {task_id} has unknown requirements: {', '.join(sorted(unknown_requirements))}"
            )
        if not isinstance(maintenance, bool) or not isinstance(run_on_first_release, bool):
            raise ValueError(f"Release task {task_id} has invalid boolean metadata")
        if not triggers and not run_on_first_release:
            raise ValueError(
                f"Release task {task_id} can never be selected; add a trigger "
                "or enable run_on_first_release"
            )
        if maintenance and "api" not in requires:
            raise ValueError(f"Maintenance release task {task_id} must require the API")

        manifest = _relative(manifest_path, root)
        # Editing the task definition or implementation always re-selects it.
        trigger_paths = tuple(dict.fromkeys([*triggers, manifest, script]))
        tasks.append(ReleaseTask(
            task_id=task_id,
            script=script,
            trigger_paths=trigger_paths,
            requires=frozenset(requires),
            maintenance=maintenance,
            run_on_first_release=run_on_first_release,
        ))
        seen_ids.add(task_id)
        registered_scripts.add(script)

    registered_manifests = {
        _relative(path, root)
        for path in tasks_dir.glob("*.json")
        if path.is_file()
    }
    allowed_files = registered_manifests | registered_scripts
    current_files = {
        _relative(path, root)
        for path in tasks_dir.iterdir()
        if path.is_file()
    }
    unregistered = sorted(current_files - allowed_files)
    if unregistered:
        raise ValueError("Unregistered release task file(s): " + ", ".join(unregistered))

    return tasks


def select_release_tasks(
    paths: list[str],
    *,
    first_release: bool,
    root: Path | str = ".",
) -> list[ReleaseTask]:
    selected: list[ReleaseTask] = []
    for task in load_release_tasks(root):
        triggered = any(
            fnmatch.fnmatchcase(path, pattern)
            for path in paths
            for pattern in task.trigger_paths
        )
        if triggered or (first_release and task.run_on_first_release):
            selected.append(task)
    return selected

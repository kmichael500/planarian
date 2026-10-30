#!/usr/bin/env python3
"""Execute the selected registered release tasks for one deployment phase."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess

from release_tasks import load_release_tasks


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tasks-json", required=True)
    parser.add_argument(
        "--phase",
        required=True,
        choices=("preflight", "before-migrations", "run", "after-api-deploy"),
    )
    args = parser.parse_args()

    requested = json.loads(args.tasks_json)
    if not isinstance(requested, list) or not all(isinstance(item, str) for item in requested):
        raise ValueError("--tasks-json must be a JSON array of task ids")
    if len(set(requested)) != len(requested):
        raise ValueError("--tasks-json contains duplicate task ids")

    registry = {task.task_id: task for task in load_release_tasks(Path.cwd())}
    unknown = [task_id for task_id in requested if task_id not in registry]
    if unknown:
        raise ValueError("Unknown selected release task(s): " + ", ".join(unknown))

    for task_id in requested:
        task = registry[task_id]
        if args.phase in ("before-migrations", "after-api-deploy") and not task.maintenance:
            continue
        subprocess.run(["bash", task.script, args.phase], check=True)


if __name__ == "__main__":
    main()

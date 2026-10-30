"""Unit tests for generic release-task registration."""
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from release_tasks import load_release_tasks, select_release_tasks

REPO_ROOT = Path(__file__).resolve().parents[2]
RUNNER = REPO_ROOT / "scripts/deploy/run_tasks.py"


class ReleaseTaskRegistryTests(unittest.TestCase):
    def make_task(self, root: Path, *, task_id="example", maintenance=False, requires=None):
        tasks = root / "scripts/deploy/tasks"
        tasks.mkdir(parents=True, exist_ok=True)
        script = tasks / f"{task_id}.sh"
        script.write_text("#!/usr/bin/env bash\n")
        manifest = tasks / f"{task_id}.json"
        manifest.write_text(json.dumps({
            "id": task_id,
            "script": f"scripts/deploy/tasks/{task_id}.sh",
            "triggers": [f"feature/{task_id}/**"],
            "requires": requires or [],
            "maintenance": maintenance,
            "run_on_first_release": False,
        }))
        return manifest, script

    def test_task_is_selected_by_domain_trigger_manifest_or_script(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest, script = self.make_task(root, requires=["api", "database"])
            for changed in (
                ["feature/example/change.txt"],
                [manifest.relative_to(root).as_posix()],
                [script.relative_to(root).as_posix()],
            ):
                selected = select_release_tasks(changed, first_release=False, root=root)
                self.assertEqual(["example"], [task.task_id for task in selected])
                self.assertEqual(frozenset({"api", "database"}), selected[0].requires)

    def test_unregistered_task_script_fails_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            tasks = root / "scripts/deploy/tasks"
            tasks.mkdir(parents=True)
            (tasks / "forgotten.sh").write_text("#!/usr/bin/env bash\n")
            with self.assertRaisesRegex(ValueError, "Unregistered release task file"):
                load_release_tasks(root)

    def test_stray_task_file_fails_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.make_task(root)
            (root / "scripts/deploy/tasks/forgotten.sql").write_text("select 1;")
            with self.assertRaisesRegex(ValueError, "Unregistered release task file"):
                load_release_tasks(root)

    def test_nested_task_directory_fails_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            tasks = root / "scripts/deploy/tasks"
            (tasks / "nested").mkdir(parents=True)
            with self.assertRaisesRegex(ValueError, "only regular files"):
                load_release_tasks(root)

    def test_task_script_cannot_escape_task_directory(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            tasks = root / "scripts/deploy/tasks"
            tasks.mkdir(parents=True)
            outside = root / "scripts/deploy/outside.sh"
            outside.write_text("#!/usr/bin/env bash\n")
            (tasks / "escape.json").write_text(json.dumps({
                "id": "escape",
                "script": "scripts/deploy/tasks/../outside.sh",
                "triggers": [],
                "requires": [],
                "maintenance": False,
                "run_on_first_release": False,
            }))
            with self.assertRaisesRegex(ValueError, "invalid script path"):
                load_release_tasks(root)

    def test_noncanonical_or_absolute_task_script_paths_fail_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest, script = self.make_task(root)
            data = json.loads(manifest.read_text())
            for invalid in (
                f"./scripts/deploy/tasks/{script.name}",
                str(script.resolve()),
                f"scripts/deploy/tasks/../tasks/{script.name}",
            ):
                data["script"] = invalid
                manifest.write_text(json.dumps(data))
                with self.assertRaisesRegex(ValueError, "invalid script path"):
                    load_release_tasks(root)

    def test_duplicate_triggers_and_requirements_fail_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest, _ = self.make_task(root)
            data = json.loads(manifest.read_text())

            data["triggers"] = ["feature/example/**", "feature/example/**"]
            manifest.write_text(json.dumps(data))
            with self.assertRaisesRegex(ValueError, "duplicate trigger paths"):
                load_release_tasks(root)

            data["triggers"] = ["feature/example/**"]
            data["requires"] = ["api", "api"]
            manifest.write_text(json.dumps(data))
            with self.assertRaisesRegex(ValueError, "duplicate requirements"):
                load_release_tasks(root)

    def test_manifest_field_typos_fail_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest, _ = self.make_task(root)
            data = json.loads(manifest.read_text())
            data["maintenace"] = data.pop("maintenance")
            manifest.write_text(json.dumps(data))
            with self.assertRaisesRegex(ValueError, "invalid fields"):
                load_release_tasks(root)

    def test_same_script_cannot_be_registered_twice(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest, _ = self.make_task(root)
            data = json.loads(manifest.read_text())
            data["id"] = "duplicate"
            duplicate = manifest.with_name("duplicate.json")
            duplicate.write_text(json.dumps(data))
            with self.assertRaisesRegex(ValueError, "registered more than once"):
                load_release_tasks(root)

    def test_unselectable_task_manifest_fails_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest, _ = self.make_task(root)
            data = json.loads(manifest.read_text())
            data["triggers"] = []
            data["run_on_first_release"] = False
            manifest.write_text(json.dumps(data))
            with self.assertRaisesRegex(ValueError, "can never be selected"):
                load_release_tasks(root)

    def test_maintenance_task_must_require_api(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.make_task(root, maintenance=True, requires=["database"])
            with self.assertRaisesRegex(ValueError, "must require the API"):
                load_release_tasks(root)

    def test_retired_task_directory_can_be_empty(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "scripts/deploy/tasks").mkdir(parents=True)
            self.assertEqual([], load_release_tasks(root))
            self.assertEqual([], select_release_tasks(
                ["scripts/deploy/tasks/retired.json"],
                first_release=False,
                root=root,
            ))

    def test_runner_rejects_duplicate_task_ids(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.make_task(root)
            result = subprocess.run(
                [
                    sys.executable,
                    str(RUNNER),
                    "--tasks-json",
                    '["example","example"]',
                    "--phase",
                    "run",
                ],
                cwd=root,
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, result.returncode)
            self.assertIn("duplicate task ids", result.stderr)

    def test_runner_executes_selected_task_phase(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _, script = self.make_task(root)
            script.write_text('#!/usr/bin/env bash\nprintf "%s\\n" "$1" >> "$CAPTURE"\n')
            capture = root / "capture.txt"
            env = os.environ.copy()
            env["CAPTURE"] = str(capture)

            subprocess.run(
                [
                    sys.executable,
                    str(RUNNER),
                    "--tasks-json",
                    '["example"]',
                    "--phase",
                    "run",
                ],
                cwd=root,
                env=env,
                check=True,
            )
            self.assertEqual("run", capture.read_text().strip())


if __name__ == "__main__":
    unittest.main()

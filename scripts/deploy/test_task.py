"""Behavior tests for the one-time Cave revision release task."""
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

from release_tasks import select_release_tasks

REPO_ROOT = Path(__file__).resolve().parents[2]
TASK = REPO_ROOT / "scripts/deploy/tasks/cave-revision-history.sh"
MIGRATION = "Planarian/Planarian.Migrations/Migrations/20261029194500_CaveRevisionsCompletion.cs"
MANIFEST = "scripts/deploy/tasks/cave-revision-history.json"
OPERATIONS = "Planarian/Planarian.Operations/CaveRevisionHistory/InitializeCaveRevisionHistoryOperation.cs"
CAVE_REVISIONS = "Planarian/Planarian.Operations/CaveRevisionHistory/CaveRevisionRolloutRepository.cs"


class CaveRevisionHistoryTaskTests(unittest.TestCase):
    def test_manifest_selects_the_one_off_and_declares_its_dependencies(self):
        for changed in (
            [MIGRATION],
            [MANIFEST],
            [TASK.relative_to(REPO_ROOT).as_posix()],
            [OPERATIONS],
            [CAVE_REVISIONS],
        ):
            selected = select_release_tasks(changed, first_release=False, root=REPO_ROOT)
            self.assertEqual(["cave-revision-history"], [task.task_id for task in selected])
            self.assertEqual(frozenset({"api", "database"}), selected[0].requires)
            self.assertTrue(selected[0].maintenance)

        first = select_release_tasks([], first_release=True, root=REPO_ROOT)
        self.assertIn("cave-revision-history", [task.task_id for task in first])

    def test_cave_one_off_does_not_match_unrelated_operations_or_shared_library_changes(self):
        for changed in (
            ["Planarian/Planarian.Operations/Operations/FutureUnrelatedOperation.cs"],
            ["Planarian/Planarian.Operations/README.md"],
            ["Planarian/Planarian.Library/Caves/Revisions/FutureRevisionRepository.cs"],
        ):
            selected = select_release_tasks(changed, first_release=False, root=REPO_ROOT)
            self.assertEqual([], selected)

    def common_env(self, root: Path) -> dict[str, str]:
        publish = root / "operations-publish"
        publish.mkdir()
        env = os.environ.copy()
        env.update({
            "RELEASE_OPERATIONS_PUBLISH_DIR": str(publish),
            "ConnectionStrings__default": "Host=example.invalid;Database=planarian",
            "AZURE_RESOURCE_GROUP": "test-rg",
            "AZURE_WEBAPP_NAME": "test-app",
        })
        return env

    def test_preflight_validates_required_configuration_without_side_effects(self):
        with tempfile.TemporaryDirectory() as temporary:
            env = self.common_env(Path(temporary))
            subprocess.run(["bash", str(TASK), "preflight"], env=env, check=True)

            del env["ConnectionStrings__default"]
            failed = subprocess.run(
                ["bash", str(TASK), "preflight"],
                env=env,
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, failed.returncode)
            self.assertIn("ConnectionStrings__default", failed.stderr)

    def test_preflight_requires_the_registered_cave_migration(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            env = self.common_env(root)
            failed = subprocess.run(
                ["bash", str(TASK), "preflight"],
                cwd=root,
                env=env,
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, failed.returncode)
            self.assertIn("CaveRevisionsCompletion.cs", failed.stderr)

    def test_run_invokes_the_operations_artifact_once(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            env = self.common_env(root)
            operations_dll = Path(env["RELEASE_OPERATIONS_PUBLISH_DIR"]) / "Planarian.Operations.dll"
            operations_dll.write_text("placeholder")

            bin_dir = root / "bin"
            bin_dir.mkdir()
            capture = root / "capture.txt"
            fake_dotnet = bin_dir / "dotnet"
            fake_dotnet.write_text(
                "#!/usr/bin/env bash\n"
                "printf '%s\\n' \"$ConnectionStrings__default|$*\" > \"$CAPTURE\"\n"
            )
            fake_dotnet.chmod(0o755)
            env.update({
                "PATH": f"{bin_dir}{os.pathsep}{env['PATH']}",
                "CAPTURE": str(capture),
            })

            subprocess.run(["bash", str(TASK), "run"], env=env, check=True)

            self.assertEqual(
                f"Host=example.invalid;Database=planarian|{operations_dll} initialize-cave-revision-history",
                capture.read_text().strip(),
            )

    def test_run_fails_if_operations_artifact_is_missing(self):
        with tempfile.TemporaryDirectory() as temporary:
            env = self.common_env(Path(temporary))
            failed = subprocess.run(
                ["bash", str(TASK), "run"],
                env=env,
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, failed.returncode)
            self.assertIn("Planarian operations artifact is missing", failed.stderr)

    def test_maintenance_phases_stop_then_restart_the_api(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            env = self.common_env(root)
            bin_dir = root / "bin"
            bin_dir.mkdir()
            capture = root / "az-capture.txt"
            fake_az = bin_dir / "az"
            fake_az.write_text(
                "#!/usr/bin/env bash\n"
                "case \"$*\" in\n"
                "  *\"--query state\"*) echo \"${AZ_STATE:-Running}\" ;;\n"
                "  *\"--query scmSiteAlsoStopped\"*) echo false ;;\n"
                "  *\"appsettings list\"*) echo false ;;\n"
                "  *) printf '%s\\n' \"$*\" >> \"$CAPTURE\" ;;\n"
                "esac\n"
            )
            fake_az.chmod(0o755)
            env.update({
                "PATH": f"{bin_dir}{os.pathsep}{env['PATH']}",
                "CAPTURE": str(capture),
                "RESUME_MAINTENANCE": "false",
            })

            subprocess.run(["bash", str(TASK), "before-migrations"], env=env, check=True)
            subprocess.run(["bash", str(TASK), "after-api-deploy"], env=env, check=True)

            commands = capture.read_text().splitlines()
            self.assertEqual(2, len(commands))
            self.assertIn("webapp stop", commands[0])
            self.assertIn("webapp start", commands[1])

    def test_resume_accepts_an_already_stopped_api_without_stopping_again(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            env = self.common_env(root)
            bin_dir = root / "bin"
            bin_dir.mkdir()
            capture = root / "az-capture.txt"
            fake_az = bin_dir / "az"
            fake_az.write_text(
                "#!/usr/bin/env bash\n"
                "case \"$*\" in\n"
                "  *\"--query state\"*) echo Stopped ;;\n"
                "  *\"--query scmSiteAlsoStopped\"*) echo false ;;\n"
                "  *\"appsettings list\"*) echo false ;;\n"
                "  *) printf '%s\\n' \"$*\" >> \"$CAPTURE\" ;;\n"
                "esac\n"
            )
            fake_az.chmod(0o755)
            env.update({
                "PATH": f"{bin_dir}{os.pathsep}{env['PATH']}",
                "CAPTURE": str(capture),
                "RESUME_MAINTENANCE": "true",
            })

            subprocess.run(["bash", str(TASK), "before-migrations"], env=env, check=True)
            self.assertFalse(capture.exists())


if __name__ == "__main__":
    unittest.main()

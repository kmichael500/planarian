"""Offline, deterministic release-planning tests (python3 -m unittest discover)."""
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from plan import (
    TRACKING_TASK,
    attempted_release_shas,
    diff_paths,
    release_plan,
    previous_successful_sha,
    validate_maintenance_resume,
)

PLAN_SCRIPT = Path(__file__).with_name("plan.py")


def deployment(sha, state, created_at, *, task=TRACKING_TASK):
    return {
        "commitOid": sha,
        "createdAt": created_at,
        "state": state,
        "task": task,
    }


def deployment_page(nodes, *, total_count=None, has_next_page=False):
    return {
        "data": {
            "repository": {
                "deployments": {
                    "totalCount": len(nodes) if total_count is None else total_count,
                    "nodes": nodes,
                    "pageInfo": {
                        "hasNextPage": has_next_page,
                        "endCursor": "next" if has_next_page else None,
                    },
                }
            }
        }
    }


class PlanTests(unittest.TestCase):
    def test_durable_deployment_history_uses_last_success_and_newer_attempts(self):
        successful_sha = "d" * 40
        failed_sha = "b" * 40
        ignored_sha = "a" * 40
        payload = [deployment_page([
            deployment(failed_sha, "FAILURE", "2026-10-29T17:10:00Z"),
            deployment(ignored_sha, "SUCCESS", "2026-10-29T17:05:00Z", task="other-task"),
            deployment(successful_sha, "SUCCESS", "2026-10-29T17:00:00Z"),
        ])]

        self.assertEqual(successful_sha, previous_successful_sha(payload))
        self.assertEqual([failed_sha], attempted_release_shas(payload))

    def test_active_deployment_state_is_a_successful_baseline(self):
        active_sha = "4" * 40
        payload = [deployment_page([
            deployment(active_sha, "ACTIVE", "2026-10-29T18:30:00Z"),
        ])]
        self.assertEqual(active_sha, previous_successful_sha(payload))
        self.assertEqual([], attempted_release_shas(payload))

    def test_durable_deployment_history_orders_by_creation_time(self):
        older_sha = "1" * 40
        newer_sha = "2" * 40
        payload = [deployment_page([
            deployment(older_sha, "SUCCESS", "2026-10-29T17:00:00Z"),
            deployment(newer_sha, "SUCCESS", "2026-10-29T18:00:00Z"),
        ])]
        self.assertEqual(newer_sha, previous_successful_sha(payload))

    def test_paginated_deployment_history_is_complete_and_ordered(self):
        successful_sha = "9" * 40
        first_page_nodes = [
            deployment(f"{number:040x}", "FAILURE", f"2026-10-29T18:{number:02d}:00Z")
            for number in range(10)
        ]
        payload = [
            deployment_page(first_page_nodes, total_count=11, has_next_page=True),
            deployment_page(
                [deployment(successful_sha, "SUCCESS", "2026-10-29T17:00:00Z")],
                total_count=11,
            ),
        ]
        self.assertEqual(successful_sha, previous_successful_sha(payload))
        self.assertEqual(
            [item["commitOid"] for item in sorted(
                first_page_nodes,
                key=lambda item: item["createdAt"],
                reverse=True,
            )],
            attempted_release_shas(payload),
        )

    def test_incomplete_deployment_history_fails_closed(self):
        payload = [
            deployment_page(
                [deployment("8" * 40, "SUCCESS", "2026-10-29T17:00:00Z")],
                total_count=2,
            )
        ]
        with self.assertRaisesRegex(ValueError, "history is incomplete"):
            previous_successful_sha(payload)

    def test_statusless_tracking_record_is_treated_as_an_attempt(self):
        attempted_sha = "7" * 40
        successful_sha = "6" * 40
        payload = [deployment_page([
            deployment(attempted_sha, None, "2026-10-29T18:00:00Z"),
            deployment(successful_sha, "SUCCESS", "2026-10-29T17:00:00Z"),
        ])]
        self.assertEqual(successful_sha, previous_successful_sha(payload))
        self.assertEqual([attempted_sha], attempted_release_shas(payload))

    def test_invalid_matching_deployment_fails_closed(self):
        payload = [deployment_page([
            deployment("bogus", "SUCCESS", "2026-10-29T17:00:00Z"),
        ])]
        with self.assertRaisesRegex(ValueError, "invalid commit SHA"):
            previous_successful_sha(payload)

    def test_empty_durable_deployment_history_means_first_coordinated_release(self):
        payload = [deployment_page([])]
        self.assertEqual("", previous_successful_sha(payload))
        self.assertEqual([], attempted_release_shas(payload))

    def test_maintenance_resume_requires_durable_deployment_history(self):
        result = subprocess.run(
            [
                sys.executable,
                str(PLAN_SCRIPT),
                "--head",
                "7" * 40,
                "--resume-maintenance",
            ],
            capture_output=True,
            text=True,
        )
        self.assertNotEqual(0, result.returncode)
        self.assertIn("--resume-maintenance requires --deployments-file", result.stderr)

    def test_maintenance_resume_requires_newest_unsuccessful_release_commit(self):
        successful_sha = "5" * 40
        attempted_sha = "6" * 40
        payload = [deployment_page([
            deployment(attempted_sha, "FAILURE", "2026-10-29T18:00:00Z"),
            deployment(successful_sha, "SUCCESS", "2026-10-29T17:00:00Z"),
        ])]

        validate_maintenance_resume(payload, attempted_sha)
        with self.assertRaisesRegex(ValueError, "same release commit"):
            validate_maintenance_resume(payload, "7" * 40)

        completed = [deployment_page([
            deployment(successful_sha, "SUCCESS", "2026-10-29T19:00:00Z"),
        ])]
        with self.assertRaisesRegex(ValueError, "unsuccessful coordinated release attempt"):
            validate_maintenance_resume(completed, successful_sha)

    def test_frontend_only(self):
        self.assertEqual(
            (False, True, False, False),
            self.flags(["Planarian.Web/src/App.tsx"]),
        )

    def test_shared_cave_revision_library_change_deploys_api_without_rerunning_completed_rollout(self):
        plan = release_plan([
            "Planarian/Planarian.Library/Caves/Revisions/CavePublishedSnapshotRepository.cs"
        ])
        self.assertTrue(plan["api"])
        self.assertFalse(plan["web"])
        self.assertFalse(plan["database"])
        self.assertFalse(plan["has_tasks"])
        self.assertEqual([], plan["tasks"])

    def test_backend_only(self):
        self.assertEqual((True, False, False, False),
                         self.flags(["Planarian/Planarian/Startup/Program.cs"]))

    def test_database_only(self):
        self.assertEqual((False, False, True, False),
                         self.flags(["Planarian/Planarian.Migrations/Migrations/20270101_Other.cs"]))

    def test_ef_tool_manifest_change_rebuilds_database_bundle(self):
        self.assertEqual((False, False, True, False),
                         self.flags([".config/dotnet-tools.json"]))

    def test_shared_contracts_build_both(self):
        self.assertEqual((True, True, False, False), self.flags(["Contracts/ApiRoutes.cs"]))

    def test_model_changes_build_api_and_migrate(self):
        self.assertEqual((True, False, True, False),
                         self.flags(["Planarian/Planarian.Model/Database/PlanarianDbContext.cs"]))

    def test_mixed_release(self):
        self.assertEqual((False, True, True, False), self.flags(
            ["Planarian.Web/src/index.tsx",
             "Planarian/Planarian.Migrations/Migrations/20270101_Other.cs"]))

    def test_shared_route_generator_deploys_both(self):
        self.assertEqual((True, True, False, False),
                         self.flags(["scripts/generate-client-routes.mjs"]))

    def test_frontend_csp_build_script_deploys_web(self):
        self.assertEqual((False, True, False, False),
                         self.flags(["scripts/configure-static-web-app-csp.mjs"]))

    def test_pdf_asset_build_script_deploys_web(self):
        self.assertEqual((False, True, False, False),
                         self.flags(["scripts/prepare-pdfjs-assets.mjs"]))

    def test_unrelated_test_script_not_deployed(self):
        self.assertEqual((False, False, False, False),
                         self.flags(["scripts/run-browser-e2e.test.mjs"]))

    def test_new_deploy_tool_redeploys_apps_without_touching_database(self):
        self.assertEqual((True, True, False, False),
                         self.flags(["scripts/deploy/verify-release.sh"]))

    def test_deployment_test_changes_do_not_deploy(self):
        self.assertEqual((False, False, False, False),
                         self.flags(["scripts/deploy/test_workflow.py"]))

    def test_documentation_does_not_deploy(self):
        self.assertEqual((False, False, False, False), self.flags(["docs/testing.md"]))

    def test_first_release_conservatively_selects_all_core_components(self):
        plan = release_plan(None)
        self.assertTrue(plan["api"])
        self.assertTrue(plan["web"])
        self.assertTrue(plan["database"])
        self.assertTrue(plan["any"])

    def test_force_is_additive_not_rollback(self):
        self.assertEqual((True, True, True, False),
                         self.flags(["docs/testing.md"], force_all=True))

    def test_deployment_workflow_redeploys_apps_without_touching_database(self):
        self.assertEqual((True, True, False, False),
                         self.flags([".github/workflows/Planarian.Deploy.yml"]))

    def test_multiple_maintenance_tasks_fail_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            tasks = root / "scripts/deploy/tasks"
            tasks.mkdir(parents=True)
            for task_id in ("first", "second"):
                script = tasks / f"{task_id}.sh"
                script.write_text("#!/usr/bin/env bash\n")
                (tasks / f"{task_id}.json").write_text(json.dumps({
                    "id": task_id,
                    "script": f"scripts/deploy/tasks/{task_id}.sh",
                    "triggers": ["shared/**"],
                    "requires": ["api"],
                    "maintenance": True,
                    "run_on_first_release": False,
                }))
            cwd = os.getcwd()
            try:
                os.chdir(root)
                with self.assertRaisesRegex(ValueError, "at most one maintenance task"):
                    release_plan(["shared/change.txt"])
            finally:
                os.chdir(cwd)

    def test_unknown_script_fails_closed(self):
        with tempfile.TemporaryDirectory() as temp:
            task_dir = Path(temp) / "scripts/deploy/tasks"
            task_dir.mkdir(parents=True)
            (task_dir / "forgotten.sh").write_text("#!/bin/bash\n")
            cwd = os.getcwd()
            try:
                os.chdir(temp)
                with self.assertRaisesRegex(ValueError, "Unregistered release task file"):
                    release_plan(["scripts/deploy/tasks/forgotten.sh"])
            finally:
                os.chdir(cwd)

    def test_missing_registered_script_fails_before_deployment(self):
        with tempfile.TemporaryDirectory() as temp:
            task_dir = Path(temp) / "scripts/deploy/tasks"
            task_dir.mkdir(parents=True)
            (task_dir / "broken.json").write_text(
                '{"id":"broken","script":"scripts/deploy/tasks/missing.sh",'
                '"triggers":["anything"],"requires":[],"maintenance":false,'
                '"run_on_first_release":false}'
            )
            cwd = os.getcwd()
            try:
                os.chdir(temp)
                with self.assertRaisesRegex(ValueError, "script is missing"):
                    release_plan(["anything"])
            finally:
                os.chdir(cwd)

    def test_git_diff_from_last_success_includes_failed_changes(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            def git(*args):
                return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()
            git("init", "-q")
            git("config", "user.name", "Test")
            git("config", "user.email", "test@example.invalid")
            target = root / "Planarian.Web"
            target.mkdir()
            (target / "App.tsx").write_text("first")
            git("add", ".")
            git("commit", "-qm", "base")
            good = git("rev-parse", "HEAD")
            (target / "App.tsx").write_text("second")
            git("add", ".")
            git("commit", "-qm", "web change whose deployment fails")
            (root / "docs").mkdir()
            (root / "docs" / "README.md").write_text("docs only")
            git("add", ".")
            git("commit", "-qm", "later docs change")
            head = git("rev-parse", "HEAD")
            cwd = os.getcwd()
            try:
                os.chdir(root)
                changed = diff_paths(good, head)
                self.assertIn("Planarian.Web/App.tsx", changed)
                self.assertEqual((False, True, False, False), self.flags(changed))
                with self.assertRaisesRegex(ValueError, "not an ancestor"):
                    diff_paths(head, good)
                reverse_changed = diff_paths(head, good, allow_non_ancestor=True)
                self.assertIn("Planarian.Web/App.tsx", reverse_changed)
            finally:
                os.chdir(cwd)

    def test_failed_release_migration_cannot_be_edited_before_next_success(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)

            def git(*args):
                return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()

            git("init", "-q")
            git("config", "user.name", "Test")
            git("config", "user.email", "test@example.invalid")
            (root / "README.md").write_text("base")
            git("add", ".")
            git("commit", "-qm", "successful release")
            successful = git("rev-parse", "HEAD")

            migration = root / "Planarian/Planarian.Migrations/Migrations/20260101000000_Risky.cs"
            migration.parent.mkdir(parents=True)
            migration.write_text("version-1")
            git("add", ".")
            git("commit", "-qm", "failed release that may have applied migration")
            attempted = git("rev-parse", "HEAD")

            migration.write_text("version-2")
            git("add", ".")
            git("commit", "-qm", "illegal edit after failed release")
            current = git("rev-parse", "HEAD")

            deployments_file = root / "deployments.json"
            deployments_file.write_text(json.dumps([deployment_page([
                deployment(attempted, "FAILURE", "2026-10-29T17:10:00Z"),
                deployment(successful, "SUCCESS", "2026-10-29T17:00:00Z"),
            ])]))

            result = subprocess.run(
                [
                    sys.executable,
                    str(PLAN_SCRIPT),
                    "--deployments-file",
                    str(deployments_file),
                    "--head",
                    current,
                    "--allow-non-ancestor",
                ],
                cwd=root,
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, result.returncode)
            self.assertIn("prior coordinated release attempt", result.stderr)

    def test_failed_release_snapshot_change_requires_a_newer_migration(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)

            def git(*args):
                return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()

            git("init", "-q")
            git("config", "user.name", "Test")
            git("config", "user.email", "test@example.invalid")
            migrations = root / "Planarian/Planarian.Migrations/Migrations"
            migrations.mkdir(parents=True)
            snapshot = migrations / "PlanarianDbContextModelSnapshot.cs"
            snapshot.write_text("snapshot-0")
            git("add", ".")
            git("commit", "-qm", "successful release")
            successful = git("rev-parse", "HEAD")

            first_migration = migrations / "20260101000000_First.cs"
            first_migration.write_text("migration-1")
            snapshot.write_text("snapshot-1")
            git("add", ".")
            git("commit", "-qm", "failed release may have applied first migration")
            attempted = git("rev-parse", "HEAD")

            snapshot.write_text("snapshot-2")
            git("add", ".")
            git("commit", "-qm", "invalid snapshot advance without newer migration")
            missing_migration = git("rev-parse", "HEAD")

            deployments_file = root / "deployments.json"
            deployments_file.write_text(json.dumps([deployment_page([
                deployment(attempted, "FAILURE", "2026-10-29T17:10:00Z"),
                deployment(successful, "SUCCESS", "2026-10-29T17:00:00Z"),
            ])]))

            failed = subprocess.run(
                [
                    sys.executable,
                    str(PLAN_SCRIPT),
                    "--deployments-file",
                    str(deployments_file),
                    "--head",
                    missing_migration,
                    "--allow-non-ancestor",
                ],
                cwd=root,
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, failed.returncode)
            self.assertIn("snapshot changed without a new migration", failed.stderr)

            second_migration = migrations / "20260202000000_Second.cs"
            second_migration.write_text("migration-2")
            git("add", ".")
            git("commit", "-qm", "add migration for post-failure model change")
            repaired = git("rev-parse", "HEAD")

            repaired_result = subprocess.run(
                [
                    sys.executable,
                    str(PLAN_SCRIPT),
                    "--deployments-file",
                    str(deployments_file),
                    "--head",
                    repaired,
                    "--allow-non-ancestor",
                ],
                cwd=root,
                capture_output=True,
                text=True,
            )
            self.assertEqual(0, repaired_result.returncode, repaired_result.stderr)

    def test_deployed_migrations_are_immutable_and_snapshot_requires_new_migration(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            def git(*args):
                return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()
            git("init", "-q")
            git("config", "user.name", "Test")
            git("config", "user.email", "test@example.invalid")
            migrations = root / "Planarian/Planarian.Migrations/Migrations"
            migrations.mkdir(parents=True)
            existing = migrations / "20260101000000_Existing.cs"
            existing.write_text("v1")
            snapshot = migrations / "PlanarianDbContextModelSnapshot.cs"
            snapshot.write_text("snapshot-1")
            git("add", ".")
            git("commit", "-qm", "deployed")
            deployed = git("rev-parse", "HEAD")

            existing.write_text("v2")
            git("add", ".")
            git("commit", "-qm", "illegally edit deployed migration")
            edited = git("rev-parse", "HEAD")
            cwd = os.getcwd()
            try:
                os.chdir(root)
                with self.assertRaisesRegex(ValueError, "immutable"):
                    diff_paths(deployed, edited)
            finally:
                os.chdir(cwd)

            git("reset", "--hard", deployed)
            snapshot.write_text("snapshot-without-migration")
            git("add", ".")
            git("commit", "-qm", "invalid snapshot-only change")
            snapshot_only = git("rev-parse", "HEAD")
            cwd = os.getcwd()
            try:
                os.chdir(root)
                with self.assertRaisesRegex(ValueError, "snapshot changed without a new migration"):
                    diff_paths(deployed, snapshot_only)
            finally:
                os.chdir(cwd)

            git("reset", "--hard", deployed)
            new_migration = migrations / "20260202000000_New.cs"
            new_migration.write_text("new")
            snapshot.write_text("snapshot-2")
            git("add", ".")
            git("commit", "-qm", "new migration")
            added = git("rev-parse", "HEAD")
            cwd = os.getcwd()
            try:
                os.chdir(root)
                changed = diff_paths(deployed, added)
                self.assertIn("Planarian/Planarian.Migrations/Migrations/20260202000000_New.cs", changed)
                self.assertIn("Planarian/Planarian.Migrations/Migrations/PlanarianDbContextModelSnapshot.cs", changed)
            finally:
                os.chdir(cwd)

    def test_development_branch_switch_cannot_drop_a_deployed_migration(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)

            def git(*args):
                return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()

            git("init", "-q")
            git("config", "user.name", "Test")
            git("config", "user.email", "test@example.invalid")
            (root / "README.md").write_text("base")
            git("add", ".")
            git("commit", "-qm", "base")
            base = git("rev-parse", "HEAD")

            migrations = root / "Planarian/Planarian.Migrations/Migrations"
            migrations.mkdir(parents=True)
            (migrations / "20260101000000_BranchOnly.cs").write_text("migration")
            git("add", ".")
            git("commit", "-qm", "branch-only migration")
            deployed_branch = git("rev-parse", "HEAD")

            git("checkout", "-q", "-b", "alternate", base)
            (root / "README.md").write_text("alternate")
            git("add", ".")
            git("commit", "-qm", "alternate branch")
            alternate = git("rev-parse", "HEAD")

            cwd = os.getcwd()
            try:
                os.chdir(root)
                with self.assertRaisesRegex(ValueError, "immutable"):
                    diff_paths(deployed_branch, alternate, allow_non_ancestor=True)
            finally:
                os.chdir(cwd)

    @staticmethod
    def flags(paths, force_all=False):
        plan = release_plan(paths, force_all=force_all)
        return tuple(plan[k] for k in ("api", "web", "database", "maintenance"))


if __name__ == "__main__":
    unittest.main()

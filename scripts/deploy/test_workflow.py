"""Static contracts for the coordinated GitHub Actions release workflow."""
import re
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = REPO_ROOT / ".github/workflows/Planarian.Deploy.yml"
PR_WORKFLOW = REPO_ROOT / ".github/workflows/pr-validation.yml"


class WorkflowContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = WORKFLOW.read_text(encoding="utf-8")
        cls.pr_workflow = PR_WORKFLOW.read_text(encoding="utf-8")

    def step(self, name: str) -> int:
        marker = f"- name: {name}"
        position = self.workflow.find(marker)
        self.assertNotEqual(-1, position, f"Missing workflow step: {name}")
        return position

    def test_all_artifacts_are_ready_and_state_is_durable_before_mutation(self):
        release_record = self.step("Begin durable coordinated release record")
        maintenance = self.step("Prepare registered maintenance tasks")
        migration = self.step("Apply pending EF migrations")
        for name in (
            "Package API artifact",
            "Build operations artifact",
            "Build web artifact",
            "Validate model and build one-shot EF migration bundle",
        ):
            self.assertLess(self.step(name), release_record)
        self.assertLess(release_record, maintenance)
        self.assertLess(release_record, migration)

    def test_release_order_preserves_database_task_api_web_dependency(self):
        self.assertLess(
            self.step("Apply pending EF migrations"),
            self.step("Run registered release tasks"),
        )
        self.assertLess(
            self.step("Run registered release tasks"),
            self.step("Deploy API"),
        )
        self.assertLess(self.step("Deploy API"), self.step("Deploy web"))

    def test_database_release_rejects_uncaptured_model_changes(self):
        build = self.step("Validate model and build one-shot EF migration bundle")
        migration = self.step("Apply pending EF migrations")
        self.assertLess(build, migration)
        self.assertIn("migrations has-pending-model-changes", self.workflow)

    def test_database_secret_is_shared_with_operations_without_app_config_access(self):
        self.assertGreaterEqual(self.workflow.count("ConnectionStrings__default:"), 4)
        self.assertNotIn('--connection "$MIGRATION_CONNECTION_STRING"', self.workflow)
        self.assertNotIn("AZURE_APP_CONFIGURATION_CONNECTION_STRING", self.workflow)
        self.assertIn("Build operations artifact", self.workflow)
        self.assertIn("Planarian/Planarian.Operations/Planarian.Operations.csproj", self.workflow)
        self.assertIn("GetConnectionString(\"default\")", (
            REPO_ROOT / "Planarian/Planarian.Migrations/DesignTime/DesignTimeFactory.cs"
        ).read_text(encoding="utf-8"))
        operation = (
            REPO_ROOT
            / "Planarian/Planarian.Operations/CaveRevisionHistory/InitializeCaveRevisionHistoryOperation.cs"
        ).read_text(encoding="utf-8")
        self.assertIn('Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)', operation)
        self.assertNotIn("Azure App Configuration", operation)

    def test_release_state_uses_durable_github_deployments_not_workflow_runs(self):
        self.assertIn("Read durable coordinated deployment history", self.workflow)
        self.assertIn("gh api graphql --paginate --slurp", self.workflow)
        self.assertIn("deployments(", self.workflow)
        self.assertIn("commitOid", self.workflow)
        self.assertIn("--deployments-file", self.workflow)
        self.assertNotIn("/actions/workflows/Planarian.Deploy.yml/runs", self.workflow)
        self.assertIn('task="planarian-coordinated-release"', self.workflow)
        self.assertIn("planarian-release-state-development", self.workflow)
        self.assertIn("planarian-release-state-production", self.workflow)
        self.assertIn("Record successful coordinated deployment", self.workflow)
        self.assertIn("Mark durable deployment record failed", self.workflow)
        self.assertGreaterEqual(self.workflow.count("auto_inactive=false"), 3)

    def test_all_action_dependencies_are_immutable_and_repo_token_is_not_exposed(self):
        action_refs = re.findall(r"uses: ([^/\s]+/[^@\s]+)@([^\s#]+)", self.workflow)
        self.assertGreaterEqual(len(action_refs), 6)
        self.assertTrue(
            all(re.fullmatch(r"[0-9a-f]{40}", ref) for _, ref in action_refs),
            action_refs,
        )
        self.assertNotIn("repo_token:", self.workflow)
        pr_action_refs = re.findall(r"uses: ([^/\s]+/[^@\s]+)@([^\s#]+)", self.pr_workflow)
        self.assertTrue(
            all(re.fullmatch(r"[0-9a-f]{40}", ref) for _, ref in pr_action_refs),
            pr_action_refs,
        )

    def test_preflight_verifies_app_settings_without_mutating_them(self):
        self.assertIn("Planarian deployment requires a Linux x86_64 runner.", self.workflow)
        self.assertIn("Verify hosted API configuration", self.workflow)
        self.assertIn("az webapp config appsettings list", self.workflow)
        self.assertNotIn("az webapp config appsettings set", self.workflow)
        for command in ("gh", "python3", "curl", "az", "zip"):
            self.assertIn(f"require_command {command}", self.workflow)

    def test_operations_boundary_does_not_depend_on_or_pollute_web_startup(self):
        web_program = (
            REPO_ROOT / "Planarian/Planarian/Startup/Program.cs"
        ).read_text(encoding="utf-8")
        operations_program = (
            REPO_ROOT / "Planarian/Planarian.Operations/Program.cs"
        ).read_text(encoding="utf-8")
        operations_project = (
            REPO_ROOT / "Planarian/Planarian.Operations/Planarian.Operations.csproj"
        ).read_text(encoding="utf-8")
        web_project = (
            REPO_ROOT / "Planarian/Planarian/Planarian.csproj"
        ).read_text(encoding="utf-8")
        model_project = (
            REPO_ROOT / "Planarian/Planarian.Model/Planarian.Model.csproj"
        ).read_text(encoding="utf-8")
        library_project = (
            REPO_ROOT / "Planarian/Planarian.Library/Planarian.Library.csproj"
        ).read_text(encoding="utf-8")
        rollout_repository_path = (
            REPO_ROOT
            / "Planarian/Planarian.Operations/CaveRevisionHistory/CaveRevisionRolloutRepository.cs"
        )

        self.assertNotIn("initialize-cave-revision-history", web_program)
        self.assertNotIn("CaveRevisionRolloutRepository", web_program)
        self.assertNotIn("CaveRevisionReadinessRepository", web_program)
        self.assertNotIn("initialize-cave-revision-history", operations_program)
        self.assertNotIn("CaveRevisionRolloutRepository", operations_program)
        self.assertTrue(rollout_repository_path.is_file())
        self.assertFalse(
            (REPO_ROOT / "Planarian/Planarian.Library/Caves/Revisions/CaveRevisionRolloutRepository.cs").exists()
        )
        self.assertFalse(
            (REPO_ROOT / "Planarian/Planarian.Library/Caves/Revisions/CaveRevisionReadinessRepository.cs").exists()
        )
        self.assertNotIn("Planarian/Planarian.csproj", operations_project)
        self.assertNotIn("Planarian\\Planarian.csproj", operations_project)
        self.assertIn("Planarian.Library", operations_project)
        self.assertIn('<InternalsVisibleTo Include="Planarian.Tests.Integration" />', operations_project)
        self.assertNotIn("Planarian.Operations", web_project)
        self.assertNotIn('ProjectReference Include="..\\Planarian.Operations', library_project)
        self.assertIn('<InternalsVisibleTo Include="Planarian.Operations" />', library_project)
        self.assertNotIn("Planarian.Library", model_project)
        self.assertIn("Planarian.Model", library_project)
        snapshot_repository = (
            REPO_ROOT
            / "Planarian/Planarian.Library/Caves/Revisions/CavePublishedSnapshotRepository.cs"
        ).read_text(encoding="utf-8")
        self.assertNotIn("ForRevisionRollout", snapshot_repository)
        self.assertIn(
            "internal static CavePublishedSnapshotRepository ForTrustedAccountScope",
            snapshot_repository,
        )
        rollout_repository = rollout_repository_path.read_text(encoding="utf-8")
        self.assertIn("internal sealed class CaveRevisionRolloutRepository", rollout_repository)
        self.assertEqual(4, rollout_repository.count("IgnoreQueryFilters()"))
        self.assertEqual(
            2,
            rollout_repository.count("knownAccountIds.Contains(revision.AccountId)"),
        )
        self.assertEqual(
            1,
            rollout_repository.count("knownAccountIds.Contains(cave.AccountId)"),
        )
        self.assertEqual(
            1,
            rollout_repository.count("accountIds.Contains(cave.AccountId)"),
        )

    def test_registered_tasks_are_generic_and_maintenance_resume_is_explicit(self):
        self.assertIn("resume_maintenance:", self.workflow)
        self.assertIn("--resume-maintenance", self.workflow)
        self.assertIn("registered maintenance release task", self.workflow)
        self.assertIn("scripts/deploy/run_tasks.py", self.workflow)
        self.assertNotIn("Cave", self.workflow)
        self.assertNotIn("cave-revision-history", self.workflow)

    def test_manual_development_can_run_from_arbitrary_ref(self):
        self.assertNotIn("Development releases must run from main", self.workflow)
        self.assertIn('if [[ "$target" == Development ]]; then', self.workflow)
        self.assertIn("Manual Development releases may", self.workflow)
        self.assertIn("args+=(--allow-non-ancestor)", self.workflow)

    def test_production_is_independent_and_still_requires_release_tag(self):
        self.assertNotIn("Require successful Development promotion source", self.workflow)
        self.assertNotIn("Production releases must promote", self.workflow)
        self.assertIn("refs/tags/planarian-release-v*", self.workflow)
        self.assertIn("last successful release", self.workflow)

    def test_environment_scoping_is_explicit(self):
        self.assertIn(
            "needs.plan.outputs.environment == 'Production' && vars.REACT_APP_UMAMI_WEBSITE_ID || ''",
            self.workflow,
        )

    def test_changed_workflows_use_least_privilege_checkout_credentials(self):
        self.assertIn("permissions:\n  contents: read", self.workflow)
        self.assertIn("deployments: read", self.workflow)
        self.assertIn("deployments: write", self.workflow)
        self.assertNotIn("actions: read", self.workflow)
        self.assertGreaterEqual(self.workflow.count("persist-credentials: false"), 2)
        self.assertIn("permissions:\n  contents: read", self.pr_workflow)
        self.assertIn("persist-credentials: false", self.pr_workflow)

    def test_pr_validation_rejects_edits_to_base_migrations(self):
        self.assertIn('".github/workflows/**"', self.pr_workflow)
        self.assertIn('".config/**"', self.pr_workflow)
        self.assertIn("fetch-depth: 0", self.pr_workflow)
        self.assertIn("Reject edits to migrations already present on the PR base", self.pr_workflow)
        self.assertIn("--previous \"$PLANARIAN_DEPLOY_BASE_SHA\" --head \"$GITHUB_SHA\"", self.pr_workflow)


if __name__ == "__main__":
    unittest.main()

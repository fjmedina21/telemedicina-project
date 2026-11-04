import tempfile
import unittest
from pathlib import Path

from teleharness.collaboration import CollaborationError, CollaborationPolicy
from teleharness.config import ROOT
from teleharness.tools import WorkspaceTools, ToolError, apply_git_patch


class CollaborationTests(unittest.TestCase):
    def setUp(self):
        self.policy = CollaborationPolicy.load(ROOT)

    def test_backend_only_owner(self):
        self.assertEqual(self.policy.paths_for("mobile", "frontend"), ("app/src/",))
        with self.assertRaises(CollaborationError):
            self.policy.paths_for("mobile", "backend")
        with self.assertRaises(CollaborationError):
            self.policy.paths_for("mobile", "fullstack")

    def test_backend_owner_can_work_across_areas(self):
        self.assertIn("api/src/", self.policy.paths_for("backend_mobile", "backend"))
        self.assertEqual(len(self.policy.paths_for("backend_mobile", "fullstack")), 3)

    def test_portal_separate_area(self):
        self.assertEqual(self.policy.paths_for("mobile", "portal"), ("portalweb/src/",))
        self.assertEqual(self.policy.paths_for("backend_mobile", "portal"), ("portalweb/src/",))

    def test_writable_paths_enforced(self):
        with tempfile.TemporaryDirectory() as tmp:
            base = Path(tmp)
            (base / "app" / "src").mkdir(parents=True)
            (base / "api" / "src").mkdir(parents=True)
            tools = WorkspaceTools(base, allowed_paths=self.policy.paths_for("mobile", "frontend"))
            backend_patch = (
                "diff --git a/api/src/Test.cs b/api/src/Test.cs\n"
                "new file mode 100644\n--- /dev/null\n+++ b/api/src/Test.cs\n"
                "@@ -0,0 +1 @@\n+class Test {}\n"
            )
            with self.assertRaises(CollaborationError):
                tools.verify_patch(backend_patch)
            with self.assertRaises(CollaborationError):
                apply_git_patch(base, backend_patch, self.policy.paths_for("mobile", "frontend"))

    def test_agentes_file_short(self):
        text = (ROOT.parent / "AGENTS.md").read_text(encoding="utf-8")
        self.assertLessEqual(len(text.splitlines()), 14)
        self.assertIn("agents/*.md", text)


if __name__ == "__main__":
    unittest.main()

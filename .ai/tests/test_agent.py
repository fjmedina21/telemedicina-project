import json
import tempfile
import unittest
from pathlib import Path

from teleharness.agent import AgentEngine, AgentError, parse_action, ROLES
from teleharness.config import ROOT
from teleharness.context import list_modules, load_context
from teleharness.providers import MockProvider
from teleharness.tools import WorkspaceTools


class AgentTests(unittest.TestCase):
    def test_parse_json_and_code_fence(self):
        obj = parse_action('```json\n{"action":"finish","args":{"summary":"ok"}}\n```')
        self.assertEqual(obj["action"], "finish")
        with self.assertRaises(AgentError):
            parse_action('{"action":"execute_shell","args":{}}')

    def test_specs_complete(self):
        modules = list_modules(ROOT)
        self.assertEqual(len(modules), 12)
        context = load_context(ROOT, "M04", 18000)
        self.assertIn("Citas", context)
        self.assertIn("doble", context.lower())

    def test_roles_present(self):
        for role in ROLES:
            self.assertTrue((ROOT / "agents" / f"{role.upper()}.md").is_file())

    def test_mock_run_no_modification(self):
        with tempfile.TemporaryDirectory() as folder:
            base = Path(folder)
            workspace = base / "workspace"
            workspace.mkdir()
            (workspace / "keep.txt").write_text("unchanged", encoding="utf-8")
            engine = AgentEngine(MockProvider(), WorkspaceTools(workspace), ROOT, max_steps=3)
            result = engine.run(
                module="M04", role="implementer", task="inspeccionar", context="Spec de prueba",
                run_dir=base / "run", actor="mobile", area="frontend",
            )
            self.assertEqual(result["status"], "completed")
            self.assertEqual((workspace / "keep.txt").read_text(encoding="utf-8"), "unchanged")
            self.assertFalse((base / "run" / "proposal.patch").exists())
            self.assertEqual(json.loads((base / "run" / "result.json").read_text())["role"], "implementer")
            self.assertEqual(result["area"], "frontend")


if __name__ == "__main__":
    unittest.main()

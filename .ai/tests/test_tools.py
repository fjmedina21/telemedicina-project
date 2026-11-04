import shutil
import tempfile
import unittest
from pathlib import Path

from teleharness.tools import ToolError, WorkspaceTools, apply_git_patch, validate_patch


class ToolTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.base = Path(self.tmp.name)
        (self.base / "main.dart").write_text("void main() {}\n", encoding="utf-8")
        (self.base / ".env").write_text("SECRET=do-not-read\n", encoding="utf-8")
        self.tools = WorkspaceTools(self.base)

    def tearDown(self):
        self.tmp.cleanup()

    def test_read_file(self):
        self.assertIn("void main", self.tools.read_file("main.dart")["content"])
        self.assertNotIn(".env", self.tools.list_files()["files"])

    def test_secret_and_traversal_blocked(self):
        for name in (".env", "../other", "a/../../b", "private.pem", ".git/config"):
            with self.subTest(name=name), self.assertRaises(ToolError):
                self.tools.read_file(name)

    def test_symlink_outside_blocked(self):
        with tempfile.TemporaryDirectory() as other:
            target = Path(other, "outside.txt")
            target.write_text("outside", encoding="utf-8")
            (self.base / "link.txt").symlink_to(target)
            with self.assertRaises(ToolError):
                self.tools.read_file("link.txt")

    def test_safe_patch_application(self):
        if not shutil.which("git"):
            self.skipTest("git no disponible")
        patch = (
            "diff --git a/main.dart b/main.dart\n"
            "--- a/main.dart\n"
            "+++ b/main.dart\n"
            "@@ -1 +1 @@\n"
            "-void main() {}\n"
            "+void main() { print('test'); }\n"
        )
        self.assertEqual(validate_patch(patch, self.base), ["main.dart"])
        self.tools.verify_patch(patch)
        apply_git_patch(self.base, patch)
        self.assertIn("print('test')", (self.base / "main.dart").read_text(encoding="utf-8"))

    def test_patch_restrictions(self):
        patches = [
            "diff --git a/.env b/.env\n--- a/.env\n+++ b/.env\n@@ -1 +1 @@\n-a\n+b\n",
            "diff --git a/../victim b/../victim\n--- a/../victim\n+++ b/../victim\n@@ -0,0 +1 @@\n+x\n",
            "diff --git a/main.dart b/main.dart\ndeleted file mode 100644\n--- a/main.dart\n+++ /dev/null\n@@ -1 +0,0 @@\n-void main() {}\n",
            "diff --git a/link b/link\nnew file mode 120000\n--- /dev/null\n+++ b/link\n@@ -0,0 +1 @@\n+elsewhere\n",
        ]
        for p in patches:
            with self.subTest(p=p[:40]), self.assertRaises(ToolError):
                validate_patch(p, self.base)


if __name__ == "__main__":
    unittest.main()

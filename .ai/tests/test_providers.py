import os
import unittest
from unittest.mock import patch

from teleharness.providers import (
    AnthropicMessagesProvider,
    CompatibleChatProvider,
    OpenAIResponsesProvider,
    ProviderError,
    validate_compatible_url,
)


class ProviderTests(unittest.TestCase):
    def setUp(self):
        self.messages = [{"role": "user", "content": "Hola"}]

    @patch.dict(os.environ, {"OPENAI_API_KEY": "dummy-test-value"})
    @patch("teleharness.providers._post_json")
    def test_openai_output(self, post):
        post.return_value = {"output": [{"type": "message", "content": [{"type": "output_text", "text": '{"action":"finish","args":{}}'}]}]}
        text = OpenAIResponsesProvider("model-a").generate(system="system", messages=self.messages)
        self.assertIn('"finish"', text)
        self.assertEqual(post.call_args.args[0], "https://api.openai.com/v1/responses")
        self.assertEqual(post.call_args.args[2]["input"], self.messages)

    @patch.dict(os.environ, {"ANTHROPIC_API_KEY": "dummy-test-value"})
    @patch("teleharness.providers._post_json")
    def test_anthropic_output(self, post):
        post.return_value = {"content": [{"type": "text", "text": "ok"}]}
        self.assertEqual(AnthropicMessagesProvider("model-b").generate(system="x", messages=self.messages), "ok")
        self.assertEqual(post.call_args.args[2]["system"], "x")

    @patch("teleharness.providers._post_json")
    def test_compatible_local_no_key(self, post):
        post.return_value = {"choices": [{"message": {"content": "ok"}}]}
        with patch.dict(os.environ, {"COMPAT_API_KEY": ""}):
            result = CompatibleChatProvider("local").generate(system="s", messages=self.messages)
        self.assertEqual(result, "ok")
        self.assertEqual(post.call_args.args[0], "http://127.0.0.1:11434/v1/chat/completions")

    def test_remote_insecure_url_rejected(self):
        with self.assertRaises(ProviderError):
            validate_compatible_url("http://example.com/v1")
        with self.assertRaises(ProviderError):
            validate_compatible_url("https://user:pass@example.com/v1")

    def test_missing_openai_key_rejected_without_network(self):
        with patch.dict(os.environ, {"OPENAI_API_KEY": ""}):
            with self.assertRaises(ProviderError):
                OpenAIResponsesProvider("model").generate(system="s", messages=self.messages)


if __name__ == "__main__":
    unittest.main()

"""Exercise pinned native downloads against real local HTTP transports, without external services."""
from collections import Counter
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import os
from pathlib import Path
import socket
import struct
import subprocess
import tempfile
import threading
import unittest


FETCH = Path(__file__).resolve().parents[3] / "build/android/native/fetch.sh"
CONTENT = b"pinned native source archive\n" * 1000
SHA256 = hashlib.sha256(CONTENT).hexdigest()


class NativeFetchTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.destination = Path(self.temp.name) / "downloads/source archive.zip"
        self.responses = {}
        self.requests = Counter()
        case = self

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                case.requests[self.path] += 1
                replies = case.responses[self.path]
                reply = replies[min(case.requests[self.path] - 1, len(replies) - 1)]
                if reply is None:
                    # Abort the TCP connection before returning an HTTP response.
                    self.connection.setsockopt(socket.SOL_SOCKET, socket.SO_LINGER, struct.pack("ii", 1, 0))
                    self.close_connection = True
                    self.connection.close()
                    return
                status, body = reply
                self.send_response(status)
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)

            def log_message(self, *_):
                pass

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join()
        self.temp.cleanup()

    def url(self, path):
        return f"http://127.0.0.1:{self.server.server_port}{path}"

    def fetch(self):
        environment = os.environ.copy()
        environment["NO_PROXY"] = "127.0.0.1,localhost"
        return subprocess.run(
            ["bash", "-euo", "pipefail", "-c", 'source "$1"; fetch "$2" "$3" "$4" "$5"',
             "native-fetch-test", str(FETCH), self.url("/primary"), SHA256,
             str(self.destination), self.url("/fallback")],
            text=True, capture_output=True, env=environment, timeout=30)

    def assert_no_partial(self):
        self.assertEqual([], list(self.destination.parent.glob(self.destination.name + ".part.*")))

    def test_503_primary_uses_pinned_fallback(self):
        self.responses = {"/primary": [(503, b"unavailable")], "/fallback": [(200, CONTENT)]}
        result = self.fetch()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(CONTENT, self.destination.read_bytes())
        self.assertGreater(self.requests["/primary"], 0)
        self.assertEqual(1, self.requests["/fallback"])
        self.assertIn(self.url("/primary"), result.stderr)
        self.assertIn(self.url("/fallback"), result.stderr)
        self.assert_no_partial()

    def test_connection_reset_is_retried_before_using_fallback(self):
        self.responses = {"/primary": [None, (200, CONTENT)], "/fallback": [(200, CONTENT)]}
        result = self.fetch()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(CONTENT, self.destination.read_bytes())
        self.assertGreater(self.requests["/primary"], 1)
        self.assertEqual(0, self.requests["/fallback"])
        self.assert_no_partial()

    def test_verified_cache_needs_no_network(self):
        self.destination.parent.mkdir()
        self.destination.write_bytes(CONTENT)
        result = self.fetch()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(CONTENT, self.destination.read_bytes())
        self.assertEqual({}, self.requests)
        self.assert_no_partial()

    def test_hash_mismatch_is_never_published_or_replaced_by_a_mirror(self):
        self.responses = {"/primary": [(200, b"wrong source")], "/fallback": [(200, CONTENT)]}
        result = self.fetch()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("pinned SHA-256", result.stderr)
        self.assertFalse(self.destination.exists())
        self.assertEqual(0, self.requests["/fallback"])
        self.assert_no_partial()

    def test_corrupt_cache_fails_without_network_or_replacement(self):
        self.destination.parent.mkdir()
        self.destination.write_bytes(b"corrupt cache")
        result = self.fetch()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("cache does not match", result.stderr)
        self.assertEqual(b"corrupt cache", self.destination.read_bytes())
        self.assertEqual({}, self.requests)
        self.assert_no_partial()

    def test_all_transport_failures_leave_no_published_or_partial_archive(self):
        self.responses = {"/primary": [(503, b"unavailable")], "/fallback": [(503, b"unavailable")]}
        result = self.fetch()
        self.assertNotEqual(0, result.returncode)
        self.assertGreater(self.requests["/primary"], 0)
        self.assertGreater(self.requests["/fallback"], 0)
        self.assertFalse(self.destination.exists())
        self.assert_no_partial()


if __name__ == "__main__":
    unittest.main()

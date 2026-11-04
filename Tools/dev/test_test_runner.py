"""Unity Editor 없이 EditMode 실행 스크립트의 종료 상태를 검사한다."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("test.sh")


class TestRunnerExitTests(unittest.TestCase):
    def run_case(self, status=None, launch_failure=False):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            dev = root / "Tools" / "dev"
            game = root / "Game"
            binaries = root / "bin"
            dev.mkdir(parents=True)
            (game / "Temp").mkdir(parents=True)
            binaries.mkdir()
            shutil.copyfile(SCRIPT, dev / "test.sh")
            (dev / "_env.sh").write_text(
                f'GAME="{game}"\nU="{binaries / "unity"}"\nPY="{shutil.which("python3")}"\n'
            )
            (binaries / "unity").write_text(
                '#!/bin/sh\n'
                'cp "$FIXTURE" Temp/pipeline_test_status.json\n'
                'if [ "$LAUNCH_FAILURE" = 1 ]; then exit 7; fi\n'
            )
            (binaries / "seq").write_text('#!/bin/sh\necho 1\n')
            (binaries / "sleep").write_text('#!/bin/sh\nexit 0\n')
            for file in binaries.iterdir():
                file.chmod(0o755)
            fixture = root / "fixture.json"
            if status is not None:
                fixture.write_text(json.dumps(status))
            env = dict(os.environ, PATH=str(binaries) + os.pathsep + os.environ["PATH"],
                       FIXTURE=str(fixture), LAUNCH_FAILURE="1" if launch_failure else "0")
            return subprocess.run(["bash", str(dev / "test.sh")], env=env,
                                  capture_output=True, text=True)

    def test_passed_suite_succeeds(self):
        result = self.run_case({"status": "completed", "summary": {"total": 1, "passed": 1,
                               "failed": 0, "skipped": 0, "inconclusive": 0},
                               "results": [{"Status": "Passed", "FullName": "Good", "Message": None}]})
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_failed_test_fails_script(self):
        result = self.run_case({"status": "completed", "summary": {"total": 1, "passed": 0,
                               "failed": 1, "skipped": 0, "inconclusive": 0},
                               "results": [{"Status": "Failed", "FullName": "Bad", "Message": "failure"}]})
        self.assertNotEqual(0, result.returncode)
        self.assertIn("Bad", result.stdout)

    def test_empty_or_unfinished_suite_fails(self):
        for status in [
            {"status": "completed", "summary": {"total": 0, "passed": 0, "failed": 0}, "results": []},
            {"status": "error", "message": "runner failed"},
            {"status": "running"},
        ]:
            with self.subTest(status=status):
                self.assertNotEqual(0, self.run_case(status).returncode)

    def test_unity_command_failure_fails_even_without_result_file(self):
        result = self.run_case({"status": "completed", "summary": {"total": 1, "passed": 1,
                               "failed": 0, "skipped": 0, "inconclusive": 0},
                               "results": [{"Status": "Passed"}]}, launch_failure=True)
        self.assertNotEqual(0, result.returncode)


if __name__ == "__main__":
    unittest.main()

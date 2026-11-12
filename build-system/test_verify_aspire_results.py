import tempfile
import unittest
from pathlib import Path

from verify_aspire_results import REQUIRED, verify


class VerifyAspireResultsTests(unittest.TestCase):
    def test_results(self):
        names = sorted(REQUIRED)
        good = [(name, "Passed") for name in names]
        cases = {
            "passed": (good, True),
            "missing case": (good[:1], False),
            "skipped": ([(names[0], "NotExecuted"), good[1]], False),
            "failed": ([(names[0], "Failed"), good[1]], False),
            "empty": ([], False),
            "duplicate": (good + [good[0]], False),
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.trx"
            with self.assertRaises(FileNotFoundError):
                verify(path)
            for label, (results, succeeds) in cases.items():
                with self.subTest(label=label):
                    entries = "".join(f'<UnitTestResult testName="{name}" outcome="{outcome}" />'
                                      for name, outcome in results)
                    path.write_text(f'<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>{entries}</Results></TestRun>')
                    if succeeds:
                        verify(path)
                    else:
                        with self.assertRaises(ValueError):
                            verify(path)


if __name__ == "__main__":
    unittest.main()

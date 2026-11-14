import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location("benchmark_backend", Path(__file__).resolve().parents[1] / "benchmark-backend.py")
benchmark = importlib.util.module_from_spec(spec)
spec.loader.exec_module(benchmark)


class BackendBenchmarkTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        self.head, self.base, self.output = root / "head", root / "base", root / "output"
        for checkout in [self.head, self.base]:
            (checkout / benchmark.PROJECT).parent.mkdir(parents=True)
            (checkout / benchmark.PROJECT).write_text(
                '<Project><ItemGroup><PackageReference Include="KeepBaseDependency" Version="1.2.3" /></ItemGroup></Project>')
        for relative in benchmark.HARNESS:
            (self.head / relative).parent.mkdir(parents=True, exist_ok=True)
            (self.head / relative).write_text('{}' if relative.suffix == '.json' else '// benchmark harness')

    def test_install_into_old_base_preserves_its_project_dependencies(self):
        benchmark.install_harness(self.head, self.base)
        root = ET.parse(self.base / benchmark.PROJECT).getroot()
        self.assertEqual(root.find('.//PackageReference').get('Version'), '1.2.3')
        self.assertEqual(root.find('.//None').get('Link'), 'Performance/backend-expectations.json')
        for relative in benchmark.HARNESS:
            self.assertEqual((self.head / relative).read_bytes(), (self.base / relative).read_bytes())

    def test_existing_resource_glob_is_not_duplicated(self):
        (self.base / benchmark.PROJECT).write_text(
            '<Project><ItemGroup><None Include="../fixtures/performance/*" Link="Performance/%(Filename)%(Extension)" /></ItemGroup></Project>')
        benchmark.install_harness(self.head, self.base)
        self.assertEqual(len(ET.parse(self.base / benchmark.PROJECT).findall('.//None')), 1)

    def run_fake(self, fail_base=False):
        order = []

        def command(args, cwd, log, env=None):
            log.write_text('test log')
            if 'build' in args and cwd == self.base and fail_base:
                return 1
            if 'test' in args:
                order.append(cwd.name)
                # A huge regression must still be reported without a threshold failure.
                row = {'name': 'minutes-small', 'operations': 64,
                       'allocatedBytes': 10**12 if cwd == self.head else 1000}
                (Path(env['FREETRAIN_BACKEND_PERFORMANCE_OUTPUT']) / 'minutes-small.json').write_text(json.dumps(row))
            return 0

        with patch.object(benchmark, 'command', side_effect=command), patch.object(benchmark, 'sha', return_value='a' * 40):
            report = benchmark.run(self.base, self.head, self.output, 3)
        return report, order

    def test_runs_alternate_and_numerical_regressions_are_informational(self):
        report, order = self.run_fake()
        self.assertEqual(order, ['base', 'head', 'head', 'base', 'base', 'head'])
        self.assertEqual(len(report['runs']), 6)
        saved = json.loads((self.output / 'report.json').read_text())
        self.assertEqual(saved, report)
        self.assertEqual(report['runs'][1]['measurements'][0]['allocatedBytes'], 10**12)

    def test_failed_base_build_keeps_head_results_and_partial_report(self):
        report, order = self.run_fake(fail_base=True)
        self.assertEqual(order, ['head'] * 3)
        self.assertEqual(report['builds']['base'], 1)
        self.assertEqual(len(report['runs']), 3)

    def test_triggering_pr_identity_is_carried_to_the_publisher_report(self):
        with patch.dict(benchmark.os.environ, {'BENCHMARK_PR_NUMBER': '7'}):
            report, _ = self.run_fake()
        self.assertEqual(report['pullRequest'], 7)


if __name__ == '__main__':
    unittest.main()

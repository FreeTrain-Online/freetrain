#!/usr/bin/env python3
"""Run the PR's identical benchmark harness against two isolated checkouts."""

import argparse
import fnmatch
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import xml.etree.ElementTree as ET

PROJECT = Path("tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj")
HARNESS = [
    Path("tests/FreeTrain.Online.Tests/BackendPerformanceTests.cs"),
    Path("tests/FreeTrain.Online.Tests/BackendPerformanceFixture.cs"),
    Path("tests/fixtures/performance/backend-expectations.json"),
]


def install_harness(head, base):
    """Change only benchmark sources/resources in the disposable base checkout."""
    for relative in HARNESS:
        target = base / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(head / relative, target)
    project = base / PROJECT
    tree = ET.parse(project)
    include = "../fixtures/performance/backend-expectations.json"
    if not any(fnmatch.fnmatchcase(include, node.get("Include", ""))
               for node in tree.getroot().iter("None")):
        group = ET.SubElement(tree.getroot(), "ItemGroup")
        ET.SubElement(group, "None", Include=include,
                      Link="Performance/backend-expectations.json",
                      CopyToOutputDirectory="PreserveNewest")
        tree.write(project, encoding="unicode")


def command(args, cwd, log, env=None):
    with log.open("w") as stream:
        return subprocess.run(args, cwd=cwd, env=env, stdout=stream,
                              stderr=subprocess.STDOUT, check=False).returncode


def sha(checkout):
    return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=checkout, text=True).strip()


def run(base, head, output, repetitions):
    output.mkdir(parents=True, exist_ok=True)
    report = {
        "schemaVersion": 1, "baseSha": sha(base), "headSha": sha(head),
        "pullRequest": int(os.environ["BENCHMARK_PR_NUMBER"]) if os.environ.get("BENCHMARK_PR_NUMBER") else None,
        "repetitions": repetitions,
        "environment": {"os": platform.system(), "architecture": platform.machine(),
                        "logicalCpus": os.cpu_count()},
        "builds": {}, "runs": [],
    }

    def save():
        (output / "report.json").write_text(json.dumps(report, indent=2) + "\n")

    save()
    try:
        install_harness(head, base)
        for side, checkout in (("base", base), ("head", head)):
            command(["dotnet", "--info"], checkout, output / f"{side}-dotnet.log")
            restored = command(["dotnet", "restore", str(PROJECT), "--locked-mode"],
                               checkout, output / f"{side}-restore.log")
            built = restored or command([
                "dotnet", "build", str(PROJECT), "-c", "Release", "--no-restore",
                "-m:1", "-nodeReuse:false", "-p:UseSharedCompilation=false",
            ], checkout, output / f"{side}-build.log")
            report["builds"][side] = built
            save()
        # Alternate base/head order to reduce a systematic runner-order bias.
        # Every sample gets a fresh test process and the same fixture/warmup.
        for repetition in range(1, repetitions + 1):
            order = ("base", "head") if repetition % 2 else ("head", "base")
            for side in order:
                if report["builds"][side] != 0:
                    continue
                checkout = base if side == "base" else head
                directory = output / f"{side}-{repetition}"
                directory.mkdir()
                env = dict(os.environ, FREETRAIN_BACKEND_PERFORMANCE_OUTPUT=str(directory))
                code = command([
                    "dotnet", "test", str(PROJECT), "-c", "Release", "--no-build", "--no-restore",
                    "--filter", "Category=BackendPerformance",
                    "--logger", "trx;LogFileName=benchmarks.trx",
                    "--results-directory", str(directory),
                ], checkout, directory / "test.log", env)
                measurements = [json.loads(p.read_text()) for p in sorted(directory.glob("*.json"))]
                report["runs"].append({"side": side, "repetition": repetition,
                                       "exitCode": code, "measurements": measurements})
                save()
                print(f"{side} sample {repetition}: exit {code}, {len(measurements)} measurements", flush=True)
    except Exception as error:
        report["error"] = type(error).__name__
        raise
    finally:
        save()
    # Numerical changes never determine the exit code. Build/test errors stay
    # visible in the report; the normal C# workflow enforces head correctness.
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", type=Path, required=True)
    parser.add_argument("--head", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--repetitions", type=int, default=3, choices=range(1, 11))
    args = parser.parse_args()
    base, head, output = args.base.resolve(), args.head.resolve(), args.output.resolve()
    if base == head:
        parser.error("--base and --head must be separate checkouts")
    if output.exists() and any(output.iterdir()):
        parser.error("--output must be a new or empty directory")
    for checkout in (base, head):
        top = subprocess.check_output(["git", "rev-parse", "--show-toplevel"], cwd=checkout, text=True).strip()
        if Path(top).resolve() != checkout:
            parser.error("--base and --head must point to checkout roots")
    if subprocess.check_output(["git", "status", "--porcelain"], cwd=base, text=True).strip():
        parser.error("--base must be clean; use a fresh disposable checkout")
    run(base, head, output, args.repetitions)


if __name__ == "__main__":
    main()

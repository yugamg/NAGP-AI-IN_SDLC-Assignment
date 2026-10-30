"""Summarise CI history from ops/pipeline/run-timings.csv.

Usage: python3 -I docs/measurement/ci_stats.py ops/pipeline/run-timings.csv
Prints per-run duration, the median and p90, the red-run rate and the slowest steps.
"""
import csv
import statistics
import sys
from collections import defaultdict


def main(timings_path: str) -> int:
    rows = list(csv.DictReader(open(timings_path)))
    runs = defaultdict(list)
    for row in rows:
        runs[int(row["run_number"])].append(row)

    durations = []
    for run_number, steps in sorted(runs.items()):
        minutes = sum(int(step["duration_seconds"]) for step in steps) / 60
        durations.append(minutes)
        first = steps[0]
        print(f"run {run_number:2d} {first['started_at']} {first['branch']:28s} {first['event']:12s} "
              f"{first['conclusion']:8s} {minutes:5.1f} min, {len(steps)} steps")

    ordered = sorted(durations)
    p90 = ordered[max(0, round(0.9 * len(ordered)) - 1)]
    red = [steps[0] for steps in runs.values() if steps[0]["conclusion"] != "success"]
    main_runs = [steps[0] for steps in runs.values() if steps[0]["branch"] == "main"]
    print(f"\n{len(runs)} runs, {runs[min(runs)][0]['started_at']} to {runs[max(runs)][0]['started_at']}")
    print(f"duration median {statistics.median(durations):.1f} min, p90 {p90:.1f} min, "
          f"range {min(durations):.1f}-{max(durations):.1f} min")
    print(f"red runs {len(red)} of {len(runs)}; pushes to main {len(main_runs)}, "
          f"red on main {sum(run['conclusion'] != 'success' for run in main_runs)}")

    by_step = defaultdict(list)
    for row in rows:
        by_step[row["step"]].append(int(row["duration_seconds"]))
    total_median = sum(statistics.median(values) for values in by_step.values())
    print("\nslowest steps (median seconds, share of summed medians):")
    for step, values in sorted(by_step.items(), key=lambda item: -statistics.median(item[1]))[:5]:
        median = statistics.median(values)
        print(f"  {step:40s} {median:5.0f}s {median / total_median:5.1%}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))

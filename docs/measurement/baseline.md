# Delivery baseline before the on-call rotation starts

**Captured:** 2026-10-29.

**The three measures:**
- lead time for changes;
- change failure rate;
- review turnaround.

**Captured today:** the two that the repository and its CI history allow,
under [Captured today](#captured-today).

All the source data is the invented `ops/` evidence
(`ops/README.md` line 22). The method carries over to the real repository;
the numbers do not.

## Plan

### 1. Lead time for changes

**Definition.** For each change, the time from its first commit to that commit
running in production. Report the median and p90 per month, never the mean,
because long-tail changes dominate a mean.

**Start event:** the first commit on the pull request.

```bash
gh pr list --repo <owner>/<repo> --state merged --base main --limit 500 \
  --json number,mergedAt,mergeCommit,commits \
  --jq '.[] | {pr: .number, merge_sha: .mergeCommit.oid, first_commit: (.commits | map(.committedDate) | min)}'
```

**End event:** the production slot swap for the release that contains the
merge commit. The app logs it as
`[Deploy] Swap completed release=… commit=…`
(`ops/incidents/INC-2041/app-logs.txt` lines 70-71). In the logging
workspace, the query is:

```kusto
AppServiceConsoleLogs
| where ResultDescription has "[Deploy] Swap completed"
| parse ResultDescription with * "release=" Release " " *
| project TimeGenerated, Release
```

The table name depends on how App Service diagnostics are routed, so check it.
Map the release to its commit with the `Starting deployment … commit=` line
just before it.

**Join:** the merge commit must be an ancestor of the deployed commit:

```bash
git merge-base --is-ancestor <merge_sha> <deployed_sha>
```

Take the first deploy for which that is true.

**Known gap:**
- `run-timings.csv` records no commit SHA, so CI runs cannot be joined to
  commits today.
- **Add `github.sha` to the timing export** before the baseline window starts.

### 2. Change failure rate

**Definition.** Production deploys that lead to an incident, rollback or
hotfix, divided by all production deploys. Report it per month and as a
running 90-day figure.

**Deploys:** the same `Swap completed` query as above, one row per release.

**Failures:** incidents whose record names a triggering release.

- The incident template needs a `Triggered by release:` field. Neither
  INC-2041 nor INC-2047 has one today.
- Until then, attribute an incident to a release only if both hold:
  - it started within 24 hours of that release's deploy;
  - a fix or rollback of that release closed it.
- Record the attribution as a judgement, not as a fact.

### 3. Review turnaround

**Definition.** The time from a pull request being opened, or marked ready for
review, to its first submitted review by someone other than the author.
Report the median and p90, and split them by author tenure, because two of
the six engineers are new.

**Source:** the GitHub API.

```bash
gh pr list --repo <owner>/<repo> --state all --base main --limit 500 \
  --json number,author,createdAt,isDraft,reviews \
  --jq '.[] | select(.isDraft | not) | .author.login as $author
        | {pr: .number, author: $author, opened: .createdAt,
           first_review: ([.reviews[] | select(.author.login != $author) | .submittedAt] | min)}'
```

**Caveat on the start time.** `createdAt` is when the PR was opened, so a
draft that is later marked ready starts the clock too early. When drafts are
common, take the `ready_for_review` event from
`gh api repos/<owner>/<repo>/issues/<n>/timeline` instead.

**Not available today.** This folder is not a git repository and has no
pull-request data. `run-timings.csv` shows that pull requests exist, as
`pull_request` runs, but not when they were opened or reviewed.

## Captured today

### Lead time: one complete sample, and the CI component for every run

- **End to end, one change.** Commit `7c41ab9` was committed at
  2026-01-13 17:22:08Z (`deploy-diff.patch` line 3). The production swap for
  2.14.0 completed at 2026-01-14 09:58:41Z (`app-logs.txt` line 71). Lead
  time: **16 h 37 min.** One sample says nothing about the distribution. It
  shows the start and end events exist and can be joined.
- **CI component, 20 runs.** These are measured. The 31 minutes are a floor
  under every change's lead time.

```bash
python3 -I docs/measurement/ci_stats.py ops/pipeline/run-timings.csv
```

| Measure | Value |
| --- | --- |
| Window | 2026-01-19 to 2026-02-02, 20 runs |
| Pipeline duration, median | **31.3 min** |
| Pipeline duration, p90 | 34.5 min (range 28.4–35.1) |
| Slowest step | Static analysis, median 480 s, **26%** of pipeline time |
| Next slowest steps | End-to-end tests 220 s (12%), container build 163 s (9%) |
| Red runs | 2 of 20 (10%, 95% CI 3–30%), both on pull requests. Pushes to `main`: 8 runs, 0 red |

### Change failure rate: 2 of 4 known releases

| Release | Deployed | Evidence | Failed? |
| --- | --- | --- | --- |
| 2.14.0 | 2026-01-14 09:58Z (`app-logs.txt` line 71) | INC-2041 alert at 10:12Z, 14 minutes after the swap. The next release, 2.14.3, is "Retry policy for the PayEx client, pooled handler" (`recent-changes.md` line 7), which fixes the client 2.14.0 introduced | **Yes** (judgement) |
| 2.14.3 | 2026-01-16 | No incident in the evidence | No |
| 2.15.0 | 2026-01-21 | No incident in the evidence | No |
| 2.15.1 | 2026-01-28 08:42Z (`publisher-logs.txt` line 13) | Events renamed from 09:03Z. INC-2047 followed. The release note itself warned subscribers (`recent-changes.md` lines 25-26) | **Yes** (judgement) |

**Change failure rate: 2 of 4, so 50%,** with a 95% confidence interval of
15–85%.

- **If 2.14.1 and 2.14.2 also shipped** (the evidence neither shows nor rules
  them out), it is 2 of 6, so 33% (10–70%).
- **Read it as "high, and badly measured".** Both failures were contract or
  dependency changes that the pipeline passed, which is the more useful
  finding.

### Review turnaround: not capturable today

See [Review turnaround](#3-review-turnaround) above for what is missing and
the query to run once the GitHub repository is available.

# Submission: AI in SDLC home assignment

Each hand-in item, the file that holds it, and the command that re-checks it.
Everything under `ops/` describes a planned deployment with invented data
(`ops/README.md`). The code itself is the small demo app.

## Prerequisites

- .NET 8 SDK
- Python 3, for the checker scripts
- `jq`, for one query in the measurement plan

On macOS, port 5000 is taken by AirPlay, so run the app on 5050:

```bash
dotnet run --project backend/src/InvoiceApp.Api --no-launch-profile --urls http://localhost:5050
```

## Hand-in items

| # | Item | Where | Re-check |
| --- | --- | --- | --- |
| 1 | Mermaid source for the as-built and target diagrams, the citation list and the inferred-items list | [docs/architecture/as-built.md](docs/architecture/as-built.md), [docs/architecture/target-state.md](docs/architecture/target-state.md). Renders from the Mermaid MCP are in [docs/architecture/rendered/](docs/architecture/rendered/) | Citation checker, below |
| 2 | One architecture test, with its command | [backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs](backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs). Evidence that it can fail is in as-built.md, "Architecture test" | `dotnet test backend/InvoiceApp.sln --filter "Category=Architecture"` |
| 3 | Exactly-once decision record, with "Where the agent was wrong or useless" | [docs/adr/ADR-001-payex-exactly-once.md](docs/adr/ADR-001-payex-exactly-once.md) | Citation checker, below |
| 4 | One-page autonomy policy, with the day-one level | [docs/policy/autonomy-incident-first-response.md](docs/policy/autonomy-incident-first-response.md). Worked example: [docs/policy/examples/INC-2047-first-response-L1.md](docs/policy/examples/INC-2047-first-response-L1.md) | `python3 -I docs/architecture/check_citations.py docs/policy/examples/INC-2047-first-response-L1.md` |
| 5 | Two added eval cases, and the results table for three runs plus the changed-variable run | Cases: [ops/evals/cases/case-11.diff](ops/evals/cases/case-11.diff) (defect), [ops/evals/cases/case-12.diff](ops/evals/cases/case-12.diff) (clean). Results: [ops/evals/results.md](ops/evals/results.md), [ops/evals/results.csv](ops/evals/results.csv). Key: [ops/evals/answer-key.md](ops/evals/answer-key.md). Raw reviews: [ops/evals/runs/](ops/evals/runs/) | `ops/evals/run_reviews.sh <label> 3 <model>` (needs a signed-in `claude` CLI) |
| 6 | Risk audit, with a decision recorded | [docs/risk/lethal-trifecta-audit.md](docs/risk/lethal-trifecta-audit.md). Changes applied: [.claude/settings.json](.claude/settings.json), [.mcp.json](.mcp.json) | — |
| 7 | Measurement plan, with the two numbers captured | [docs/measurement/baseline.md](docs/measurement/baseline.md) | `python3 -I docs/measurement/ci_stats.py ops/pipeline/run-timings.csv` |
| 8 | 8–10 minute demo video | *(link to be added)*. Outline: [docs/demo-video-outline.md](docs/demo-video-outline.md) | — |
| 9 | AI augmentation logs | [docs/ai-augmentation-log.md](docs/ai-augmentation-log.md). The raw session transcript is exported separately as a zip | — |

**Citation checker,** covering items 1 and 3:

```bash
python3 -I docs/architecture/check_citations.py docs/architecture/as-built.md docs/architecture/target-state.md docs/adr/ADR-001-payex-exactly-once.md
```

**Full test suite:**

```bash
dotnet test backend/InvoiceApp.sln
```

## Key results

| Task | Result |
| --- | --- |
| 1 | 38 checked citations, 12 inferred items, 11 corrections. The architecture test has 3 checks, and 7 seeded violations prove what it catches |
| 2 | Outbox plus idempotency key, conditional on PayEx deduplicating (Q1). All three stress tests are recorded, and each changed the design |
| 3 | Incident first-response summaries: day-one level L1, with an experienced reviewer for new engineers' first two incidents |
| 4 | Sonnet and Haiku both caught 21/21 defects with 0/9 false alarms, so no measurable difference at this sample size. The eval found three real gaps in the Task 1 test, which are now fixed |
| 5 | All trifecta legs present; external communication removed for repo work. CI median 31.3 min. Change failure rate 2 of 4 releases |

## Still open

- **ADR-001:** review the three agent-drafted verdicts, and rewrite the
  record in the author's own words.
- **ADR-001:** fix the 4xx handling (429 retryable) before build.
- **R1:** turn off unneeded connectors for this project.
- **Demo video** link.

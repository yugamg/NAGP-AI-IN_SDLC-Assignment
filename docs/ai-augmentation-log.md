# AI augmentation log

**What this is.** A record of how an AI agent was used for this assignment:
what it did, which decisions stayed with the human, and every place where the
agent was wrong, over-confident or useless, with how each was caught.

**Setup.**
- **Agent:** Claude Code in the Claude desktop app (model Opus 5.5).
- **Session:** one session, 2026-10-29, working in this repository.
- **Raw transcript:** exported from the app as a zip holding the
  conversation, the subagent transcripts and session metadata. It is kept
  outside the repository. Review it before sharing, because it includes tool
  output from the author's connected accounts.

**How to read the tables.**
- **H** = a decision the human made.
- **A** = work the agent did.
- **✗** = an agent error, with how it was caught.
- **!** = a problem in the environment or the provided material, not an
  agent error.

## Summary

Counted from the rows below.

| | Count |
| --- | --- |
| Agent errors (✗) | **26** |
| — caught by something that ran (parser, checker, seeded test, experiment, header script, a tool's `--help`) | 7 |
| — caught by a stress test or eval the human asked for | 5 |
| — caught by the agent re-checking its claim against evidence (`grep`, recount, re-reading the source) | 11 |
| — not caught, only disclosed (limits of what the agent can know) | 3 |
| Environment or material problems (!) | 3 |
| Decisions the human made (H) | 8 |

**The lesson that recurred.** The errors that would have mattered most were
caught by something that ran, or by a challenge the human set:
- the architecture-test gaps, by seeded violations and the eval;
- the worker design, by the stress tests;
- the Playwright claim, by its own `--help`.

The agent's own re-checks caught the miscounts and over-claims, but only after
the first draft had already stated them as fact.

## Set-up and orientation

| | What happened |
| --- | --- |
| H | Asked for an explanation of the assignment, a working run of the repo, and task-by-task work. Asked what the two demo users can do |
| A | Installed the .NET 8 SDK into `~/.dotnet`, which it was missing. Ran the app on port 5050, because macOS AirPlay holds 5000. Added `.claude/launch.json` |
| ! | **The repo's documented run command does not work on this Mac** (`http://localhost:5000`). Found when the port was refused. Recorded as correction 7 in the as-built doc |

## Task 1: as-built diagrams and architecture test

| | What happened |
| --- | --- |
| A | Read every source file. Drew the container view and the approval sequence, each element citing a file and line range |
| A | Wrote `check_citations.py`, so every citation is checked against the file, not trusted. Proved it fails on a wrong line range and on a missing ID |
| A | Tested inferred claims against the running app: the race, session expiry, error codes, self-approval |
| H | Chose to render the diagrams through the Mermaid MCP once the connector was reconnected |
| ✗ | **Stated a double-approval race as fact.** 30 rounds of 8 concurrent approvals never reproduced it. Moved to "inferred" with the evidence (I4) |
| ✗ | **Sequence diagram invalid:** a `;` inside a Mermaid note ended the statement. Caught by the Mermaid parser (correction 8) |
| ✗ | **Wrong line range for citation T11.** Caught by the citation checker (correction 10) |
| ✗ | **Overlapping subgraph titles** in the first render. Caught visually; titles shortened |
| ✗ | **Architecture test v1 missed two violations:** a `const` from ASP.NET Core (the compiler inlines it, so no reference is emitted) and an unused framework reference. Caught by seeding violations in scratch copies. A project-file check was added |
| ✗ | **Architecture test v2 still had three gaps:** a Web SDK switch, `<Reference>` items, `Directory.Build.props`. Missed by the agent and **caught by the Task 4 eval** (see below) |
| A | Found that `AGENTS.md` is wrong about the code in three places, and that `ops/` describes a different, planned version of the routes |

## Task 2: exactly-once decision record (ADR-001)

| | What happened |
| --- | --- |
| A | Mined the INC-2041 evidence: three different timeouts (10, 30, 100 s), 25 timeouts and 81 socket errors after the release, invoices approved more than once |
| A | Compared idempotency key, outbox and optimistic reconciliation. Proposed an outbox carrying the idempotency key. Set the open question Q1, whether PayEx deduplicates, as the precondition for everything |
| ✗ | **Counted 46 repeat approvals;** the right figure is 33. The count included the window before 2.14.0, when there was no PayEx call. Caught by splitting the log at the deploy line |
| ✗ | **Explained the repeats by per-instance memory.** The logs disproved it: 13 repeats happened on the same instance |
| ✗ | **Wrote "paid twice" before checking for success log lines;** there are none. Downgraded to an inference from the code path |
| ✗ | **Useless on the key fact:** it cannot know PayEx behaviour. The decision was made conditional on Q1 |
| H | Ran the three stress tests: a preference for another option, a 3 a.m. failure, cost |
| ✗ | **Wrote its own stress-test prompts,** sent with one click. Flagged in the record as a weaker test than intended |
| ✗ | **Over-built the worker** (a per-row lease across 3 instances). Stress test 1 cut it to one active worker |
| ✗ | **That simplification created a new failure,** a hung lock holder blocking all payments. Caught by stress test 2 |
| ✗ | **Alert design would page a new engineer** at 3 a.m. for a PayEx outage they couldn't fix, and had no case for a deploy breaking every call. Caught by stress test 2 |
| ✗ | **Made the rejected option look cheap** ("one table"). Costing it line by line in stress test 3 showed it needs a sweeper |
| ✗ | **Cost numbers are guesses:** no team history exists. Labelled measured, calculated or guessed. Azure prices were refused rather than invented |
| H | Asked the agent to draft the three stress-test verdicts. They are marked in the ADR as agent-drafted, pending the author's review. The rewrite in the author's own words stays with the author |
| ✗ | **All three stress tests missed one error-handling gap,** found while drafting the verdicts: every 4xx goes to `Rejected, no retry`, which mishandles a 429 (rate limited) and possibly a duplicate-key 409. Recorded as wrong-or-useless entry 11 and as an action before build |

## Task 3: autonomy policy

| | What happened |
| --- | --- |
| A | Chose first-response incident summaries, the workflow with real evidence in the repo. Defined L0–L3, task ceilings, controls, promotion evidence and approvers. Day one: L1 plus an experienced reviewer for new engineers |
| A | Wrote a worked L1 draft for INC-2047. It passes 21 of 21 citation checks and has no e-mail addresses |
| ✗ | **Stated a Service Bus behaviour as fact without evidence** while writing the example. The policy gained a rule: such claims must be marked "general knowledge, verify" |
| ✗ | **Cited a Service Bus permissions detail from memory** (that the peek right also allows delete). Left in the policy marked "to confirm", not verified |

## Task 4: review evaluation

| | What happened |
| --- | --- |
| A | Wrote the answer key **before** any run. Added case 11 (a real defect, from INC-2041) and case 12 (a clean case, the Task 1 test). Built `run_reviews.sh` for fresh, tool-less CLI reviews |
| ! | **The harness could not run:** the standalone CLI was not signed in, even from the author's terminal. Probing where credentials are stored was **blocked by the safety classifier**, correctly |
| H | Chose to use subagents instead |
| A | Ran 72 reviews (Sonnet against Haiku, 3 runs each) and moved the answer key out of the repo during the runs. Result: both caught 21/21 defects with 0/9 false alarms, so no measurable difference |
| ✗ | **Planned variable (AGENTS.md) was unusable:** subagents already load it, and 25 reviews cite it. Switched the variable to the model and recorded why |
| ✗ | **Isolation weaker than the eval asks:** subagents had tools, and the agent scored its own key. Both recorded as weaknesses |
| ✗ | **Got a count wrong in the results:** 9 of 12 malformed hunk headers became 8, and the AGENTS.md citation count was corrected. Caught by re-counting before publishing |
| ✗ | **Its own case 11 has malformed hunk headers.** Found by a header-checking script |
| A | **The eval found real gaps in the agent's own Task 1 test.** |
| H | Chose to fix the gaps |
| A | Added a third check on restore output. Proved each gap with a seeded violation: the old test passed, the new one fails. Suite 10/10 in Debug and Release |

## Task 5: risk audit and measurement plan

| | What happened |
| --- | --- |
| ! | **`ops/README.md` has no agent setup to audit,** though the brief says it does |
| H | Chose to audit the setup actually used for this assignment |
| A | Inventoried the session's tools: all three trifecta legs are present. Decided to remove external communication for repo work (R1–R4), with the residual risk accepted in writing |
| A | Captured two numbers: CI median 31.3 min (20 runs, script included) and change failure rate 2 of 4 releases (50%, 95% CI 15–85%). Plus one end-to-end lead-time sample of 16 h 37 min |
| ✗ | **First review-turnaround query was wrong:** it compared each review's author with itself. Rewritten and tested on a sample |
| H | Chose to apply the repo-side changes. Asked the agent to put the author's name on the risk acceptance |
| ✗ | **Claimed Playwright's origin list would stop external browsing.** Its own `--help` says it is not a security boundary. The audit was corrected and the browser added to residual risk |
| ✗ | **Deny rules may not load this session,** because the settings file was created mid-session. Recorded |

## What the agent did well, with evidence

- **It turned claims into checks.** The citation checker, seeded violations,
  the hunk-header checker and the CI stats script all re-run, so a reviewer
  does not have to trust the prose.
- **It downgraded its own claims when the evidence did not hold:** the race,
  "paid twice", and the count of 46.
- **It kept decisions that are not the agent's with the human:** workflow
  choices, the audit target, settings changes, and the final ADR wording.

## What still needs a human

- **Second scoring of the eval.** Re-score the clean and ambiguous rows in
  `ops/evals/results.csv`.
- **ADR-001 verdicts and rewrite,** and the risk-acceptance signature.
- **A real answer to Q1** from PayEx.

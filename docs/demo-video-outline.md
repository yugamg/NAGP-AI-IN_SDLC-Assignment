# Demo video outline: 9 minutes 30 seconds

**The thread through the whole video:** every claim was measured, not
assumed, and here is the agent being wrong and getting caught. Show commands
running, not just documents.

## Before recording

- **Make text readable:** terminal font at 16–18 pt, editor zoomed in.
- **Open these files in tabs, in order:**
  1. `SUBMISSION.md`
  2. `docs/architecture/rendered/as-built-container.png`
  3. `docs/architecture/as-built.md`
  4. `docs/adr/ADR-001-payex-exactly-once.md`
  5. `docs/architecture/rendered/adr-001-submission-sequence.png`
  6. `docs/policy/autonomy-incident-first-response.md`
  7. `docs/policy/examples/INC-2047-first-response-L1.md`
  8. `ops/evals/results.md`
  9. `docs/risk/lethal-trifecta-audit.md`
  10. `docs/measurement/baseline.md`
  11. `docs/ai-augmentation-log.md`
- **Run once first** so the build is warm and the live runs are quick:
  `dotnet test backend/InvoiceApp.sln`
- **Keep private things off screen:** close mail, chat and any panel showing
  account connectors.
- **Recording:** Teams, or any screen recorder that produces MP4. macOS
  `Cmd+Shift+5` produces `.mov`; export or convert it to MP4.

## Running order

### 0:00–0:40 · Set-up

- **Show:** `SUBMISSION.md`.
- **Say:**
  - The service becomes tier 2 in January, with PayEx, a database and on-call.
  - Five deliverables. One rule throughout: back every claim with something
    that runs.

### 0:40–2:30 · Task 1: as-built and architecture test

1. **Show** the rendered container view (Mermaid MCP). **Say:** every box
   carries a citation ID.
2. **Run:**

   ```bash
   python3 -I docs/architecture/check_citations.py docs/architecture/as-built.md docs/architecture/target-state.md docs/adr/ADR-001-payex-exactly-once.md
   ```

   **Say:** 55 citations checked against the actual files.
3. **Live failure (about 40 s).** Edit
   `backend/src/InvoiceApp.Domain/InvoiceApp.Domain.csproj` and add this line
   just before `</Project>`:

   ```xml
   <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>
   ```

   Then run:

   ```bash
   dotnet test backend/InvoiceApp.sln --filter "Category=Architecture"
   ```

   Two of the three checks go red: "FrameworkReference Microsoft.AspNetCore.App
   in InvoiceApp.Domain.csproj" and "Restore resolved: framework
   Microsoft.AspNetCore.App". **Delete the line** and re-run to show green.
   This is seeded case c from the evidence table. A Web SDK switch on its own
   may not compile, so it is not used for the live demo.
4. **Say,** in one line each:
   - `AGENTS.md` is wrong about the code in three places.
   - `ops/` uses a different approval route from the code.
   - A user can approve their own invoice (tested live).

### 2:30–4:30 · Task 2: exactly-once ADR

1. **Show** the INC-2041 context. **Say:**
   - 33 invoices returned 200 on approve more than once after release 2.14.0.
   - There were three different timeouts: 10, 30 and 100 seconds.
   - 25 PayEx calls timed out, and each timeout is an unknown outcome.
2. **Show** the options comparison, then the decision: an outbox carrying the
   idempotency key.
3. **Say the key point:** nobody can deliver exactly once. Exactly-once
   *effect* needs PayEx to deduplicate. That is question Q1, and the decision
   is conditional on it.
4. **Show** the stress-test sections. Each changed the design:
   - one active worker;
   - a stuck-worker health check, and a stop on systemic rejections;
   - option A's real cost.
5. **Show** "Where the agent was wrong or useless". Pick two to say aloud:
   - the hung lock holder its own simplification created;
   - it cannot know how PayEx behaves.

### 4:30–5:45 · Task 3: autonomy policy

1. **Show** the levels and task-ceilings tables.
2. **Say:** day one is L1, and an experienced reviewer checks new engineers'
   first two incidents. That is because the human reviewer is the control at
   L1.
3. **Show** the INC-2047 example. **Point at** "Health check ok" with 0
   messages processed. **Say:** that is why health claims must cite a number.
   Every line cites a source, and it passes 21 of 21 citation checks.

### 5:45–7:30 · Task 4: review eval

1. **Say:**
   - The answer key was written before any run.
   - 72 fresh reviews: Sonnet against Haiku, 3 runs each.
   - The key was hidden from the reviewers.
2. **Show** the results table. **Say:**
   - Both models caught 21/21 with 0/9 false alarms. No measurable difference.
   - At 7 defect cases and 3 clean ones, the false-alarm interval is 0–56%,
     and there is a ceiling effect.
3. **The twist.** Case 12 was the Task 1 test, entered as a clean case. The
   reviewers found three real ways around it. **Show** the old-versus-new
   table in `as-built.md`. **Say:** each gap was seeded, the old test passed
   it, and the new one fails it.

### 7:30–9:00 · Task 5: risk audit and measurement

1. **Show** the trifecta inventory. **Say:**
   - All three legs are in one session: private data, untrusted content and
     external communication.
   - What held today was behaviour: the safety classifier blocked the agent's
     own probe for where credentials are stored.
   - The decision removes external communication.
2. **Say:** the agent claimed Playwright's origin list would stop browsing out.
   Its own `--help` says it is not a security boundary. The audit was
   corrected.
3. **Run:**

   ```bash
   python3 -I docs/measurement/ci_stats.py ops/pipeline/run-timings.csv
   ```

   **Say:**
   - CI median 31.3 min, a quarter of it static analysis.
   - Change failure rate 2 of 4 releases, with a 15–85% interval.
   - Review turnaround cannot be captured: there is no PR data. The query is
     written and ready.

### 9:00–9:30 · AI augmentation log and close

- **Show** the summary table in `docs/ai-augmentation-log.md`. **Say:**
  - 25 agent errors in all.
  - The worst ones were caught by something that ran, or by a challenge the
    human set.
- **List what is still open:** the ADR verdicts, the risk sign-off, and Q1
  for PayEx.

## If time is short

**Cut first:**
- the live architecture-test failure (show the evidence table instead);
- the Task 3 example.

**Never cut:** the Task 4 twist, where the eval caught the Task 1 test. It
connects the tasks and shows the method working.

# Answer key used for scoring

**Written before any review run, and not changed after seeing the outputs.**
This is our own judgement of each diff. The facilitator's answers are not in
the repository, so where this key and the session runbook disagree, the
runbook wins and the scores must be recomputed.

| Case | Key | The defect a finding must name to count as caught |
| --- | --- | --- |
| 01 | Defect | `BulkApprove` does not go through `Handle`. One bad or already-decided ID throws midway, so the earlier invoices stay approved and the caller gets a 500 without knowing which ones went through. **Also accepted:** `Ids` is nullable and is iterated without a check, so a null throws. |
| 02 | Defect | SQL injection: `customer` and `status` are interpolated into the SQL string. |
| 03 | **Ambiguous** | The rounding change. The discount is now truncated, so results move by up to 0.01. Example: 10.00 at 33.37% gives 6.67, where the old code gave 6.66. Whether that is a bug or a deliberate "never over-discount" rule cannot be told from the diff. Scored separately, not in the headline rates. |
| 04 | Clean | Rename of `_repository` to `_invoices`, with every usage in the diff updated. |
| 05 | Defect | `Delete` has no `[RequireRole("Admin")]`, so any signed-in user can delete invoices. The neighbouring `Update` has it. |
| 06 | Clean | Three tests that match the service's behaviour: whitespace reason throws `ArgumentException`, approve records approver and time, unknown ID throws `KeyNotFoundException`. |
| 07 | Defect | `{@Session}` logs the whole session object, which can include the bearer token, and `{@Invoice}` logs invoice data. Secrets and personal data end up in the logs. |
| 08 | Defect | `ConcurrentDictionary` replaced with `Dictionary` in a singleton repository. Concurrent requests can corrupt it, and `Add` becomes check-then-act. |
| 09 | **Ambiguous** | The config change is fine. The README text is false for this app: signing out does not reset sample data, and the demo accounts come from configuration, not the in-memory store. A reviewer that has only the diff cannot check that. Scored separately. |
| 10 | Defect | `catch (Exception)` returns `200 {"status":"completed"}`. Every unexpected failure is reported to the caller as a success. |
| 11 | Defect *(ours)* | A new `HttpClient` per call (socket exhaustion under load), and the configured 10 s timeout is dropped, so the 100 s default applies. Taken from the INC-2041 release diff. Naming either one counts. |
| 12 | Clean *(ours)* | The architecture test from Task 1 (`DomainDependencyTests.cs`). It passes on the real code and was shown to fail on three seeded violations. |

Headline rates use the cases with a definite key:
- **Defects:** 01, 02, 05, 07, 08, 10, 11. That is 7 cases.
- **Clean:** 04, 06, 12. That is 3 cases.

## Why case 11 and case 12

- **Case 11** is a real defect seen in this work. The INC-2041 evidence shows
  it in production: socket errors on `PayExClient.SubmitAsync`, and
  outbound connections rising from 68 to 2,907.
- **Case 12** is a real clean change, and its correctness was measured, not
  assumed. It also tempts a reviewer, with reflection, walking the file
  system and XML parsing, so it tests the false-alarm rate.

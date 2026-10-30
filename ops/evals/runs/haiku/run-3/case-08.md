# Review: case-08.diff (InMemoryInvoiceRepository)

## Finding 1: Thread-unsafe storage replaces ConcurrentDictionary

- File: `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`
- Line: 9
- What goes wrong: The field changes from `ConcurrentDictionary<string, Invoice>` to `Dictionary<string, Invoice>`. An ASP.NET Core API handles requests concurrently, and an in-memory repository is normally shared across requests. Concurrent calls to `Add` and `Update` (and reads through `GetById` during a write) on a `Dictionary` are unsafe. Possible results include lost updates, corrupted internal state, `KeyNotFoundException`-style failures or infinite loops, and intermittent errors that are hard to reproduce. The diff gives no reason for the change and does not add a comment explaining why single-threaded access is safe.
- Confidence: High that this is a defect in a concurrent web app. Medium on exposure in practice, because the diff does not show how the repository is registered in `Program.cs`. If it is registered as a singleton, which is the usual choice for an in-memory store, the risk is real.

## Finding 2: Duplicate-ID guard is no longer atomic

- File: `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`
- Lines: 19 (`ContainsKey` check) and 24 (indexer write)
- What goes wrong: The old code used `TryAdd`, which checks and inserts in one atomic step. It threw `InvalidOperationException` for a duplicate ID, and `Handle(...)` maps that to 409. The new code checks `ContainsKey` and then writes with the indexer as two separate steps. Two concurrent `Add` calls with the same `invoice.Id` can both pass the check. The second write then silently overwrites the first invoice, with no exception and no 409. This breaks the duplicate-ID protection under concurrency and can lose data. It is a regression introduced by this diff.
- Confidence: High. The check-then-act race is evident from the code, and the loss of `TryAdd` atomicity is a direct consequence of the change.

## Other observations

- No tests are included in the diff. AGENTS.md requires tests for every new rule or behavior change, and this diff changes concurrency behavior without any test covering it.
- Line 1-5 (`using` directives) is outside the hunks shown, so I cannot tell whether `System.Collections.Concurrent` is now an unused import. I did not treat this as a finding.

## Summary

Two defects, both from the change of `ConcurrentDictionary` to `Dictionary`: unsafe concurrent access (line 9) and a non-atomic duplicate check in `Add` (lines 19-24) that can silently overwrite an invoice. I recommend reverting to `ConcurrentDictionary` and restoring `TryAdd`, or otherwise locking the check and write together.

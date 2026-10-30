# Review: case-08.diff

Scope: one file, `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`. Line numbers below are line numbers in `case-08.diff`.

## Finding 1: Duplicate check is no longer atomic

- File: `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`
- Line: diff line 18 (`if (_invoices.ContainsKey(invoice.Id))`) and diff line 23 (`_invoices[invoice.Id] = invoice;`)
- What goes wrong: The original `TryAdd` checked for the key and inserted in one atomic step. The replacement checks and then writes as two separate steps. If two `Add` calls for the same invoice Id overlap, both can pass `ContainsKey`, and the second write silently overwrites the first. No `InvalidOperationException` is thrown, so the duplicate rule is bypassed. Sequential duplicate adds still throw, so the regression appears only under concurrency.
- Confidence: high that the race exists in the code as written. Whether it is reachable depends on whether one repository instance is shared across requests. Registration is not in this diff, so I cannot confirm that.

## Finding 2: `Dictionary` is not safe for concurrent use

- File: `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`
- Line: diff line 9 (field changed from `ConcurrentDictionary` to `Dictionary`)
- What goes wrong: `Dictionary<TKey, TValue>` does not support concurrent reads and writes. The original `ConcurrentDictionary` existed to handle that. If the repository is registered as a singleton, which an in-memory store needs in order to keep data across HTTP requests, concurrent requests calling `Add`, `Update` or `GetById` can corrupt the internal state, throw, or return wrong results. `Update` (diff line 26, unchanged) writes through the same indexer and has the same exposure. Any enumeration method in the part of the file not shown in the hunks (old lines 13-15) would be affected too.
- Confidence: high that this is a thread-safety regression in the code. Medium that it is hit in practice, since the service lifetime is not visible in the diff.

## Finding 3: No tests for the changed behaviour

- File: whole diff (no test file is changed)
- Line: n/a
- What goes wrong: `AGENTS.md` requires tests for every new rule or endpoint, using `FakeInvoiceRepository`. This change alters duplicate detection and thread-safety of the repository, and no test covers either. A test that runs concurrent `Add` calls with the same Id would have caught Finding 1.
- Confidence: medium. Tests may live in a separate change that is not part of this diff.

## Minor note: possibly unused using directive

- File: `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`
- Line: not in the diff. If `using System.Collections.Concurrent;` is still at the top of the file, it is now unused.
- Confidence: low. This is a style issue only.

## Checked and not a finding

- Sequential `Add` behaviour is unchanged. Duplicates still throw.
- `GetById` is unchanged.

## Suggested fix

Restore `ConcurrentDictionary` and the `TryAdd` call. Alternatively, keep `Dictionary` but guard the check and the insert together with a lock. Add a test for concurrent duplicate `Add` calls.

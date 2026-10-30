# Code review: case-08 (InMemoryInvoiceRepository)

Diff reviewed: `ops/evals/cases/case-08.diff`

## Finding 1: The store is no longer thread-safe

- File: `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`
- Line: 9 (field `_invoices`, changed from `ConcurrentDictionary` to `Dictionary`)
- What goes wrong: `Dictionary<TKey, TValue>` does not support concurrent access. If this repository is shared across requests (for example, registered as a singleton; the registration is not in this diff, so I could not confirm it), concurrent `Add` or `Update` calls can corrupt the internal state, lose writes, or throw. `GetById` can also run during a write and read inconsistent data. Restoring `ConcurrentDictionary` removes this risk.
- Confidence: high (about 85%) that this is a defect. The severity depends on how the repository is registered, which the diff does not show.

## Finding 2: The duplicate-id check in `Add` is no longer atomic

- File: `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`
- Line: 18 to 23 (`Add`)
- What goes wrong: The atomic `TryAdd` is replaced by a `ContainsKey` check followed by a separate indexer write. Two concurrent `Add` calls with the same `invoice.Id` can both pass the `ContainsKey` check and both write. The second write silently overwrites the first, and no `InvalidOperationException` is thrown, so the rule "an invoice id must be unique" is no longer enforced under concurrency. This is still a race even if the field goes back to `ConcurrentDictionary`, because the check and the write remain two separate steps. Restoring `TryAdd` fixes it.
- Confidence: high (about 90%) that the race exists as written. Whether it is hit depends on concurrent use.

## Finding 3 (lower confidence, unchanged context line)

- File: `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`
- Line: 26 (`Update`, a context line this diff does not change)
- What goes wrong: `Update` uses `_invoices[invoice.Id] = invoice;`, which inserts the invoice when the id does not exist. An update to an unknown id therefore creates a record silently instead of failing. Finding 2 makes this easier to hit because the store can now hold an overwritten record without any error. Whether this is a defect depends on whether callers expect `Update` to fail for unknown ids, which I cannot check from this diff.
- Confidence: medium-low (about 50%). This behavior predates the diff.

## Summary

Both changed hunks are defects. The diff replaces the thread-safe dictionary and the atomic `TryAdd` with non-atomic operations, which can lose or overwrite invoices when requests run concurrently. The safe fix is to restore the original two lines: `ConcurrentDictionary` for the field and `TryAdd` in `Add`.

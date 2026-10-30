# Review of case-08.diff

## Finding 1: Thread-safety regression, ConcurrentDictionary replaced with plain Dictionary

- File: backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs
- Line: 9 in the diff (the new `private readonly Dictionary<string, Invoice> _invoices = new();`; the old line 8 is removed)
- What goes wrong: The repository is an in-memory store that an ASP.NET Core web API will normally register as a singleton and call from many request threads at once. `Dictionary<TKey,TValue>` is not safe for concurrent writes, or for reads that overlap a write. Concurrent `Add` or `Update` calls can corrupt the internal buckets and entries. In practice this shows up as lost invoices, `KeyNotFoundException` or `NullReferenceException` from reads, or an infinite loop or CPU spin inside the dictionary. Any enumeration (for example a list or GetAll method just outside the visible hunk) that overlaps a write will throw `InvalidOperationException: Collection was modified`. The old `ConcurrentDictionary` guarded against all of this. Nothing in the diff adds a lock or other synchronisation to replace it.
- Confidence: high (about 90%), assuming the repository is registered as a singleton, which is the usual pattern for an in-memory repository. I could not check this from the diff alone.

## Finding 2: `Add` is now a non-atomic check-then-act, so the duplicate check can be bypassed

- File: backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs
- Line: 18 to 23 in the diff (`if (_invoices.ContainsKey(invoice.Id)) { throw ... } _invoices[invoice.Id] = invoice;`)
- What goes wrong: `TryAdd` was a single atomic operation. It has been split into `ContainsKey` followed by an indexer assignment. Two concurrent `Add` calls with the same id can both pass the `ContainsKey` check, and both then write. The second write silently overwrites the first instead of throwing "already exists". That breaks the "Invoice already exists" guarantee and can lose an invoice. This is a real race on top of the corruption risk in Finding 1, and it would remain even if the dictionary type were fixed without a lock.
- Confidence: high (about 90%).

## Finding 3: Likely unused `using System.Collections.Concurrent;` (minor)

- File: backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs
- Line: top of file, outside the visible hunk
- What goes wrong: The original code used `ConcurrentDictionary`, so the file very likely imports `System.Collections.Concurrent`. That import is now probably unused. It is only a warning or a lint issue, not a runtime defect.
- Confidence: low to medium (about 40%). The imports are not shown in the diff.

## Other notes

- No test changes are included. Nothing in the diff gives a reason for the change, such as a stated need to drop concurrency support, and the change removes safety rather than adding any benefit.
- Recommendation: revert to `ConcurrentDictionary` and `TryAdd`. If a plain `Dictionary` is wanted for some reason, wrap every access in a `lock`, including enumeration, and keep the check and the insert in the same locked section.

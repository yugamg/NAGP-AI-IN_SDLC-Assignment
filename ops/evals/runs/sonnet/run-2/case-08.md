# Review of case-08.diff

## Finding 1: Thread-safety regression, ConcurrentDictionary replaced by plain Dictionary

- File: backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs
- Line: 9 in the new file (the `_invoices` field declaration; diff line 9)
- What goes wrong: The repository is an in-memory store shared across ASP.NET Core requests. In a Web API this is normally registered as a singleton, because otherwise the data would not persist between requests. Requests run concurrently on thread-pool threads. `Dictionary<TKey,TValue>` is not safe for concurrent reads and writes. Concurrent `Add`, `Update` and `GetById` calls can corrupt the internal buckets and entries. The visible symptoms are lost invoices, `InvalidOperationException` or `IndexOutOfRangeException` from the dictionary internals, an infinite loop in `TryGetValue` or the indexer (a known failure mode when the dictionary is resized during a concurrent write), and enumeration failures such as "Collection was modified" in the list method on the unseen lines 13-15. The change removes the thread-safety the original code deliberately had, and the diff gives no reason for it (there is no lock or other synchronization).
- Confidence: High (about 90%). It depends on the repository being registered as a singleton. I only saw the diff, not `Program.cs`. If it were registered as transient or scoped, the app would lose data between requests, which is not plausible for this design.

## Finding 2: `Add` duplicate check is now a non-atomic check-then-act

- File: backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs
- Line: 18-23 in the new file (diff lines 17-23, `ContainsKey` followed by `_invoices[invoice.Id] = invoice`)
- What goes wrong: The original `TryAdd` was one atomic operation. The new code calls `ContainsKey`, then separately assigns through the indexer. Two concurrent requests with the same invoice id can both pass the `ContainsKey` check. Both then write, so the second silently overwrites the first and no "already exists" exception is thrown. The uniqueness guarantee the method promises is broken. The indexer assignment also overwrites unconditionally, which makes the problem worse. Even with a lock this would be a behavior regression from `TryAdd` unless the check and write sit in the same critical section.
- Confidence: High (about 85%) that the race exists in the code. Practical impact depends on the likelihood of duplicate ids arriving concurrently, for example double-submit or retry.

## Finding 3: Possible leftover unused `using System.Collections.Concurrent;`

- File: backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs
- Line: near the top of the file, outside the diff context (the hunk starts at line 6)
- What goes wrong: The original code used `ConcurrentDictionary`, so the file very likely imports `System.Collections.Concurrent`. After this change that import is unused, which gives a compiler or analyzer warning at most. It is a minor cleanup item and would be moot if the change were reverted.
- Confidence: Low (about 40%). I cannot see the import lines in the diff.

## Other notes

- No tests were changed or added for this behavior. The project convention is that every rule or endpoint needs tests. A concurrency regression like this would not be caught by existing single-threaded tests.

## Recommendation

Revert to `ConcurrentDictionary` and keep `TryAdd`, which gives atomic add-if-absent and safe concurrent reads and writes. If a plain `Dictionary` is truly wanted, every access must be protected by a lock, and the check and insert in `Add` must happen under the same lock.

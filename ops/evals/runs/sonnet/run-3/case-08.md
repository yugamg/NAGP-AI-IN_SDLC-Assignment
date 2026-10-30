# Review of case-08.diff

## Finding 1: Repository is no longer thread-safe (high severity)

- File: backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs
- Line: 9 (new), the field declaration `private readonly Dictionary<string, Invoice> _invoices = new();`
- What goes wrong: The change replaces `ConcurrentDictionary` with a plain `Dictionary`. A repository like this is normally registered as a singleton in `Program.cs` and shared by every request. ASP.NET Core handles requests concurrently on multiple threads. `Dictionary<TKey,TValue>` is not safe for concurrent reads and writes. Simultaneous `Add` and `Update` calls (the indexer set) can corrupt the internal buckets and entries. Symptoms include lost invoices, an infinite loop in a resize or lookup, `NullReferenceException` or `IndexOutOfRangeException` inside the dictionary, and `InvalidOperationException` ("Collection was modified") when another method enumerates the dictionary (the `GetAll`-style method just above line 16) while a write happens. These failures appear intermittently under load and are hard to reproduce, and they would not show up in single-threaded unit tests.
- Confidence: high (about 90%). The only uncertainty is that the singleton lifetime is not visible in this diff. In-memory repositories in ASP.NET Core almost always are singletons, because a scoped one would lose data on every request.

## Finding 2: `Add` is now a non-atomic check-then-act (medium to high severity)

- File: backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs
- Lines: 18 to 23 (new)
- What goes wrong: The atomic `TryAdd` is replaced by `ContainsKey` followed by `_invoices[invoice.Id] = invoice`. Two concurrent `Add` calls with the same Id can both pass the `ContainsKey` check. Both then write, and the second silently overwrites the first, so the "already exists" `InvalidOperationException` is never raised. The service layer's duplicate-detection guarantee (which surfaces as a 409) is lost, and one caller's invoice is dropped without any error. Even with a lock this would need to cover both steps. Here there is no lock at all, which also feeds into Finding 1.
- Confidence: high (about 85%) that the race exists. It matters in practice only if the same Id can be created concurrently.

## Finding 3: `Update` is not synchronised with other operations (medium severity)

- File: backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs
- Line: 26 (unchanged line, but its safety depends on the field type that changed)
- What goes wrong: `_invoices[invoice.Id] = invoice` was safe on a `ConcurrentDictionary`. On a `Dictionary` it is an unsynchronised write that can race with `Add`, `GetById` and enumeration. This is the same underlying problem as Finding 1, listed separately because `Update` is the path used by approve and reject.
- Confidence: high (about 85%), as a consequence of Finding 1.

## Finding 4: Likely leftover unused `using System.Collections.Concurrent;` (low severity)

- File: backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs
- Line: near the top of the file (not visible in this diff)
- What goes wrong: If the `using` directive for `System.Collections.Concurrent` is still present, it is now unused. This causes at most a compiler or analyser warning.
- Confidence: low (about 30%). The file header is not in the diff, so I cannot confirm this.

## Other notes

- No justification is given for the change. Nothing in the diff shows a requirement that would make `Dictionary` the better choice. If the intent was to enforce stricter semantics, a `lock` around all operations or keeping `ConcurrentDictionary` would do that without losing safety.
- The diff includes no test changes. The change is behavioural (concurrency semantics), but nothing in the repository's tests would detect it.

## Summary

The diff introduces a real defect. It removes a thread-safe collection from a shared in-memory repository and turns the atomic duplicate check in `Add` into a racy check-then-set. Recommended fix: revert to `ConcurrentDictionary` and `TryAdd`, or guard every access with a single lock.

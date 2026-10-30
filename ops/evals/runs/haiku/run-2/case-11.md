# Review: case-11.diff

Findings, most serious first.

## 1. Per-call HttpClient causes socket exhaustion (high severity)

- File: `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, lines 20-26 (in `SubmitAsync`).
- What goes wrong: a new `HttpClient` is created on every call and never disposed. Each instance gets its own connection pool, and the sockets stay open until the finalizer runs. Under load this exhausts sockets and ports. The old code used the injected, factory-managed client, which avoided this. The change also ignores DNS refresh, which the `IHttpClientFactory` lifetime handling provided.
- Confidence: 95%.

## 2. HTTP timeout regression (high severity)

- File: `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, lines 20-23, and `backend/src/InvoiceApp.Api/Program.cs`, lines 37-41 (removed).
- What goes wrong: the removed registration set `client.Timeout = TimeSpan.FromSeconds(10)`. The new `new HttpClient { BaseAddress = ... }` has no timeout, so it falls back to the 100-second default. A slow or hung PayEx call now holds a request thread for up to 100 seconds instead of 10.
- Confidence: 90%.

## 3. Resilience and handler configuration silently dropped (medium severity)

- File: `backend/src/InvoiceApp.Api/Program.cs`, lines 37-42.
- What goes wrong: `AddHttpClient<...>` was replaced by `AddSingleton<IPayExClient, PayExClient>()`. Any message handlers, retry or circuit-breaker policies, or logging attached to the typed client are no longer applied. The diff does not show any were attached, so the current impact may be none, but the change removes the extension point.
- Confidence: 70%.

## 4. Options dependency and configuration failure moved to runtime (medium severity, cannot fully verify from the diff)

- File: `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, line 11 (constructor takes `PayExOptions`); `Program.cs` line 42.
- What goes wrong: `PayExClient` now needs `PayExOptions` injected. The diff does not show that `PayExOptions` is registered in DI (for example with `Configure<PayExOptions>` or `AddSingleton`). If it is not registered, the app fails when `IPayExClient` is first resolved.
- Also, `new Uri(_options.BaseUrl)` now runs per request. Before, the missing `PayEx:BaseUrl` key failed at startup because of the `!` null-forgiving operator in `Program.cs`. Now a missing or empty key surfaces as an exception on the first payment submission.
- Confidence: 60% on the DI registration gap, 75% on the deferred failure.

## 5. Tests and callers not updated (low severity, cannot verify from the diff)

- File: `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, line 11.
- What goes wrong: the constructor signature changed from `(HttpClient, PayExOptions)` to `(PayExOptions)`. Any existing test or code that constructs `PayExClient` with an `HttpClient` will no longer compile. AGENTS.md also requires tests for every new rule or endpoint, and the diff adds no tests for the new behavior. The diff does not show the test project, so this is unverified.
- Confidence: 50%.

## Not a defect

- The `X-Api-Key` header is added from configuration and is not logged. No secret is hard-coded in the diff.

## Summary

Two findings are clear from the diff alone: the per-call `HttpClient` (socket exhaustion risk) and the lost 10-second timeout. Both should be fixed before merge. The DI registration of `PayExOptions` and the test suite were not visible in the diff and should be checked.

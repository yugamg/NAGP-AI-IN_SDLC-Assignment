# Review of case-11.diff

## Finding 1: New HttpClient created on every call (socket exhaustion, stale DNS)
- File: backend/src/InvoiceApp.Api/Clients/PayExClient.cs
- Line: 20-24 (diff lines; `var http = new HttpClient { ... }` inside `SubmitAsync`)
- What goes wrong: `SubmitAsync` builds a new `HttpClient` on each invocation and never disposes it. Each instance has its own handler and connection pool. Under load, closed connections pile up in TIME_WAIT and the process can run out of sockets (`SocketException` / "Address already in use"). Because every client is short-lived, connections are not reused, so each payment submission pays for a fresh TCP and TLS handshake. The change also removes `IHttpClientFactory` (`AddHttpClient<IPayExClient, PayExClient>`), which pooled and recycled handlers and so handled DNS changes. The undisposed instances also leak until the GC finalizes them.
- Confidence: High (about 95%).
- Suggested fix: Keep `AddHttpClient<IPayExClient, PayExClient>` and inject `HttpClient`. If a direct client is needed, use one shared static or singleton `HttpClient` with a `SocketsHttpHandler { PooledConnectionLifetime = ... }`.

## Finding 2: The 10-second timeout is lost
- File: backend/src/InvoiceApp.Api/Program.cs (removed `client.Timeout = TimeSpan.FromSeconds(10);`) and PayExClient.cs line 20-24
- What goes wrong: The registration used to cap PayEx calls at 10 seconds. The hand-built `HttpClient` has no `Timeout` set, so it falls back to the .NET default of 100 seconds. A slow or hung payment provider now blocks request threads and callers for up to 100 seconds. Unless the caller passes a `CancellationToken` that cancels sooner, this can back up the request pipeline. The behaviour change is silent and the diff does not mention it.
- Confidence: High (about 90%).
- Suggested fix: Set `Timeout` again, or better, restore the factory registration that carried it.

## Finding 3: Dependency injection and configuration wiring may be broken
- File: backend/src/InvoiceApp.Api/Program.cs, line 42 (`AddSingleton<IPayExClient, PayExClient>()`), and PayExClient.cs constructor
- What goes wrong: The container must now resolve `PayExOptions` for the singleton. No registration or `Configure`/bind for `PayExOptions` appears in this diff. If one does not exist elsewhere, the app fails at the first resolution of `IPayExClient` with an `InvalidOperationException` ("Unable to resolve service for type PayExOptions"). The `PayEx:BaseUrl` setting was previously read directly in `Program.cs` and is now expected on `_options.BaseUrl`. If `PayExOptions.BaseUrl` is not bound from the same configuration key, `new Uri(_options.BaseUrl)` throws `ArgumentNullException` or `UriFormatException` at call time instead of at startup. That is a later and harder-to-spot failure.
- Confidence: Low to medium (about 35%). I can only see this diff, so I cannot tell whether `PayExOptions` is registered elsewhere.

## Finding 4: Request failures surface only at call time, and testability regresses
- File: PayExClient.cs, constructor signature (`PayExClient(HttpClient http, PayExOptions options)` became `PayExClient(PayExOptions options)`)
- What goes wrong: The constructor no longer takes an `HttpClient`, so tests can no longer inject a stub or mock `HttpMessageHandler`. Any existing tests that construct `PayExClient` with an `HttpClient` will stop compiling. New tests cannot intercept the HTTP call without real network access. The diff does not add or update any tests, although the project convention says every change needs tests.
- Confidence: Medium (about 60%) that existing tests break. High that the code is no longer unit-testable without the network.

## Finding 5: API key header attached per request
- File: PayExClient.cs, line 24 (`http.DefaultRequestHeaders.Add("X-Api-Key", _options.ApiKey);`)
- What goes wrong: This is harmless while the client is created per call. If anyone later "fixes" Finding 1 by caching the client in a field, `Add` on `DefaultRequestHeaders` would throw or duplicate on repeat use. Also, if a message handler or the options elsewhere already sets this header, it would be sent twice. This is a minor risk.
- Confidence: Low (about 20%).

## Summary
The change removes `IHttpClientFactory` usage in favour of `new HttpClient()` per call. The main defects are socket exhaustion and no connection reuse (Finding 1) and the lost 10-second timeout (Finding 2). Both are high confidence. Findings 3 to 5 are lower-confidence follow-on risks.

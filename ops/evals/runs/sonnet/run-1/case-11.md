# Review of case-11.diff

## Findings

### 1. New HttpClient created on every call (socket exhaustion, stale DNS)
- File: backend/src/InvoiceApp.Api/Clients/PayExClient.cs
- Line: about 17-26 of the new file (`SubmitAsync`, the `var http = new HttpClient { ... }` block)
- What goes wrong: `SubmitAsync` builds and never disposes a new `HttpClient` on every invocation. Each instance has its own connection pool, so under load closed connections pile up in TIME_WAIT and the process can hit `SocketException` / port exhaustion. The client is also registered as a singleton, so the old pooled-handler behaviour that `AddHttpClient` gave (handler lifetime rotation, DNS refresh) is lost. The change removes `IHttpClientFactory` and goes back to the classic anti-pattern.
- Confidence: high (about 95%).
- Fix: keep the typed client registration (`AddHttpClient<IPayExClient, PayExClient>`), or inject a single long-lived `HttpClient` / `IHttpClientFactory`. Set the base address and API key header once, not per request.

### 2. The 10 second timeout was dropped
- File: backend/src/InvoiceApp.Api/Program.cs (removed `client.Timeout = TimeSpan.FromSeconds(10)`) and PayExClient.cs (the new `HttpClient` sets no timeout)
- Line: Program.cs about 21-24 of the old file; PayExClient.cs about 20-23 of the new file
- What goes wrong: the new `HttpClient` falls back to the default 100 second timeout. A slow or hung payment provider now ties up request threads and connections for 100 seconds instead of failing after 10. `SubmitAsync` only observes the caller's `CancellationToken`, which may never be cancelled. This is a silent behaviour regression.
- Confidence: high (about 90%).
- Fix: set `Timeout` again (10 seconds), or use `CancellationTokenSource.CreateLinkedTokenSource` with a timeout.

### 3. Constructor signature change breaks existing callers and tests
- File: backend/src/InvoiceApp.Api/Clients/PayExClient.cs
- Line: about 10-11 of the old file (constructor)
- What goes wrong: `PayExClient(HttpClient, PayExOptions)` became `PayExClient(PayExOptions)`. Any test or code that constructs it with a fake `HttpClient` (the normal way to unit-test it with a stub `HttpMessageHandler`) no longer compiles. With the `HttpClient` created inside the method, `SubmitAsync` can no longer be tested without real network calls. The diff includes no test changes. The project rules say every change needs tests.
- Confidence: medium (about 70%). I cannot see the test project, so I cannot confirm that existing tests construct the class.

### 4. Configuration and API key handling moved without visible wiring (needs verification)
- File: backend/src/InvoiceApp.Api/Program.cs
- Line: about 21 of the new file (`AddSingleton<IPayExClient, PayExClient>()`)
- What goes wrong: the base URL used to be read from `builder.Configuration["PayEx:BaseUrl"]!` at startup. It now comes from `_options.BaseUrl` inside `SubmitAsync`. If `PayExOptions` is not registered in DI or not populated, resolving `IPayExClient` fails. A null or invalid `BaseUrl` throws `ArgumentNullException` / `UriFormatException` on the first payment request instead of at startup. The `X-Api-Key` header is new in this diff, and I cannot tell from the diff where the key was set before. If a message handler elsewhere also adds it, the header may be duplicated. If it was never sent, this changes what the provider receives.
- Confidence: low (about 35%). The diff does not show where `PayExOptions` is registered, so this may be fine.

### 5. Response is not disposed
- File: backend/src/InvoiceApp.Api/Clients/PayExClient.cs
- Line: about 26-29 of the new file
- What goes wrong: `HttpResponseMessage` and its content are not disposed, which adds to the resource leak described in finding 1. This is a minor point and was present before the change.
- Confidence: medium-low (about 40%).

## Summary
The main defects are findings 1 and 2: a per-call `HttpClient` that is never disposed, and a lost 10 second timeout. Both are real regressions caused by this diff. Findings 3 to 5 are lower-severity or depend on code that is not in the diff.

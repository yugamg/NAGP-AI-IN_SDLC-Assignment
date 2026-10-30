# Review of case-11.diff

## Findings

### 1. A new `HttpClient` is created on every call and never disposed (socket exhaustion)
- File: `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, lines 20-24 of the diff (the new `var http = new HttpClient { ... }` inside `SubmitAsync`)
- What goes wrong: `SubmitAsync` builds a fresh `HttpClient` for every payment submission and never disposes it. Each instance has its own connection pool and handler. Under load, connections pile up in TIME_WAIT and the process can run out of sockets (`SocketException` / "Address already in use"). It also adds a TCP and TLS handshake to every payment call. Because the client is created per call, DNS changes on the PayEx host are never picked up by a pooled connection.
- Confidence: high (about 95%). This is the classic `HttpClient` anti-pattern, and the diff removes the `IHttpClientFactory`-managed typed client that avoided it.

### 2. The 10-second timeout was dropped, so requests can hang for 100 seconds
- File: `backend/src/InvoiceApp.Api/Program.cs`, lines 37-41 of the diff (removed `client.Timeout = TimeSpan.FromSeconds(10)`), together with `PayExClient.cs` lines 20-23
- What goes wrong: the new `HttpClient` sets only `BaseAddress`. It falls back to the default 100-second timeout. A slow or unresponsive PayEx will tie up request threads and callers for 100 seconds instead of failing after 10. The only other bound is the caller's `CancellationToken`, and the diff gives no sign that the caller sets a deadline.
- Confidence: high (about 90%). The explicit timeout was removed and nothing replaces it.

### 3. The `HttpClient` setup moved from DI into the client, and the singleton registration loses factory features
- File: `backend/src/InvoiceApp.Api/Program.cs`, line 42 of the diff (`AddSingleton<IPayExClient, PayExClient>()`)
- What goes wrong: the typed-client registration no longer gives `PayExClient` a factory-managed handler. That loses handler lifetime rotation and any resilience, retry or logging handlers added to the client later. The constructor now needs only `PayExOptions`, but this diff does not show where `PayExOptions` is registered. If it is not registered elsewhere, the app fails at first resolution of `IPayExClient`, not at startup, since singletons are built lazily by default. `_options.BaseUrl` replaces `builder.Configuration["PayEx:BaseUrl"]!`. If `PayExOptions` is not bound to that same config key, `new Uri(_options.BaseUrl)` throws on every call.
- Confidence: medium (about 60%). The registration of `PayExOptions` and its binding to `PayEx:BaseUrl` are outside the diff, so I can't confirm the breakage. The loss of handler management is certain.

### 4. The constructor signature change breaks callers and tests, and the client can no longer be faked
- File: `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, lines 7-15 of the diff (the `HttpClient` constructor parameter is removed)
- What goes wrong: any test or code that builds `new PayExClient(http, options)` no longer compiles. Because `HttpClient` is created inside the method, tests can't inject an `HttpMessageHandler` stub. A unit test for `SubmitAsync` would therefore make real network calls. The diff includes no test changes, although the project convention is that changes come with tests.
- Confidence: medium (about 70%). It follows from the signature change, but I can't see the test project.

### 5. The `HttpResponseMessage` is not disposed
- File: `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, lines 26-29 of the diff
- What goes wrong: `response` is never disposed. This was already the case before the change, so it is a pre-existing minor leak and not something this diff introduced.
- Confidence: low (about 30% that it matters). Mentioned for completeness.

## Summary
The change replaces a correctly configured, factory-managed typed `HttpClient` with a per-call `new HttpClient()`. It also removes the explicit 10-second timeout and makes the client harder to test. Findings 1 and 2 are definite defects. Finding 3 depends on code outside the diff. Finding 4 is likely. The diff should not be approved as written. Keep `AddHttpClient<IPayExClient, PayExClient>` and set `BaseAddress`, `Timeout` and the `X-Api-Key` header in its configure callback, or use `IHttpClientFactory`.

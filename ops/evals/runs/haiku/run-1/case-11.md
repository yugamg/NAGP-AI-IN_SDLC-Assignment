# Review: case-11.diff

Two files changed: `backend/src/InvoiceApp.Api/Clients/PayExClient.cs` and `backend/src/InvoiceApp.Api/Program.cs`. The change removes the injected `HttpClient` and creates one inside `SubmitAsync`, and moves the PayEx client from a typed `AddHttpClient` registration to a plain singleton.

## Findings

### 1. New HttpClient created and never disposed on every call
- File: `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, lines 16-22 (in `SubmitAsync`)
- What goes wrong: `var http = new HttpClient { ... }` runs on every submission. Each instance has its own handler and connection pool and is never disposed. Under load this exhausts sockets (TIME_WAIT buildup) and leaves connections to be reclaimed only by finalization. The original code avoided this by using the DI-managed client from `AddHttpClient`. Recreating the client per request is the known anti-pattern that `IHttpClientFactory` exists to prevent.
- Confidence: high (about 95%).

### 2. The 10-second timeout was silently dropped
- File: `backend/src/InvoiceApp.Api/Program.cs`, removed line 40 (`client.Timeout = TimeSpan.FromSeconds(10);`). Replacement is in `PayExClient.cs` lines 16-19.
- What goes wrong: the new `HttpClient` uses the default timeout of 100 seconds. A hung PayEx call now holds the request thread for up to 100 seconds instead of 10. This is a behavior regression that the diff does not mention.
- Confidence: high (about 90%).

### 3. Configuration is now read per request instead of at startup
- File: `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, lines 18 and 20. Program.cs line 20 no longer checks anything.
- What goes wrong: the removed registration used `builder.Configuration["PayEx:BaseUrl"]!`, so a missing base URL failed at startup. Now `new Uri(_options.BaseUrl)` runs on the first payment. If `BaseUrl` is null or empty, that call throws at request time. If `ApiKey` is null, `DefaultRequestHeaders.Add` may throw or send an empty header. Misconfiguration moves from a fast startup failure to a runtime failure in production. Also, the key is sent over whatever scheme `BaseUrl` specifies; nothing enforces HTTPS.
- Confidence: medium-high (about 80%) for the timing change. Lower (about 40%) for the HTTPS point.

### 4. Possible unresolved dependency on PayExOptions registration
- File: `backend/src/InvoiceApp.Api/Program.cs`, line 20 (`AddSingleton<IPayExClient, PayExClient>()`).
- What goes wrong: the constructor now requires `PayExOptions`. The diff does not show `PayExOptions` being registered, and I did not check the rest of the codebase. If it is not registered elsewhere, dependency resolution fails when `IPayExClient` is first requested.
- Confidence: medium (about 50%). This is a question to verify, not a confirmed defect.

### 5. No tests for the changed behavior
- Files: the diff contains no test changes. `AGENTS.md` requires tests for every new rule or endpoint.
- What goes wrong: the change removes the only seam for faking PayEx HTTP calls (the injected `HttpClient`), so `SubmitAsync` now cannot be tested without a real network call. Nothing in the diff covers the new client construction, the header, or the timeout.
- Confidence: high (about 85%) that the diff adds no tests. Whether tests exist elsewhere is unknown from this diff.

## Summary
Defects found. The most important is finding 1: a new `HttpClient` per call is a socket-exhaustion risk under load. Finding 2 (lost 10-second timeout) is a clear behavior regression. Findings 3 and 4 are configuration and DI risks that need checking. I did not run anything and reviewed only this diff.

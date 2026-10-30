# Review: case-11.diff

Scope: `backend/src/InvoiceApp.Api/Clients/PayExClient.cs` and `backend/src/InvoiceApp.Api/Program.cs`. Line numbers refer to the new version of each file, taken from the hunk headers.

## Findings

### 1. Socket exhaustion from a new HttpClient on every call
- **File:** `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, lines 16-19 (`SubmitAsync`)
- **What goes wrong:** `SubmitAsync` creates `new HttpClient` on each call and never disposes it. This is the pattern `IHttpClientFactory` / `AddHttpClient` exists to prevent. Under load, each client holds its own socket pool, so sockets pile up in TIME_WAIT and the process can run out of ephemeral ports. The original code reused one managed client.
- **Confidence:** High (about 95%).

### 2. Timeout silently removed
- **File:** `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, lines 16-19, and `backend/src/InvoiceApp.Api/Program.cs` (removed `client.Timeout = TimeSpan.FromSeconds(10);`)
- **What goes wrong:** The 10-second timeout is gone. The new `HttpClient` uses the default 100-second timeout, so a slow or hung PayEx call can hold a request far longer than before. Nothing in the diff replaces the timeout.
- **Confidence:** High (about 90%) that the behaviour changes. The user impact depends on how callers handle slow responses.

### 3. Typed-client registration may not resolve at runtime
- **File:** `backend/src/InvoiceApp.Api/Program.cs`, line 20 (`AddSingleton<IPayExClient, PayExClient>()`), and `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, lines 9-12 (constructor)
- **What goes wrong:** The constructor now takes `PayExOptions` directly, but the diff does not register `PayExOptions` in the container. Unless it is registered elsewhere in `Program.cs` (which the diff does not show), DI cannot build `PayExClient`, and the first call that resolves `IPayExClient` fails. If the options are registered as `IOptions<PayExOptions>`, the constructor parameter type does not match and resolution fails the same way.
- **Confidence:** Medium (about 60%). The result depends on the parts of `Program.cs` that are outside the diff.

### 4. Configuration failures move from startup to request time
- **File:** `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, lines 17-19
- **What goes wrong:** `new Uri(_options.BaseUrl)` now runs on every call. A missing or invalid `PayEx:BaseUrl` throws on the first submission instead of at startup. The removed code failed on the `!` at registration, so the fail-fast behaviour is lost.
- **Confidence:** Medium (about 70%).

### 5. No tests for the changed client
- **File:** `backend/src/InvoiceApp.Api/Clients/PayExClient.cs`, lines 9-12 and 16-19
- **What goes wrong:** The class now builds its own `HttpClient`, so a test cannot swap in a fake `HttpMessageHandler`. The diff adds no tests. `AGENTS.md` requires tests for every new rule or endpoint and says tests should use `FakeInvoiceRepository`. The timeout and registration regressions above would not be caught by the current suite.
- **Confidence:** High (about 85%) that no test covers this change. This is a convention and maintainability issue, not a runtime fault.

### 6. Lifetime change (informational)
- **File:** `backend/src/InvoiceApp.Api/Program.cs`, line 20
- **What goes wrong:** The client was registered by `AddHttpClient`, which is transient. It is now a singleton. This is harmless today because the class only holds options, but the diff does not say the lifetime change was intended.
- **Confidence:** Low that this is a defect. Noted for completeness.

## Summary

Findings 1 to 5 are real problems in this diff. Finding 1 (new `HttpClient` per call, never disposed) and finding 2 (timeout dropped) are the most serious. Finding 3 is likely to break the app at runtime, but the answer depends on code outside the diff. I found nothing else in the diff.

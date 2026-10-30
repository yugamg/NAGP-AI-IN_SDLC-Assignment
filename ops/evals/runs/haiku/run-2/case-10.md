# Code review: case-10.diff

Scope: one file changed, `backend/src/InvoiceApp.Api/Controllers/ApiControllerBase.cs`. The hunk starts at line 20 of the new file, so the added lines are at new-file lines 35-38.

## Findings

### 1. Unhandled exceptions are reported as success (HTTP 200)
- **File:** `backend/src/InvoiceApp.Api/Controllers/ApiControllerBase.cs`
- **Lines:** 35-38 (the added `catch (Exception)` block; the `return Ok(new { status = "completed" });` is line 37)
- **What goes wrong:** Any exception that is not a `KeyNotFoundException`, `InvalidOperationException` or `ArgumentException` is caught and turned into a 200 OK response with `{ "status": "completed" }`. The action may have failed partway through, for example a repository write that threw, and the client is told it completed. Callers cannot tell a real success from a crash. Unexpected failures should surface as a 500 (or be left to the framework's exception handling), not as success. This also breaks the controller convention in AGENTS.md, where `Handle(...)` maps service errors to 404, 409 and 400.
- **Side effects:** The exception is swallowed with no logging, so server-side faults become invisible. For an invoice approval flow, a failed approval could look approved to the user and nothing would record the error.
- **Confidence:** High. The diff shows the catch-all returning `Ok` and nothing else in the hunk handles the failure.

### 2. Success response no longer reflects the action's result
- **File:** `backend/src/InvoiceApp.Api/Controllers/ApiControllerBase.cs`
- **Lines:** 37 (and 21 for the normal path, `return Ok(action());`)
- **What goes wrong:** The normal path returns the action's result, but the fallback returns a fixed body `{ status = "completed" }` with no data. Clients that expect the action's payload get a different shape on the failure path, and the same endpoint returns two incompatible response contracts.
- **Confidence:** Medium-high. This follows from finding 1, and the diff does not show what `action()` returns, so the exact impact depends on the callers.

### 3. No tests for the new behaviour
- **File:** not present in the diff (expected under `backend/tests/InvoiceApp.Tests`)
- **What goes wrong:** AGENTS.md says every new rule or endpoint needs tests. This change alters the error contract of every controller that inherits `ApiControllerBase`, but the diff includes no test for the new catch-all, and no test that checks unexpected exceptions produce an error status.
- **Confidence:** Medium. The diff shows only the one source file, so tests may exist elsewhere, but none are included in this change.

## Summary

One real defect, with high confidence: the new catch-all converts unexpected server errors into HTTP 200 "completed" responses, which hides failures from clients and from logs. I recommend removing the catch-all, or having it log the exception and return a 500 response, and adding a test that an unexpected exception produces a server-error status.

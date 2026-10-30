# Review: case-10.diff

Scope: one file changed, `backend/src/InvoiceApp.Api/Controllers/ApiControllerBase.cs`, adding a catch-all branch to `Handle(...)`.

## Findings

### 1. Unexpected exceptions are reported as success (high confidence)
- File: `backend/src/InvoiceApp.Api/Controllers/ApiControllerBase.cs`
- Line: diff lines 20-23 (the added `catch (Exception)` branch that returns `Ok(new { status = "completed" })`)
- What goes wrong: any exception that is not a `KeyNotFoundException`, `InvalidOperationException` or `ArgumentException` now returns HTTP 200 with `status = "completed"`. That includes genuine failures such as null references, persistence or I/O errors, and bugs. The caller is told the action succeeded when it may not have. For an invoice approval flow this can show an invoice as approved when nothing was saved, and clients will not retry. The branch also logs nothing, so the failure leaves no trace on the server. The correct mapping for an unexpected error is a 500 (for example `Problem()` or rethrowing to the framework's exception handler), not a success response. This also contradicts the convention in AGENTS.md that service errors map to 404, 409 and 400 only, with nothing mapped to success.
- Suggested direction: remove the catch-all, or map it to a 500 and log the exception.

### 2. Success body shape changes for failed calls (medium-high confidence)
- File: same file
- Line: diff lines 20-23
- What goes wrong: the success path returns `Ok(action())`, which carries the action's own result. The new branch returns a fixed anonymous object `{ status = "completed" }` with no data. Clients that read the result body will get a different shape, or no data, and may misread a failed call as a successful one with an empty payload. This is only a problem if the branch stays, and it is part of the same defect as finding 1.

### 3. Catch-all also catches cancellation (low-medium confidence)
- File: same file
- Line: diff lines 20-23
- What goes wrong: `catch (Exception)` also catches `OperationCanceledException` when the client disconnects. The method then tries to return a 200 to a client that has gone. This is minor on its own but reinforces that the broad catch is hiding control-flow signals.

### 4. No tests in the diff (medium confidence)
- File: not shown in the diff. Expected under `backend/tests/InvoiceApp.Tests`.
- What goes wrong: AGENTS.md requires tests for every new rule or endpoint. This diff changes the error contract of every endpoint that uses `Handle(...)`, and no test is shown for the new branch or for the unchanged 404, 409 and 400 mappings. This may be because the diff is partial, so confidence is medium.

## Summary

Blocking defect: finding 1. The catch-all turns unexpected server errors into successful responses, with no logging and no 500. Findings 2 and 3 are consequences of the same branch. Finding 4 is a process gap that may be outside this diff.

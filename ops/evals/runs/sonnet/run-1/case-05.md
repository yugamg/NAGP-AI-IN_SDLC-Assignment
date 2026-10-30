# Review of case-05.diff

Line numbers below refer to the new version of `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`. The hunk starts at line 41, so the added `Delete` method occupies lines 44-50.

## Findings

### 1. Missing authorization on a destructive endpoint (high severity)
- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 44-45 (`[HttpDelete("{id}")]` / `public IActionResult Delete`)
- What goes wrong: The new `Delete` action has no `[RequireRole("Admin")]` attribute. The neighbouring `Update` action does have it. The project convention is that destructive actions are Admin only. Any signed-in user, including the Approver role or any other role, can therefore permanently delete any invoice. This is a privilege-escalation and data-loss defect.
- Fix: add `[RequireRole("Admin")]` to the action.
- Confidence: high (about 90%). It depends on there being no controller-level role restriction. The visible context shows `Update` carries its own attribute, which suggests there is none.

### 2. Does not use `Handle(...)`, so errors are not mapped and a missing invoice returns 204 (medium severity)
- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 47-49
- What goes wrong: `_repository.Delete(id)` is called directly and not wrapped in `Handle(() => ...)` as in the other actions (see `Handle(() => _repository.Add(invoice))` at line 41). If the repository throws for an unknown id, the exception is unhandled and the client gets a 500 instead of a 404. If the repository silently ignores unknown ids, the endpoint returns 204 and logs "Invoice deleted" for an invoice that never existed, so the audit log is misleading. In both cases the 404/409/400 mapping that the conventions require is missing.
- Confidence: medium-high (about 75%). I cannot see the repository's `Delete` behaviour in the diff.

### 3. Deletion is not guarded by any business rule, and the controller calls the repository directly (medium severity)
- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 47
- What goes wrong: Any invoice can be deleted whatever its state (Draft, PendingApproval, Approved or Rejected). Deleting an Approved invoice, or one that is pending approval, bypasses the lifecycle that `ApprovalService` owns and removes the approval record. The project rules say business rules belong in the Domain project, not in controllers, and this action goes straight to the repository. If deleting approved invoices should be prohibited or restricted, that check is missing.
- Confidence: medium (about 60%). Whether deletion is meant to be allowed in every state is a product decision I cannot see from the diff.

### 4. No tests added (medium severity)
- File: the diff touches only `InvoicesController.cs`
- Line: not applicable (whole new endpoint, lines 44-50)
- What goes wrong: The project requires tests for every new endpoint. None are included. Tests for the missing role check, the unknown-id case and the state rules would have caught findings 1-3.
- Confidence: high (about 90%) that no tests are in this diff. I cannot rule out a separate change that adds them.

### 5. Unverified dependencies: `_logger` and `CurrentUser.Name` (low severity)
- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 48
- What goes wrong: The code uses `_logger` and `CurrentUser.Name`. Neither appears in the diff context. If the controller has no `ILogger` field, or `CurrentUser` has no `Name` property, this will not compile. If `CurrentUser` can be null, there is a null-reference risk after the delete has already happened, so the request would fail with a 500 even though the invoice was removed.
- Confidence: low (about 30%). I only flag it because the context is not visible.

## Summary
The most important defect is finding 1: the unrestricted delete endpoint lets non-Admin users destroy invoices. Findings 2 and 3 are likely correctness gaps. Finding 4 is a convention violation. Finding 5 is a low-confidence compile or null-safety concern.

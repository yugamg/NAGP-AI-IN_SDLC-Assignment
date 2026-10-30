# Review: case-05.diff

Scope: one file changed, `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`, adding `DELETE api/invoices/{id}` (new lines 44-50 of the hunk).

## Findings

### 1. Delete endpoint is not restricted to Admin
- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`, line 44-45 (`[HttpDelete("{id}")]` / `public IActionResult Delete`)
- What goes wrong: the new action has no `[RequireRole(...)]` attribute. The `Update` action directly below it (line 53) carries `[RequireRole("Admin")]`, so the sibling pattern in this diff treats mutating actions as Admin-only. As written, any signed-in user whose request passes the class-level checks can permanently delete an invoice.
- Confidence: high (about 85%). The diff does not show a class-level attribute; if one exists that already restricts the whole controller to Admin, this finding falls away.

### 2. Missing invoice is not reported and the service error mapping is bypassed
- File: line 47 (`_repository.Delete(id);`)
- What goes wrong: the result of the delete is ignored and the action always returns `NoContent()`. Other actions in this controller return through `Handle(...)`, which maps service errors to 404, 409 and 400. Deleting an id that does not exist will likely return 204 instead of 404, and the caller cannot tell whether anything was removed. The diff does not show the signature of `Delete` on the repository, so I cannot confirm whether it returns a value or throws.
- Confidence: medium (about 60%).

### 3. No status check before deleting an invoice
- File: line 47
- What goes wrong: the action calls the repository directly. Nothing in the diff checks the invoice lifecycle state owned by `ApprovalService` (Draft, PendingApproval, Approved, Rejected). An Approved or PendingApproval invoice can be removed with no rule stopping it, which can drop approval history. If the business rule lives in the Domain project, this controller skips it.
- Confidence: medium (about 50%). The rule may exist in code outside this diff.

### 4. Audit log records a deletion that may not have happened
- File: line 48 (`_logger.LogInformation("Invoice {InvoiceId} deleted by {User}", ...)`)
- What goes wrong: the log line is written unconditionally, right after the repository call and before any check of the outcome. If the id was not found (see finding 2), the log says the invoice was deleted when nothing was removed. The message is misleading for anyone using these entries as an audit trail.
- Confidence: low to medium (about 40%).

### 5. Irreversible hard delete with no confirmation or soft delete
- File: line 47
- What goes wrong: `Delete` removes the record outright. There is no soft-delete flag, no restore path and no guard in the diff. An accidental or malicious call cannot be undone from this code.
- Confidence: low (about 30%). This is a design concern rather than a clear bug.

### 6. No tests in this diff
- File: the diff contains only the controller change.
- What goes wrong: the new endpoint and its behaviour (role check, not-found case, log output) have no tests in this change. Findings 1 to 3 would each be caught by a test.
- Confidence: high that the diff has no test file. The repository may have tests elsewhere, but none are included here.

### Not verified from the diff
- `_logger` and `CurrentUser` are used but not declared or injected in the visible hunk. They may exist elsewhere in the file. If not, the change will not compile.
- `_repository.Delete(string)` is called but its signature is not visible in this diff.

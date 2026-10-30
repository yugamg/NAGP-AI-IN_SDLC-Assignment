# Review: case-05.diff

Scope: one change to `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs` that adds `DELETE api/invoices/{id}` (`Delete`). Line numbers refer to the new file.

## Findings

### 1. Delete endpoint has no role restriction (high severity)
- **File:** `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- **Line:** 44-45 (`[HttpDelete("{id}")]` and `public IActionResult Delete(string id)`)
- **What goes wrong:** The method has no `[RequireRole("Admin")]` attribute. The adjacent `Update` action (line 53) has one. Any signed-in user, including an Approver, can permanently delete invoices. The project convention is that destructive actions are Admin only.
- **Fix:** Add `[RequireRole("Admin")]` above the method, matching `Update`.
- **Confidence:** High (about 90%). The diff shows the attribute missing, and the convention is stated in the project docs. I cannot see the class-level attributes in this diff, but a class-level sign-in requirement would not satisfy the Admin-only rule.

### 2. No tests for the new endpoint (medium-high severity)
- **File:** `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- **Line:** 44-50
- **What goes wrong:** The project requires tests for every new endpoint, using `FakeInvoiceRepository`. This diff adds no test file or test change. Combined with finding 1, nothing would catch a missing role check.
- **Fix:** Add tests covering: Admin can delete, non-Admin gets 403, and the not-found path.
- **Confidence:** High (about 85%) that no tests are in this diff. Tests could exist in another change, but nothing in this diff shows them.

### 3. Delete logic sits in the controller and bypasses the domain (medium severity)
- **File:** `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- **Line:** 47 (`_repository.Delete(id);`)
- **What goes wrong:** The controller calls the repository directly. The project keeps business rules in `InvoiceApp.Domain` (`ApprovalService` owns the invoice lifecycle), not in controllers. There is no lifecycle check, so an invoice in `Approved` or `PendingApproval` state could be deleted, which may break audit or approval history.
- **Fix:** Move the delete rule into a Domain service method that enforces the allowed states, and have the controller call it.
- **Confidence:** Medium (about 60%). The diff does not show whether a delete rule exists elsewhere, so I cannot confirm the state check is missing, only that it is not in this change.

### 4. Not-found and error handling not mapped (medium-low severity)
- **File:** `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- **Line:** 47-49
- **What goes wrong:** The method does not use `Handle(...)`, which the project uses to map service errors to 404, 409 and 400. It also ignores any result from `_repository.Delete(id)`. If the id does not exist, the outcome depends on the repository: an unhandled exception (500) or a silent 204. Either way the client gets no 404.
- **Fix:** Use `Handle(() => ...)` through a domain method, or check the repository result and return `NotFound()` when the invoice does not exist.
- **Confidence:** Medium (about 50%). The repository's `Delete` signature and behavior are not in the diff.

### 5. Possibly unresolved dependencies (low severity)
- **File:** `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- **Line:** 48 (`_logger`) and 48 (`CurrentUser.Name`)
- **What goes wrong:** `_logger` and `CurrentUser` are used but not declared in this hunk. If they are not already injected or defined in the controller or base class, the code will not compile. The surrounding context suggests they exist, but the diff does not show it.
- **Confidence:** Low (about 20%).

## Summary
The change has one clear defect: a destructive endpoint is open to any signed-in user, which contradicts the Admin-only rule (finding 1). It also has no tests (finding 2). Findings 3 and 4 are design and error-handling concerns that depend on code outside this diff. Finding 5 is unverified.

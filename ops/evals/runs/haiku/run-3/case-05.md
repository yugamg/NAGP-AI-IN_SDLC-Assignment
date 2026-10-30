# Review: case-05.diff

Scope: one file changed, `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`, one hunk (`@@ -41,6 +41,14 @@`) adding a `DELETE api/invoices/{id}` action. The hunk is shown with no surrounding context beyond the action it adds.

## Findings

### 1. Delete action has no role restriction (defect, high confidence)
- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`, line 9 (method `Delete`, attributes at line 8).
- What goes wrong: the action is decorated only with `[HttpDelete("{id}")]`. The `Update` action directly below it carries `[RequireRole("Admin")]`. The project convention is that destructive actions are Admin only, so any signed-in user who can reach the controller (any role, including Approver) can delete invoices.
- Confidence: high that the attribute is missing relative to the convention. Whether a controller-level `[RequireRole]` or a global policy exists cannot be seen in this hunk, so if one exists this would be moot, but nothing in the diff shows it.

### 2. Deletion bypasses the domain layer (defect against project conventions, medium-high confidence)
- File: line 11, `_repository.Delete(id);`
- What goes wrong: the controller calls the repository directly. The project rule is that business rules live in `InvoiceApp.Domain` (`ApprovalService` owns the invoice lifecycle), never in controllers. Any lifecycle rule for deletion (for example, blocking deletion of an Approved or PendingApproval invoice) has no place to live here and is not enforced.
- Confidence: medium-high. The diff does not show whether a delete rule exists elsewhere, but the controller contains none.

### 3. Missing invoice is not reported as 404 (defect, medium confidence)
- File: lines 9-13.
- What goes wrong: the action does not go through `Handle(...)`, so the service-error-to-HTTP mapping (404, 409, 400) is skipped. `_repository.Delete(id)` does not check the result, and the action returns `NoContent()` unconditionally. A DELETE for an unknown id therefore returns 204, which hides the fact that nothing was deleted. Behavior depends on what the repository does for a missing key (throw or silently return), which is not visible in the diff.
- Confidence: medium. Depends on the repository implementation, which is not in this diff.

### 4. No tests added (convention violation, high confidence)
- The diff contains no changes under `backend/tests/InvoiceApp.Tests`. The project requires tests for every new endpoint and rule, and the test class for this controller or for the domain delete path would need a new test. Nothing in the diff shows coverage for the Admin-only restriction, the not-found case, or the logging.
- Confidence: high that no test change is in this diff.

### 5. Unverified references (low confidence, not a defect in itself)
- Lines 12 and 11 use `_logger` and `CurrentUser.Name`. Neither is declared in the hunk. They may exist in the base file, so this is only a note that they could not be checked from the diff.

### 6. Minor: no input validation on `id` (low confidence)
- Line 9. An empty or whitespace `id` is passed straight to the repository. Likely harmless in an in-memory store, but worth checking against how other actions in the controller treat ids.

## Summary
Most important: finding 1 (a DELETE endpoint open to every signed-in role, against the Admin-only convention for destructive actions), then finding 2 (the deletion logic sits in the controller, not the Domain) and finding 4 (no tests). Finding 3 (204 for a missing invoice) is a correctness issue that depends on the repository code not shown here.

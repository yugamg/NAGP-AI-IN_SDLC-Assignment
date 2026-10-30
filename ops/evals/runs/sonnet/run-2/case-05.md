# Review of case-05.diff

## Finding 1: Missing authorization on the new DELETE endpoint

- **File:** backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- **Line:** diff lines 8-9 (new `[HttpDelete("{id}")]` / `Delete(string id)` action, around line 44 of the new file)
- **What goes wrong:** The new `Delete` action has no `[RequireRole("Admin")]` attribute. The neighbouring `Update` action in the same controller is restricted with `[RequireRole("Admin")]`. Deleting an invoice is destructive, so it should be Admin only. Without the attribute, any signed-in user can delete any invoice. That includes an Approver, or any other low-privilege account, if the sign-in check alone is what gates the endpoint. This is an authorization bypass and a data-loss risk.
- **Confidence:** High (about 90%). The diff shows the attribute missing, and the sibling `Update` method in the diff context has it. The only uncertainty is whether a class-level or global default policy restricts the action, and the diff gives no sign of one.

## Finding 2: No error handling and no 404 for a missing invoice

- **File:** backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- **Line:** diff lines 11-13 (body of `Delete`)
- **What goes wrong:** The action calls `_repository.Delete(id)` directly instead of going through `Handle(...)`. The `Add` action just above uses `Handle(() => ...)`. If the repository throws for an unknown id, the client gets an unhandled 500 instead of a 404. If the repository silently ignores an unknown id, the code still logs "deleted" and returns 204. In that case the audit log claims a deletion that never happened.
- **Confidence:** Medium (about 65%). It depends on how `Repository.Delete` behaves, which the diff does not show. Skipping `Handle` is a deviation from the sibling action in either case.

## Finding 3: No status check before deleting

- **File:** backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- **Line:** diff lines 11-13
- **What goes wrong:** The delete goes straight to the repository, so an invoice in any state can be removed, including PendingApproval and Approved ones. Approval history and audit trail can then be lost. A rule that decides which invoices may be deleted would belong in the Domain layer (`ApprovalService`), not in the controller. This version has no such rule.
- **Confidence:** Low to medium (about 40%). The diff does not show what the intended business rule is.

## Finding 4: No tests included

- **File:** the diff touches only InvoicesController.cs
- **Line:** whole change
- **What goes wrong:** A new endpoint with a destructive action ships with no tests. That includes no test that a non-Admin gets 403, and no test that a missing invoice gives 404. A test for the role check would have caught Finding 1.
- **Confidence:** Medium (about 60%). The diff shows only one file, but a test file would appear in it if one had been added.

## Not an issue

The `CurrentUser.Name` and `_logger` usage looks consistent with a typical controller. I cannot confirm from the diff that they exist in this class.

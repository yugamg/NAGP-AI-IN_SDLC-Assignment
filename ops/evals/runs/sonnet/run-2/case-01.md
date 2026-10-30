# Review of case-01.diff

Scope: one added endpoint, `POST bulk-approve`, in `ApprovalsController.cs`.

## Finding 1: NullReferenceException when `Ids` is null or the body is missing
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 16 of the diff (`foreach (var id in request.Ids)`)
- What goes wrong: `BulkApproveRequest` declares `Ids` as `List<string>?`, so null is explicitly allowed. A body of `{}` or `{"ids": null}` leaves `Ids` null, and `foreach` throws a NullReferenceException. The result is an unhandled 500, not a 400. A request with no body or a bad content type can also leave `request` itself null, which fails the same way. The `Handle(...)` wrapper that the project uses to map errors to 400/404/409 is not used here.
- Confidence: high (about 95%)

## Finding 2: Partial approval with no atomicity and no per-item error handling
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 16-19 of the diff (the loop calling `_approvals.Approve(id, CurrentUser.Name)`)
- What goes wrong: The call is not wrapped in `Handle(...)` and is not caught per item. If an id is not found, or an invoice is not in `PendingApproval` (the service signals this with an error that normally maps to 404 or 409), the exception escapes mid-loop. Every invoice earlier in the list has already been approved and persisted. The caller gets an error response with no indication of which invoices were approved. A retry then fails on the already-approved ones. The batch is neither all-or-nothing nor a per-item result report.
- Confidence: high (about 85%) that this is a defect. The exact error code depends on how `ApprovalService` and `Handle` behave, which I could not see in this diff.

## Finding 3: Error mapping bypassed (no `Handle(...)`)
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 14-21 of the diff
- What goes wrong: The project convention is that controllers use `Handle(...)` so service errors map to 404, 409 and 400. This endpoint calls the service directly, so any domain exception becomes a generic 500. This is the cause of the error-code problems in Findings 1 and 2.
- Confidence: medium-high (about 75%). It rests on the stated convention, and I did not read the base class.

## Finding 4: Duplicate ids and unbounded list size
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 16-19 of the diff
- What goes wrong: Nothing de-duplicates the ids. A repeated id is approved a second time, and that call probably throws because the invoice is no longer pending. This is another route to the partial-failure state in Finding 2. There is also no cap on the number of ids, so one request can run an arbitrarily large loop.
- Confidence: medium (about 60%) for duplicates, low (about 40%) for the missing cap.

## Finding 5: Role restriction may not match the convention
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 11 of the diff (`[RequireRole("Approver")]`)
- What goes wrong: The project convention says approvals are "Approver or Admin". The diff uses `[RequireRole("Approver")]`, the same as the neighbouring `Reject` action. Whether Admin is allowed depends on how `RequireRole` treats Admin, which is not visible here. If it does not treat Admin as an implicit superset, Admins cannot bulk-approve. This is the lowest-priority item and may be a false positive.
- Confidence: low (about 30%)

## Not in the diff
- No tests are included for the new endpoint. The project requires tests for every new endpoint, so the nulls, partial failure and unauthorized-role cases above are untested. (The diff shows only this one file, so tests might exist elsewhere, but none are part of this change.)

## Summary
The main defects are the null dereference on `request.Ids`, which gives a 500, and the non-atomic loop with no error handling, which can leave a half-approved batch and an unhelpful error.

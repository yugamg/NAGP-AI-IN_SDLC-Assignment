# Review of case-01.diff

Scope: new `BulkApprove` endpoint in `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`.

## Findings

### 1. Null `Ids` causes a NullReferenceException (HIGH confidence)
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, line 16 of the diff (`foreach (var id in request.Ids)`), around new-file line 49.
- The request record is declared as `BulkApproveRequest(List<string>? Ids)`, so `Ids` is explicitly nullable. A body of `{}` or `{"ids": null}` leaves `Ids` null, and the `foreach` throws. This surfaces as an HTTP 500 instead of a 400. A missing or empty body can also leave `request` itself null, with the same result.
- Fix: validate `request?.Ids` is non-null and non-empty and return 400 (or have the Domain service validate it).

### 2. Not wrapped in `Handle(...)`, so service errors become 500s (HIGH confidence)
- File: same, lines 14-21 of the diff.
- The project convention is that controllers use `Handle(...)` so service errors map to 404, 409 and 400. This action calls `_approvals.Approve(...)` directly in a loop. An unknown id, an invoice that is not in `PendingApproval`, or any other domain error will escape as an unhandled exception (500) instead of 404/409.
- Fix: route the work through `Handle(...)` or a Domain-level bulk method that returns mapped errors.

### 3. Partial application: no atomicity and no per-item result (HIGH confidence)
- File: same, lines 16-19 of the diff.
- Invoices are approved one at a time. If the Nth id fails (not found, already approved, wrong state), the first N-1 invoices stay approved. The caller gets an error and no list of what succeeded. A client that retries the request will then hit conflicts on the already-approved ids.
- The loop also does not de-duplicate ids. A repeated id will fail on its second approval after the first has been committed.
- Fix: validate all ids first and then approve, or return a per-id success/failure result. Do this in the Domain project (e.g. an `ApprovalService.ApproveMany`).

### 4. Business logic placed in the controller (MEDIUM-HIGH confidence)
- File: same, lines 14-21 of the diff.
- Iterating, accumulating results and deciding failure behaviour is orchestration of the approval lifecycle. The project rules say business rules belong in the Domain project, never in controllers. This is the cause of findings 2 and 3, and it leaves nowhere to put limits such as a maximum batch size.

### 5. No tests added (HIGH confidence)
- The diff touches only the controller. The project requires tests for every new rule or endpoint (using `FakeInvoiceRepository`). Nothing covers null or empty ids, a mixed valid and invalid batch, duplicates, or the role restriction.

### 6. No upper bound on batch size (LOW-MEDIUM confidence)
- File: same, line 16 of the diff.
- An unbounded list of ids is processed synchronously in a single request. This is minor for an in-memory demo, but it is an easy abuse or accidental-load vector.

### 7. Role attribute may exclude Admin (LOW confidence)
- File: same, line 11 of the diff (`[RequireRole("Approver")]`).
- Conventions say approvals are "Approver or Admin". The diff cannot show whether `RequireRole` implicitly admits Admin. The existing `Reject` action uses the same attribute, so this may be consistent with the surrounding code. Worth confirming, but I do not think it is a defect.

## Summary
The endpoint is not safe to merge as written. The clearest defects are the null `Ids` crash (finding 1), the missing `Handle(...)` error mapping (finding 2), non-atomic partial approvals (finding 3), and missing tests (finding 5).

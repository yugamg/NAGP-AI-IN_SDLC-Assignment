# Review: case-01.diff (bulk-approve endpoint)

The diff adds `POST bulk-approve` to `ApprovalsController`. It takes a `BulkApproveRequest` with a nullable `Ids` list and approves each invoice in a loop. I found the following defects.

## Finding 1: null `Ids` causes an unhandled exception

- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 8 (declaration `List<string>? Ids`) and line 16 (`foreach (var id in request.Ids)`)
- What goes wrong: `Ids` is declared nullable, but the loop dereferences it without a check. A request body of `{}` or `{"ids": null}` makes `request.Ids` null, so `foreach` throws `NullReferenceException` and the caller gets a 500 instead of a 400. There is also no check that `request` itself is non-null.
- Confidence: high.

## Finding 2: service errors are not mapped to 404, 409 or 400, and the approval is not atomic

- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 18 (`results.Add(_approvals.Approve(id, CurrentUser.Name));`) and line 21 (`return Ok(...)`)
- What goes wrong: The loop calls `_approvals.Approve` directly and does not go through `Handle(...)`, which the project conventions require so that service errors map to 404, 409 and 400. If one id is unknown, or its invoice is not in PendingApproval, the service presumably throws, and the request fails with a 500. Invoices approved earlier in the loop stay approved, so the caller gets a partial result with no record of which ids succeeded. Bulk operations need either a pre-validation pass or a per-item result that reports success and failure for each id.
- Confidence: high that the error mapping is missing. Medium on the exact failure mode, because `ApprovalService` is not in the diff and I have not read how it reports errors.

## Finding 3: duplicate ids are approved twice

- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 16 to 19
- What goes wrong: If the list contains the same id twice, for example `["a", "a"]`, the second `Approve` call runs against an invoice that is already Approved. Depending on how the domain handles that transition, it either throws, which causes the partial-failure problem in Finding 2, or silently returns a duplicate entry in the response. Ids are not de-duplicated or validated for null or empty strings.
- Confidence: medium. It depends on how `ApprovalService.Approve` behaves when the invoice is already approved, and that code is not in the diff.

## Finding 4: business logic sits in the controller

- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 14 to 21
- What goes wrong: The project conventions say business rules go in the Domain project, never in controllers. The loop that decides which invoices get approved and how results are collected is orchestration logic that belongs in `ApprovalService` (for example, a `BulkApprove` method that returns per-item outcomes).
- Confidence: medium. The rule is clear in AGENTS.md, but this loop is thin and the reviewer could argue it is only plumbing.

## Finding 5: no tests for the new endpoint or its rules

- File: `backend/tests/InvoiceApp.Tests` (no test file is changed in this diff)
- What goes wrong: The conventions require tests for every new rule or endpoint, using `FakeInvoiceRepository`. This diff adds an endpoint with its own error cases (null list, unknown id, partial failure, duplicates) and includes no tests for any of them.
- Confidence: high that the diff contains no tests. Whether tests exist elsewhere in the repo is outside what this diff shows.

## Not reported

- The `[RequireRole("Approver")]` attribute matches the existing `Reject` action in the diff context. AGENTS.md says approvals are Approver or Admin, so it is worth checking that Admin is also allowed, but I cannot tell from this diff alone whether that is a defect.
- The `BulkApproveRequest` record placement and the `api/[controller]` route follow the conventions.

# Review: case-01.diff (BulkApprove endpoint)

Scope: the diff adds `BulkApproveRequest` and a `POST bulk-approve` action to `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`. Line numbers below are in the new file.

## Finding 1: null `Ids` throws an unhandled exception

- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, line 49 (`foreach (var id in request.Ids)`)
- Problem: `BulkApproveRequest` declares `Ids` as nullable (line 41, `List<string>? Ids`). A body with no `ids` property, or with `"ids": null`, leaves `Ids` null, and the `foreach` throws `NullReferenceException`. The client gets a 500 when a 400 is the correct answer.
- Confidence: high (about 95%).

## Finding 2: null entries inside the list

- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, line 51 (`results.Add(_approvals.Approve(id, CurrentUser.Name))`)
- Problem: a body such as `["abc", null]` passes a null string to `Approve`. The diff does not show how `Approve` handles null, so the outcome is unknown. It most likely throws, which leads to the partial-failure problem in Finding 3.
- Confidence: medium (about 60%).

## Finding 3: partial failure is not atomic and not handled

- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, lines 49-52
- Problem: each `Approve` call changes state immediately. If the Nth id is unknown, is not pending approval, or otherwise fails, and `Approve` throws (the diff does not show how it reports failure), the exception escapes the loop. The first N-1 invoices stay approved. The caller gets an error with no record of which invoices were approved, and no per-id mapping to 404 or 409 is applied. Retrying the same request fails on the invoices already approved.
- Confidence: high that the loop is not atomic (about 90%). Medium that `Approve` throws on failure (about 60%).

## Finding 4: duplicate ids in one request

- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, lines 49-51
- Problem: an id listed twice runs `Approve` twice on the same invoice. Depending on `Approve`, the second call either throws (see Finding 3) or records a second approval. Nothing in the diff de-duplicates the input.
- Confidence: medium (about 50%).

## Finding 5: no limit on the number of ids

- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, lines 41 and 49
- Problem: `Ids` has no size cap, so one request can run an unbounded number of approvals synchronously.
- Confidence: low to medium (about 35%).

## Checked and not a defect

- `results.Select(DecisionResponse.From)` on line 54 is lazy, but it is enumerated during response serialization after the loop has finished, so the output is correct.
- `[RequireRole("Approver")]` matches the `Reject` action in the same controller.

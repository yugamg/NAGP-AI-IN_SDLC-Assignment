# Review of case-01.diff

Scope: one new endpoint, `POST bulk-approve`, in `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`.

## Findings

### 1. Null `Ids` causes a NullReferenceException (500)
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 16 (`foreach (var id in request.Ids)`), with the nullable declaration on line 8
- What goes wrong: `BulkApproveRequest` declares `Ids` as `List<string>?`, so a body of `{}` or `{"ids": null}` is valid and binds with `Ids == null`. The `foreach` then throws `NullReferenceException`, and the client gets a 500 instead of a 400. The nullable annotation shows the author expected null, but nothing handles it. An empty list also returns 200 with an empty array and no signal that nothing was done.
- Confidence: high

### 2. Not atomic: a failure partway through leaves earlier invoices approved
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Lines: 16-19
- What goes wrong: invoices are approved one at a time in a loop. If the Nth id does not exist (404), is not in PendingApproval (409), or fails any other rule, the call throws. Invoices 1 to N-1 are already approved and persisted, but the client receives only an error and the `results` list is lost. The client cannot tell which invoices were approved. Retrying hits conflicts on the ones already approved. There is no all-or-nothing validation up front and no per-id result reporting.
- Confidence: high

### 3. Service errors are not routed through `Handle(...)`, so 404/409/400 become 500
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Lines: 12-22
- What goes wrong: the project convention (controllers inherit `ApiControllerBase` and wrap service calls in `Handle(...)` so service errors map to 404, 409 and 400) is not followed here. `_approvals.Approve(...)` is called directly inside a loop. An unknown id or an invalid state transition will likely surface as an unhandled exception and a 500. This compounds finding 2. The existing endpoint on line 5 returns `Ok(DecisionResponse.From(invoice))`, which suggests the other actions go through the wrapper.
- Confidence: medium. The `Handle` signature is not visible in the diff, and I am relying on the project convention.

### 4. Duplicate ids and unbounded list size
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Lines: 16-19
- What goes wrong: if the same id appears twice, the second `Approve` call acts on an already-approved invoice and will probably fail with a conflict, which triggers the partial-failure problem in finding 2. There is also no upper bound on `Ids.Count`, so a single request can loop over an arbitrarily large list.
- Confidence: medium for duplicates, low-to-medium for the size limit, which is minor in an in-memory demo.

### 5. Role restriction may be too narrow
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 11 (`[RequireRole("Approver")]`)
- What goes wrong: the project convention says approvals are Approver or Admin. If `RequireRole("Approver")` does not implicitly admit Admin, admins are blocked from this endpoint. That would be inconsistent with the other approval actions, unless the attribute already treats Admin as a superset.
- Confidence: low. This cannot be confirmed from the diff.

### 6. No tests included
- File: whole diff (only `ApprovalsController.cs` is touched)
- What goes wrong: the diff adds a new endpoint and behaviour (bulk approval, error and partial-failure handling) with no unit or endpoint tests. Project rules require tests for every new endpoint. Findings 1 and 2 would likely have been caught by tests for null or empty input and for a mixed valid and invalid id list.
- Confidence: high

## Summary
The endpoint has real defects: a null-reference crash on missing `Ids`, non-atomic partial approval with no way for the client to see what happened, and probably no mapping of service errors to proper status codes. It also has no tests. It should not be merged as is.

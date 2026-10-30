# Review: case-07.diff

Scope: one hunk in `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, `Approve(string id)` (hunk `@@ -28,9 +28,15 @@`). Line numbers below are new-file lines, counted from the hunk header.

## Findings

### 1. Full invoice object written to logs
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, lines 34-36.
- What goes wrong: `{@Invoice}` is a destructuring placeholder, so the logger serializes the entire invoice object on every approval. Any customer, vendor, amount, or bank field on the invoice ends up in log storage. Nothing in the diff masks or limits fields. Logging the invoice id and status would be enough for an audit trail.
- Confidence: high that this is a defect in principle. Medium on severity, because the diff does not show the `Invoice` type and I cannot confirm which fields it holds.

### 2. Current user session logged with `{@Session}`
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, lines 34-36.
- What goes wrong: `CurrentUser` is destructured in full under the label "Session". Depending on its type, that can include claims, identifiers, or token-derived values that should not be persisted in logs. The approver's name alone would be the minimal necessary value.
- Confidence: medium. The type of `CurrentUser` is not visible in the diff.

### 3. Possible compile failure: `_logger` not shown as declared or injected
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, line 34.
- What goes wrong: the hunk uses `_logger` but does not add a field or constructor parameter for it. If the controller does not already have `_logger` injected, the project will not compile. The diff is too small to confirm either way.
- Confidence: low to medium. This is unverified from the diff alone.

### 4. Logging placed in the controller, not the domain or service layer
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, lines 34-36.
- What goes wrong: `AGENTS.md` says business rules belong in the Domain project and not in controllers. An audit-style log of a state change is closer to the approval lifecycle than to HTTP handling, so it is more likely to be skipped or duplicated if the approval is reached from another path. This is a design concern, not a runtime bug.
- Confidence: low to medium.

### 5. Logging happens after the state change and can turn a success into a 500
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, lines 34-38.
- What goes wrong: `_approvals.Approve` has already committed the approval before the log call runs. If serializing the invoice or user throws (for example, on a cyclic reference), the client gets an error even though the invoice was approved. The client may retry and hit a 409 or other conflict.
- Confidence: low. This depends on the serializer and the object graph, which the diff does not show.

### 6. No test added for the new log call
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, lines 34-36.
- What goes wrong: `AGENTS.md` says every new endpoint or rule needs tests. This change alters the endpoint's behavior (new side effect, new data written out), and the diff includes no test change.
- Confidence: medium. Whether a logging change requires a test is a judgment call under the project rules.

## Summary

The main defects are findings 1 and 2: the change writes the whole invoice and the whole current-user session object into logs on every approval. Findings 3 to 6 are lower confidence or design-level. I found no other defects in the visible hunk.

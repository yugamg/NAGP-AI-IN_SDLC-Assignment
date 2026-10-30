# Review: case-07.diff

Scope: one file changed, `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, `Approve` action (hunk `@@ -28,9 +28,15 @@`).

## Findings

### 1. Full invoice object written to logs
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: new-file lines 34-36 (diff lines 11-13), the `_logger.LogInformation` call, specifically the `{@Invoice}` placeholder bound to `invoice`.
- What goes wrong: `{@Invoice}` tells the logger to destructure and serialize the entire invoice object on every approval. Invoices typically carry customer, billing and amount data. That data then lands in log sinks that are usually less protected than the app's own storage, and it is retained there. This is a data-exposure and privacy defect. The approval message only needs the invoice identifier.
- Confidence: high that the log writes the whole invoice. Medium-high that this is a real problem, because the `Invoice` type is not in this diff and I cannot confirm which of its fields are sensitive.

### 2. Full current-user identity written to logs
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: new-file lines 34-36 (diff lines 11-13), `{@Session}` bound to `CurrentUser`.
- What goes wrong: `{@Session}` destructures the whole `CurrentUser` object. Depending on its type, that may include claims, token-derived values, email addresses or other identity attributes. The label also misnames the value: `CurrentUser` is a user, not a session. Only the name or id is needed here, and the approval already records `CurrentUser.Name`.
- Confidence: medium. The contents of `CurrentUser` are not visible in this diff.

### 3. Logger field not visible in this hunk
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: new-file line 34 (diff line 11), `_logger.LogInformation(`.
- What goes wrong: the hunk does not show a `_logger` field or constructor injection. If the controller does not already declare and inject `ILogger<ApprovalsController>`, the change will not compile. The diff does not include the top of the file, so I cannot confirm this either way.
- Confidence: low that it is broken. This is a verification gap, not a confirmed defect.

### 4. Log level and volume
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: new-file line 34 (diff line 11), `LogInformation`.
- What goes wrong: this writes a large structured record for every approval at Information level. That raises log volume and makes the data-exposure problem in finding 1 more widespread. It is a secondary concern.
- Confidence: medium.

### 5. No test change
- The diff contains no test change. The project conventions in AGENTS.md require tests for new rules or endpoints. A logging-only change may not fall under that rule, so this is a minor observation, not a violation.
- Confidence: low.

## Summary

Nothing in the logic of `Approve` changed: the service call, the sign-in check and the response are untouched. The defect is in the new log statement, which serializes the full invoice and the full current-user object. I recommend logging only identifiers, such as the invoice id and the approver name, and removing the `@` destructuring operator from both placeholders. Finding 3 should be checked against the full file before merging.

# Review: case-07.diff

Scope: one hunk in `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, in `Approve(string id)`. The change adds a `_logger.LogInformation` call after `_approvals.Approve(...)`. The approval call and the response are unchanged.

## Findings

### 1. Full invoice object written to logs
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 34-36 (new file numbering), specifically the `{@Invoice}` placeholder and the `invoice` argument on line 36.
- What goes wrong: `{@Invoice}` is a destructuring placeholder, so the logger serializes every public property of the invoice. Invoices usually carry customer, vendor, and payment or bank details. Those values now go into the log sink, which has a wider audience and longer retention than the API response. The project conventions say never commit customer data, and this is the same kind of exposure in a different place.
- Confidence: high that this is a problem (about 80%). The `Invoice` type is not in the diff, so I cannot confirm which fields it holds.
- Suggested fix: log only identifiers and the new status, for example the invoice id and outcome, and not the whole object.

### 2. Current user object written to logs under a "Session" label
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 35-36. The placeholder `{@Session}` is bound to `CurrentUser`.
- What goes wrong: `{@Session}` also serializes every public property of `CurrentUser`. If that object holds tokens, claims, or other session data beyond the user name, those values are written to the log. The label `Session` also misdescribes the value, which makes review harder.
- Confidence: medium (about 60%). The shape of `CurrentUser` is not in the diff.
- Suggested fix: log `CurrentUser.Name` (or a user id) with a plain `{User}` placeholder, not the whole object.

### 3. `_logger` is used but not declared in the diff
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: 34. The call uses `_logger`, but the hunk shows no field declaration or constructor injection.
- What goes wrong: if the controller does not already receive an `ILogger` through its constructor, the file will not compile.
- Confidence: cannot be verified from this diff. The class header and constructor are outside the hunk. If the field already exists, this finding does not apply.

### 4. Hunk header does not match the hunk body
- File: the diff itself, line 4 (`@@ -28,9 +28,15 @@`).
- What goes wrong: the header says six lines were added (9 to 15), but the body contains only four `+` lines (lines 11-14 of the diff). Either the diff was truncated or edited after it was generated. If it is truncated, this review may be missing changes.
- Confidence: high that header and body disagree. I cannot tell from here whether lines are missing from the diff or the header is wrong.

## Result

Defects found: four, as listed above. The two main findings are that the change writes an entire invoice object and an entire user or session object into logs (findings 1 and 2). The approval logic and the HTTP response in this hunk show no logic defect. Findings 3 and 4 depend on code or diff content that is not visible here.

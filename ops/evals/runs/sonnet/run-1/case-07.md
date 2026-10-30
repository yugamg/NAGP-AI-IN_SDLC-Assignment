## Review of case-07.diff

### Finding 1: Sensitive data logged (full invoice payload and full user session object)
- File: backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs
- Line: 11-13 of the diff (new lines in `Approve`, the `_logger.LogInformation(...)` call)
- What goes wrong: The `{@Invoice}` and `{@Session}` placeholders use Serilog/Message Templates destructuring (`@`), so the entire `invoice` object and the entire `CurrentUser` object are serialized into the log line on every approval. This writes the whole invoice payload (vendor, amounts, bank or payment details, notes, any customer data) and the whole user session (name, role, and potentially tokens, session IDs or email, depending on what `CurrentUser` carries) into application logs. Logs are usually retained longer, readable by more people, and shipped to third parties, with weaker access controls than the app's data store. This creates a data-exposure and compliance risk (PII, financial data, session or credential leakage, log injection of user-controlled fields). It is also unnecessary: an audit log needs only the invoice id, the approver's name and the outcome.
- Fix: Log only identifiers, for example `"Invoice {InvoiceId} approved by {Approver}", invoice.Id, CurrentUser.Name`. Do not destructure whole domain objects or the session.
- Confidence: High (about 90%) that this is a defect (sensitive data in logs). The exact severity depends on the fields on `Invoice` and `CurrentUser`, which are not visible in the diff.

### Finding 2: `_logger` may not exist in the controller
- File: backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs
- Line: 11 of the diff
- What goes wrong: The diff does not show a `_logger` field, an `ILogger<ApprovalsController>` constructor parameter, or a `using Microsoft.Extensions.Logging;`. If the controller does not already have them (the hunk starts at line 28, so the constructor is not visible), this will not compile.
- Confidence: Low (about 25%). It may already be injected in unchanged code outside the hunk.

### Finding 3: Business and cross-cutting concern placed in the controller; no test
- File: backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs
- Line: 11-13 of the diff
- What goes wrong: The project conventions say business logic belongs in the Domain project, not controllers, and that new behaviour needs tests. Approval audit logging added in the controller bypasses `ApprovalService`, and no test is included in the diff. This is a minor convention issue, secondary to Finding 1.
- Confidence: Medium-low (about 40%) as a defect, since plain logging in a controller is common.

### Summary
The main defect is Finding 1: logging the full invoice and the full session object on every approval leaks sensitive data into logs. This change should not be merged as written.

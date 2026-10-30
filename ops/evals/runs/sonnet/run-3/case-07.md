## Review of case-07.diff

### Finding 1: Sensitive data logged (full invoice payload and full session object)

- File: backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs
- Line: 11-13 of the diff (new `_logger.LogInformation(...)` call in `Approve`)
- What goes wrong: The log statement uses the `{@Invoice}` and `{@Session}` destructuring operators. `{@...}` serializes the entire object graph into the log. `invoice` is the full invoice payload, which can include customer, vendor and financial data. `CurrentUser` is the whole session or user object. It may include the user's role, email, session or auth token, or other identifiers. Both end up in application logs, which usually have wider access, longer retention and are shipped to third-party log sinks. This is a data-exposure and privacy problem (PII or financial data, and possibly credentials or session tokens in logs). It also breaks the project rule against committing or exposing customer data and credentials. Log only the invoice id, the approver name and the outcome instead, for example `"Invoice {InvoiceId} approved by {Approver}"`.
- Confidence: High (about 90%). The exact fields on `Invoice` and `CurrentUser` are not visible in the diff, but destructuring whole domain and session objects into logs is a defect whatever they contain.

### Finding 2: `_logger` may not exist in the controller

- File: backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs
- Line: 11 of the diff
- What goes wrong: The diff does not add a `_logger` field, an `ILogger<ApprovalsController>` constructor parameter, or a `using Microsoft.Extensions.Logging;`. If the class does not already have these (the hunk context does not show the constructor), the code will not compile. If the constructor was changed, that change is missing from the diff.
- Confidence: Low to medium (about 35%). The hunk starts at line 28, so the constructor is outside the visible context and may already inject a logger.

### Finding 3: Log emitted after the state change with no error handling (minor)

- File: backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs
- Line: 9-13 of the diff
- What goes wrong: The "Approval completed" message is logged at Information level for every approval. This is not a bug in itself. Combined with Finding 1, it means every approval writes a full payload to the logs at default log levels, which increases the volume of leaked data. It is also not covered by a test, and the project conventions say every new rule or endpoint needs tests. No test change is in the diff, but a log line probably needs none.
- Confidence: Low (about 20%). Mostly a note on severity amplification, not a separate defect.

### Summary

The one clear defect is Finding 1, logging the whole invoice and session objects. Finding 2 is worth checking before merge.

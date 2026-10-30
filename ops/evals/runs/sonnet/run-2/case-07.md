# Review of case-07.diff

Scope: one change to `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`. It adds a `LogInformation` call after `Approve` succeeds.

## Findings

### 1. Whole session/user object logged with destructuring (sensitive data leak)
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: about 34-36 in the new file (the added `_logger.LogInformation(...)` call, argument `CurrentUser` bound to `{@Session}`)
- What goes wrong: The `@` operator makes Serilog and similar structured loggers serialize the entire `CurrentUser` object into the log. That object is the signed-in session context. It can hold the session token or ID, the user's role, and other identity fields, and possibly credential-derived values, depending on how `Auth/` populates it. Those values would be written to log files and log sinks in plain text. Anyone with log access could then reuse a session or see personal data. Logs are usually retained longer and shared more widely than the app's own data.
- Fix: Log only what the audit needs, for example the invoice ID and `CurrentUser.Name`. Never destructure the session object.
- Confidence: High (about 85%) that this is a defect. The exact fields exposed depend on the `CurrentUser` type, which is not in the diff.

### 2. Full invoice payload logged (customer and financial data in logs)
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: about 34-36 (argument `invoice` bound to `{@Invoice}`)
- What goes wrong: Destructuring the whole invoice writes every field to the logs: vendor or customer details, amounts, notes and anything else on the entity. This is customer and financial data going to a less controlled place. The log message ("Invoice payload") also looks like debug output, not an audit entry. It fires on every approval, so the volume is high.
- Fix: Log the invoice ID, the new status and the approver name only.
- Confidence: Medium-high (about 75%). It is a privacy and data-minimisation problem, not a functional bug.

### 3. `_logger` is not shown to exist in the controller
- File: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`
- Line: about 34 (use of `_logger`)
- What goes wrong: The diff adds no `ILogger<ApprovalsController>` field, constructor parameter or `using`. The context shows only `_approvals` in use. If `_logger` is not already declared, the build fails. If it is declared elsewhere in the file, the finding does not apply.
- Fix: Confirm that the field and constructor injection exist. `ApiControllerBase` may already provide a logger.
- Confidence: Low (about 30%). The unchanged parts of the file are outside the diff.

### 4. No test for the new behaviour
- File: `backend/tests/InvoiceApp.Tests` (nothing added in this diff)
- What goes wrong: The change adds no test. The repo convention is that every new rule or endpoint change comes with tests. A test could assert that the log contains no session or invoice payload.
- Confidence: Low-medium (about 40%). This is a process gap, not a runtime defect.

## Summary
The main defects are findings 1 and 2: the change logs the whole session object and the whole invoice, which leaks sensitive data into logs. Finding 3 is a possible compile problem that cannot be confirmed from the diff alone. Finding 4 is a missing test.

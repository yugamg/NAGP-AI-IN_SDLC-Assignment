# Review: case-06.diff

Scope: the diff adds three xUnit tests to `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs`. The production code (`ApprovalService`, `FakeInvoiceRepository`, the `Pending(...)` helper) is not in the diff, so the findings below are conditional on that code, which I could not check.

No definite defect found in the added test code. The items below are the points where the tests could fail or mislead, depending on code outside the diff.

## Findings

1. **File:** `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs`, hunk header (`@@ -58,4 +58,34 @@`), diff line 4.
   **What goes wrong:** The header says 4 old lines and 34 new lines, but the body as shown has 3 old lines (context) and 36 new lines (3 context, 33 added). The diff may have been edited or truncated, and a strict `git apply` could reject it.
   **Confidence:** Low to medium (about 40%). The mismatch is visible in the body as displayed, but the raw file may differ.

2. **File:** `ApprovalServiceTests.cs`, new line 68 (`Reject_WithWhitespaceReason_Throws`).
   **What goes wrong:** `Assert.Throws<ArgumentException>` matches the exact type only, not subclasses such as `ArgumentNullException`. The test passes only if `ApprovalService.Reject` throws exactly `ArgumentException` for a whitespace-only reason. If the service has no whitespace check, or throws a domain exception, the test fails. The test also does not assert that the invoice stays in `PendingApproval`.
   **Confidence:** Low (about 30%) that this is a defect. It depends on the service code, which is not in the diff.

3. **File:** `ApprovalServiceTests.cs`, new line 83 (`Approve_RecordsTheApproverAndTime`).
   **What goes wrong:** The bounds use `DateTime.UtcNow`. If `ApprovalService` stamps `DecidedAt` with `DateTime.Now` (local time), the assertion fails on machines not set to UTC. The test also reads the real clock directly, which makes it less deterministic than an injected clock. Separately, the test never asserts that the status became `Approved`, so that behaviour is not covered here.
   **Confidence:** Low to medium (about 35%) that the timezone issue is real. The missing status assertion is a fact about the diff (high confidence), but it is a coverage gap, not a defect.

4. **File:** `ApprovalServiceTests.cs`, new line 91 (`Approve_UnknownInvoice_Throws`).
   **What goes wrong:** The test expects `KeyNotFoundException`. The project conventions say service errors map to 404, 409 and 400 through `Handle(...)`, which suggests the domain uses its own error types for not-found. If `ApprovalService` throws a different type, the test fails.
   **Confidence:** Low (about 35%) that this is a defect. It depends on the service code.

## Not verified

- The `using` directives for `DateTime`, `KeyNotFoundException` and `ArgumentException` are not in the diff. They are assumed to exist in the current file.
- Whether the new tests compile against the current `ApprovalService` API (`Reject`, `Approve`, `DecidedBy`, `DecidedAt`) cannot be confirmed from the diff alone.

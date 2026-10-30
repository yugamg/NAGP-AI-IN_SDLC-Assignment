# Code review: case-06

Scope: the diff adds three xUnit tests to `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs`. It changes no production code. I reviewed only the diff, so I could not check `ApprovalService`, the `Pending` helper or `FakeInvoiceRepository`. Line numbers below are in the target file, derived from the hunk header `+58`.

## Findings

1. **ApprovalServiceTests.cs, line 68: `Reject_WithWhitespaceReason_Throws`**
   - `Assert.Throws<ArgumentException>` matches the exact type only. If `ApprovalService.Reject` throws a derived or domain-specific exception, the test fails.
   - The diff adds no production change. If `Reject` does not already reject whitespace-only reasons, this test fails as written.
   - The test does not assert that the invoice stays `PendingApproval` after the rejected call. A service that changes state before throwing would still pass.
   - Confidence: medium that the exception type or the validation is wrong (not verifiable from this diff). High that the missing state assertion is a gap.

2. **ApprovalServiceTests.cs, lines 81-83: `Approve_RecordsTheApproverAndTime`**
   - The time check compares `DecidedAt` against `DateTime.UtcNow`. If the service stamps `DecidedAt` with `DateTime.Now`, the test fails on any machine not set to UTC. The test would then be correctly reporting a service defect, but the failure would look like a test problem.
   - The test never asserts `result.Status == Approved`, so the approval itself is not checked here.
   - Confidence: low to medium (depends on the service's clock call, which I could not read).

3. **ApprovalServiceTests.cs, line 91: `Approve_UnknownInvoice_Throws`**
   - The test expects `KeyNotFoundException`. `AGENTS.md` says controllers map service errors to 404 through `Handle(...)`. If the service signals not-found with another exception type or a result object, this test fails or pins the wrong contract.
   - Confidence: medium (not verifiable from this diff).

4. **Diff integrity: hunk header `@@ -58,4 +58,34 @@`**
   - The header says 4 old lines and 34 new lines. The body shows 3 context lines and 33 added lines, which gives 3 old and 36 new. The header is stale or the patch was edited by hand, and `git apply` would likely reject it.
   - Confidence: high that the counts do not match the body. Medium that this affects the real change rather than how the eval file was produced.

## Summary

No definite defect in the test code itself from the diff alone. Findings 1 to 3 depend on `ApprovalService` behaviour that this diff does not show, so they are conditional. The clearest issues are the hunk header mismatch (4) and the missing state assertion in the whitespace test (1).

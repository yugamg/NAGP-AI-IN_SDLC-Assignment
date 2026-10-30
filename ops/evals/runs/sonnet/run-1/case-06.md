# Review of case-06.diff

The diff only adds three xUnit tests to `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs`. There is no production code change. I found no defects in the diff itself.

## Findings

No confirmed defects. These are the points I checked, and none of them is a defect I can show from the diff alone.

1. `ApprovalServiceTests.cs`, lines 15 and 38. The exception types are asserted exactly.
   - `Assert.Throws<T>` requires the exact type, not a subtype.
   - Line 15 expects `ArgumentException`. If `ApprovalService.Reject` throws `ArgumentNullException` or a custom exception for a blank reason, the test fails.
   - Line 38 expects `KeyNotFoundException`. If the service throws a custom not-found exception, the test fails.
   - The diff does not show what the service throws, so I cannot confirm either mismatch.
   - Confidence that this is a real defect: low (about 15%). I raise it only because the assertions are strict.

2. `ApprovalServiceTests.cs`, lines 25-30. The time assertion depends on the system clock and on how the service reads time.
   - `Assert.InRange(result.DecidedAt!.Value, before, DateTime.UtcNow)` uses `DateTime.UtcNow` directly.
   - If the service uses `DateTime.Now` (local time) or an injected clock, the test fails or becomes meaningless.
   - It would also fail if `DecidedAt` is stored as `Kind=Unspecified` with a different offset.
   - On a normal build with a UTC-based service this passes. A coarse timer resolution could in theory make the bounds equal, but `InRange` is inclusive, so that is not a failure.
   - Confidence that this is a real defect: low (about 10%). It is a test-robustness note.

3. `ApprovalServiceTests.cs`, lines 12, 20 and 36. These lines use `Pending(...)` and `FakeInvoiceRepository`.
   - `Pending(...)` is a helper that must already exist in the class. The context lines above the hunk suggest it does, and the tests use `FakeInvoiceRepository` per the project convention.
   - I cannot verify the helper from the diff.
   - Confidence that this is a real defect: very low (under 5%).

## Coverage notes (not defects)

- The whitespace-reason test covers only `Reject`. It does not check that the invoice stays `PendingApproval` after the failed call.
- The approver test does not check that the status becomes `Approved`. An existing test may cover this.
- The new tests do not cover the case where the approver and the submitter are the same person.

## Conclusion

I found no defects in the diff. The new tests look correct and follow the project conventions, including descriptive names, the `FakeInvoiceRepository` and no `flag` parameters. Findings 1 to 3 are low-confidence checks that the diff alone cannot settle, not defects.

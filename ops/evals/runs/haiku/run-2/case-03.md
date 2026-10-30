# Review: case-03.diff

Scope: `backend/src/InvoiceApp.Domain/DiscountCalculator.cs`, method `Apply`. Line numbers refer to the new file.

## Finding 1: discount is truncated instead of rounded (lines 16-17)

- File: `backend/src/InvoiceApp.Domain/DiscountCalculator.cs`, lines 16-17
- Problem: the new code calls `Math.Round(discount, 2, MidpointRounding.ToZero)`. `ToZero` truncates toward zero for every value, not only for midpoints, so the discount is cut to two decimals instead of rounded. The outer `Math.Round(..., 2, MidpointRounding.ToZero)` truncates again. The result is systematically rounded down, which overcharges the customer by up to one cent.
- Example: `Apply(99.99m, 10m)`. The exact discount is 9.999, so the exact discounted amount is 89.991, which rounds to 89.99. The new code computes the discount as 9.99 and returns 90.00. The original code returns 89.99.
- Example 2: `Apply(50.00m, 33.333m)`. The exact discounted amount is 33.3335, which rounds to 33.33. The new code returns 33.34.
- Confidence: high (about 95%). This follows directly from the `ToZero` semantics and the arithmetic above.

## Finding 2: rounding mode of the final result changed from AwayFromZero to ToZero (lines 16-17)

- File: `backend/src/InvoiceApp.Domain/DiscountCalculator.cs`, lines 16-17
- Problem: the original code rounded the final value with `MidpointRounding.AwayFromZero`. The diff switches the final rounding to `ToZero`, with no stated reason. For input amounts with more than two decimal places, the final truncation drops the extra digits. For example, `Apply(10.005m, 0m)` returns 10.00, where the original returns 10.01. For amounts with at most two decimal places, the final rounding is a no-op after Finding 1's inner truncation, so the mode change has no effect in that case and only makes the intent unclear.
- Confidence: high that the mode changed (about 95%). Medium that it causes a visible error in practice (about 60%), because it only matters for sub-cent input amounts.

## Finding 3: no test change accompanies a behavior change (diff-level)

- File: `backend/src/InvoiceApp.Domain/DiscountCalculator.cs` (the diff contains no test file)
- Problem: `AGENTS.md` requires tests for every new rule or changed business logic, in `backend/tests/InvoiceApp.Tests` as `DiscountCalculatorTests`. This diff changes the monetary calculation and adds no test covering the rounding cases above. A test for `Apply(99.99m, 10m)` would have caught Finding 1.
- Confidence: high that this diff contains no test change (about 90%). Lower confidence that no test exists elsewhere in the repository, because this review only reads the diff.

## No other findings

- The removal of the `discounted` local variable and the expression itself are not defects.
- No findings about the discount percent range (negative or over 100) because the diff does not touch validation.

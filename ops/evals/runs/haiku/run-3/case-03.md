# Review: case-03.diff

Scope: one file, `backend/src/InvoiceApp.Domain/DiscountCalculator.cs`, one hunk in `DiscountCalculator.Apply`.

## Finding 1: Discount is truncated instead of rounded

- File: `backend/src/InvoiceApp.Domain/DiscountCalculator.cs`
- Line: 11-12 (new code). The discount is now rounded with `MidpointRounding.ToZero`, which truncates to 2 decimal places.
- What goes wrong: A discount with a fractional-cent part is always cut down, so the customer pays more. Example: `amount` 19.99, `discountPercent` 10. The discount is 1.999. The old code gives 19.99 - 1.999 = 17.991, which rounds to 17.99. The new code truncates the discount to 1.99, then gives 19.99 - 1.99 = 18.00. The customer is overcharged by one cent. The error always goes the same direction on every discounted amount with a sub-cent discount of 0.005 or more.
- Confidence: high (about 90%). The arithmetic follows directly from the code. The only open question is whether truncation was intended, and nothing in the diff says so.

## Finding 2: Final rounding changed from AwayFromZero to ToZero

- File: `backend/src/InvoiceApp.Domain/DiscountCalculator.cs`
- Line: 11-12 (the outer `Math.Round` call).
- What goes wrong: The outer rounding also changed to `ToZero`. This only matters when the input has more than two decimals, since the discounted value is then no longer exact at 2 decimals. Example: `amount` 10.555, `discountPercent` 0. The old code gives 10.56. The new code gives 10.55. For negative amounts (credit notes or refunds) the result also moves toward zero, so -10.555 gives -10.55 instead of -10.56.
- Confidence: high (about 85%) that this changes behavior. Medium-high (about 75%) that the change is a defect, since the previous convention was AwayFromZero and nothing here explains the switch.
- This finding is independent of Finding 1. It would still change results even if the discount were rounded correctly.

## Finding 3: No tests accompany the behavior change

- File: none. This diff contains no test changes.
- What goes wrong: `AGENTS.md` requires tests for every new rule, and rounding of money is a business rule that belongs in the Domain project. This diff changes the rounding rule with no test change. A single test for 19.99 at 10 percent would have caught Finding 1.
- Confidence: high (about 90%) that this diff contains no tests. Medium (about 60%) that the repository as a whole lacks a test for this method, because other files were outside the scope of this review.

## Finding 4: Readability of the rounding logic

- Line: 11-12. The intermediate `discounted` variable was removed and both rounding steps were folded into one nested expression. The order of operations (round the discount, subtract, round the result) is now hard to see, and that order is where Finding 1 comes from.
- Confidence: high that the code is harder to read. Severity is low, since this is a style point.

## Note on the hunk header

- Line 4: the header `@@ -12,9 +12,9 @@` declares 9 old and 9 new lines, but the hunk body contains 8 old and 8 new lines. This may be an artifact of how the diff was displayed. If the file is applied as shown, the patch may fail.
- Confidence: low (about 30%).

## Summary

Two correctness problems were introduced. Fractional-cent discounts are truncated, so customers are overcharged by up to one cent (Finding 1). The final rounding mode changed from AwayFromZero to ToZero, which truncates amounts with more than two decimals and moves negative results toward zero (Finding 2). Neither change is covered by a test (Finding 3). Unless truncation is intended, restore the original single `Math.Round(discounted, 2, MidpointRounding.AwayFromZero)` and add tests for the boundary cases above.

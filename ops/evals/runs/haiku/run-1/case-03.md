# Review: case-03.diff

Scope: one change to `backend/src/InvoiceApp.Domain/DiscountCalculator.cs`, method `Apply(decimal amount, decimal discountPercent)`.

## Findings

### 1. Final rounding mode silently changed from nearest to truncation
- File: `backend/src/InvoiceApp.Domain/DiscountCalculator.cs`, return statement (diff lines 11-12, roughly lines 16-17 of the new file).
- What goes wrong: the original code rounded the net amount with `MidpointRounding.AwayFromZero`. The new code uses `MidpointRounding.ToZero`, which truncates. Results change, and not only at the midpoint:
  - `Apply(10.00m, 12.355m)`: old 8.76, new 8.77. (Exact net is 8.7645; the discount is first truncated to 1.23, which is the cause here.)
  - `Apply(10.009m, 0m)`: old 10.01, new 10.00. (The final `ToZero` drops the 0.009.)
  - `Apply(-10.005m, 0m)`: old -10.01, new -10.00. (Negative amounts now round toward zero.)
- Impact: invoice totals can differ by one cent from the previous behaviour, in either direction, with no change to the method name, no tests, and no stated reason in the diff. If truncation is intended it should be a deliberate, documented decision; if not, this is a regression.
- Confidence: high that the output changes as described (checked by hand). High that this is a defect unless truncation was the goal, which the diff does not say.

### 2. Discount is rounded before it is subtracted (double rounding)
- File: `backend/src/InvoiceApp.Domain/DiscountCalculator.cs`, line 11 (`Math.Round(discount, 2, MidpointRounding.ToZero)`).
- What goes wrong: the original computed the net from the exact discount and rounded once. The new code rounds the discount to cents first, then rounds the net again. The two truncations push in different directions, so the result can be off from correct rounding by up to about a cent. The example in finding 1 (`12.355`) shows the discount step alone moving the answer by a cent.
- Confidence: high that there are two rounding steps and that the first one changes results. Medium on how serious this is for the business, since the intended rounding rule for discounts is not stated.

### 3. No test changes accompany a change to money rounding
- File: the diff touches only `DiscountCalculator.cs`.
- What goes wrong: AGENTS.md requires tests for every new rule. This change alters how invoice amounts are rounded, yet no test is added or updated in this diff, so the behaviour change in findings 1 and 2 would not be caught. I could not see the test project from this diff, so I cannot say whether an existing test covers `Apply`.
- Confidence: medium. The absence of test changes in the diff is certain. Whether a relevant existing test exists is unknown.

### 4. Readability (low severity)
- File: `backend/src/InvoiceApp.Domain/DiscountCalculator.cs`, diff lines 8-12.
- What goes wrong: the named `discounted` variable was removed and replaced by one expression with a nested `Math.Round` split across two lines. The steps (discount, net, final rounding) are harder to follow, and the two different rounding modes in one expression make the intent unclear.
- Confidence: high that the code is harder to read. Low severity.

## Summary

Defect present. The main problem is finding 1: the method's rounding behaviour changed from round-half-away-from-zero to truncation, with an extra rounding step on the discount. Finding 3 notes that nothing in the diff tests this change. Findings 2 and 4 are consequences or readability concerns. Code outside this diff, such as validation of `discountPercent`, was not reviewed.

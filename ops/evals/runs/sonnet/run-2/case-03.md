# Review of case-03.diff

## Finding 1: Rounding change silently overcharges customers by up to one cent

- File: backend/src/InvoiceApp.Domain/DiscountCalculator.cs
- Line: 11-12 of the diff (the new `return Math.Round(amount - Math.Round(discount, 2, MidpointRounding.ToZero), 2, MidpointRounding.ToZero);` in `Apply`)
- Confidence: high (about 90%)

What goes wrong: The old code computed `amount - discount` and rounded the result to the nearest cent, with midpoints going away from zero. The new code first truncates the discount toward zero to 2 decimals, then subtracts it. `MidpointRounding.ToZero` is not a midpoint tie-break. In .NET it is directed rounding, so it truncates every value toward zero. The discount is therefore always rounded down, in the company's favour, and the customer pays more.

Example: amount 10.04 with a 10.02% discount.
- The raw discount is 1.006008.
- Old result: 10.04 - 1.006008 = 9.033992, which rounds to 9.03.
- New result: the discount truncates to 1.00, so the result is 9.04.

The result is off by 0.01. It happens whenever the discount has a sub-cent remainder above half a cent. Across many invoices this gives a systematic bias, and totals will no longer match the previous behaviour or any externally computed figures.

Fix: Revert to rounding the final amount to nearest, as before. If the discount must be rounded first for presentation, use `MidpointRounding.AwayFromZero` (or the agreed policy) and state the policy explicitly.

## Finding 2: Outer `Math.Round` is redundant, and the double rounding hides the real behaviour change

- File: backend/src/InvoiceApp.Domain/DiscountCalculator.cs
- Line: 11-12 of the diff
- Confidence: medium-high (about 75%)

What goes wrong: When `amount` has at most 2 decimals, `amount - roundedDiscount` already has at most 2 decimals, so the outer `Math.Round(..., ToZero)` does nothing. When `amount` has more than 2 decimals, the outer truncation silently drops fractional cents, which is again biased upward for the payer. Either way the nested rounding adds complexity with no benefit, and it makes the bias in Finding 1 harder to see.

## Finding 3: No test change accompanies a behaviour change in a business rule

- File: backend/src/InvoiceApp.Domain/DiscountCalculator.cs (no test file in the diff)
- Line: not applicable (whole diff)
- Confidence: medium (about 70%)

What goes wrong: The diff changes the rounding rules of a money calculation but adds or updates no tests. Project convention says every new or changed rule needs tests. Existing tests that assert nearest-cent / away-from-zero results will either fail or, if they only use values with no sub-cent remainder, will not catch the regression. There is no test for a case such as 10.04 at 10.02%, which is where the old and new behaviour differ.

Fix: Add a `DiscountCalculatorTests` case for amounts and percentages that produce a sub-cent remainder above half a cent. Assert the intended policy explicitly.

## Not flagged

No overflow, null or threading problems were visible in the diff. Whether `discountPercent` is validated (negative values, values over 100) cannot be seen from the diff, so I make no claim about it.

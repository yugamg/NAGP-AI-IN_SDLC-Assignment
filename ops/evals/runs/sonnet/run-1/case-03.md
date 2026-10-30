# Review of case-03.diff

## Finding 1: Rounding change causes wrong discounted totals (off by one cent)

- File: backend/src/InvoiceApp.Domain/DiscountCalculator.cs
- Line: 11-12 of the diff (the new `return` in `Apply`, hunk starting at line 12 of the file)
- Confidence: high

What goes wrong: The old code computed `amount - discount` and rounded the final result to 2 decimals with `MidpointRounding.AwayFromZero`. The new code first rounds the discount itself with `MidpointRounding.ToZero`, then subtracts, then rounds again with `ToZero`. `MidpointRounding.ToZero` does not round to nearest. It truncates every value, not only exact midpoints. The discount is therefore always cut down, so the discounted price is pushed up. For positive inputs the result can be 0.01 higher than the old result and than the mathematically correct rounded value.

Example: amount 19.99 with a 33% discount.
- Exact discount is 6.5967. The new code truncates it to 6.59.
- The new result is 13.40.
- The old result was round(13.3933) = 13.39.

The customer is overcharged by a cent. Across many invoices this gives totals that do not match the previous behaviour or the expected "round half away from zero" convention. Existing tests that assert rounded results (for example a case that expects an `AwayFromZero` midpoint to round up) may fail or may be silently weakened.

The outer `Math.Round(..., ToZero)` is also redundant when `amount` has at most 2 decimals. When `amount` has more than 2 decimals, it truncates instead of rounding.

Suggested fix: Revert to rounding once, at the end, with `MidpointRounding.AwayFromZero`:
`Math.Round(amount - amount * (discountPercent / 100m), 2, MidpointRounding.AwayFromZero)`.
If the intent is to round the discount first, use `AwayFromZero` for it too, and state the intent in the PR.

## Finding 2: Behaviour change with no accompanying tests or stated rationale

- File: backend/src/InvoiceApp.Domain/DiscountCalculator.cs (the whole diff)
- Line: 11-12 of the diff
- Confidence: medium

What goes wrong: The diff changes the money-rounding rules in a domain rule class without touching any test file. The project convention is that every changed rule needs tests (using `FakeInvoiceRepository` where relevant). No test pins down the new truncation behaviour, so the regression in Finding 1 would not be caught.

## Minor note (low confidence)

For negative amounts (credit notes or negative discounts), `ToZero` rounds toward zero, which is asymmetric compared with the old `AwayFromZero` behaviour. Whether this is intended is unclear. I could not check how callers use negative values, because I only reviewed this diff.

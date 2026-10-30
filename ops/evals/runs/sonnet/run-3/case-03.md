## Review of case-03.diff

### Finding 1: Rounding behavior changed, so discounted totals can differ by one cent from before

- File: backend/src/InvoiceApp.Domain/DiscountCalculator.cs
- Line: 11-12 of the diff (the new `return Math.Round(amount - Math.Round(discount, 2, MidpointRounding.ToZero), 2, MidpointRounding.ToZero);` in `Apply`)
- What goes wrong: The old code computed the exact discounted amount and rounded it once, half away from zero. The new code first rounds the discount to 2 decimals with `MidpointRounding.ToZero`, which truncates and does not round to nearest. It then subtracts that truncated discount from the amount. Truncating the discount makes the discount too small, so the customer is overcharged by up to just under one cent. Example: amount 10.05 at 15% gives a discount of 1.5075. Old result: 10.05 - 1.5075 = 8.5425, rounded to 8.54. New result: discount truncated to 1.50, so 8.55. The result is off by a cent from the old behavior and from correct rounding. Invoice totals computed with this method will no longer match the previous results or any existing expected values.
- Confidence: high (about 90%) that the behavior changes and is wrong for non-trivial inputs. It is not a crash. It is a silent numeric regression.

### Finding 2: `MidpointRounding.ToZero` is used as if it rounded normally

- File: backend/src/InvoiceApp.Domain/DiscountCalculator.cs
- Line: 11-12 of the diff
- What goes wrong: `MidpointRounding.ToZero` is a directed rounding mode, not a tie-breaking rule. It truncates every value, not only exact midpoints. So the outer `Math.Round(..., 2, ToZero)` also truncates the final amount instead of rounding it. For ordinary two-decimal amounts the outer round changes nothing. If `amount` ever carries more than 2 decimals, the final total is silently cut down instead of rounded. Combined with the inner truncation, the two roundings can push the result in opposite directions, and the result is hard to reason about.
- Confidence: medium-high (about 75%). The truncation is certain. Whether it hurts depends on whether callers pass amounts with more than 2 decimals.

### Finding 3: No tests changed with a rule change

- File: backend/src/InvoiceApp.Domain/DiscountCalculator.cs (the diff touches no test files)
- Line: whole hunk
- What goes wrong: The diff changes a money-rounding rule without adding or updating any test. The project convention says every new rule needs tests. Any existing test that asserts the half-away-from-zero result may now fail. If none exist, the regression in Finding 1 is not caught.
- Confidence: medium (about 60%). I can only see this diff, so I cannot tell whether tests exist elsewhere.

### Not flagged

I saw no compile error, null issue, or injection risk. `MidpointRounding.ToZero` exists in .NET Core 3.0+ and .NET 8. The removed blank line and the `discounted` variable are cosmetic.

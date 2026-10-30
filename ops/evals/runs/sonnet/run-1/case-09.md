## Review of case-09.diff

### Finding 1: README.md, line 13 (added line "To reset the sample data without restarting, sign out and sign in again.")
- What goes wrong: The diff does not show any code that resets data on sign-out or sign-in. The sentence just above it says the data lives in an in-memory store that is recreated only when the process starts. Those two statements conflict. Signing out and in normally only ends and starts a session, and it will not reseed the repository. A reader who follows this instruction would expect the sample data to reset and it would not. This is a documentation defect, and it could lead someone to wrongly think they have a clean data set.
- Confidence: medium (about 65%). I only had the diff. If the app really does reseed on sign-in, the sentence is accurate, but that would be unusual behaviour and I would expect it to appear in the diff. The reset claim is unsupported by anything visible.
- Suggested fix: Remove the sentence, or replace it with "restart the app to reset the data". Alternatively, verify the sign-in behaviour in code and document it only if it is true.

### Finding 2: README.md, lines 9-11 (the claim that the demo accounts "exist only in the in-memory store and are recreated on every start")
- What goes wrong: This is a factual claim about how the app is implemented that cannot be checked from the diff. In the best case it is accurate. In the worst case the demo accounts are hardcoded in sign-in code and are not stored in the repository, and the data-loss sentence applies only to the invoices.
- Confidence: low (about 25%). It is a minor wording concern, not a clear defect.

### Not a defect: backend/src/InvoiceApp.Api/appsettings.json
Adding `"Microsoft.AspNetCore": "Warning"` is valid JSON, the comma is placed correctly, and the key follows the standard ASP.NET Core logging category pattern. It only lowers framework log noise and leaves application logs at Information. I found no problem here (high confidence).

### Summary
There are no code defects in this diff. The only issue worth raising is the unsupported "sign out and sign in again to reset" instruction in the README (Finding 1).

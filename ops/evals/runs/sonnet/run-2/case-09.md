## Review of case-09.diff

### Finding 1: README.md, line 13 of the diff (new line "To reset the sample data without restarting, sign out and sign in again.")

What goes wrong: this adds a behavioral claim that nothing else in the diff supports. Nothing in the diff changes sign-in or sign-out so that it resets data. The preceding added lines say the data lives in an in-memory store that is recreated only when the process starts. An in-memory repository registered as a singleton normally survives sign-out and sign-in, because those only touch the session or auth state. If so, a reader who follows this instruction will believe the data was reset when it was not. They may then trust stale or modified invoices when demoing or testing. The sentence also contradicts the sentence just above it, which says data is lost "when the process stops".

I could not check the sign-in or repository code because this review covers only the diff. If sign-out does not reseed the store, the line should be removed, or the reset behavior should be implemented and tested.

Confidence: medium (about 65%). The claim is unsupported and probably false, but the review cannot see the code that would settle it.

### Finding 2: README.md, lines 9-11 of the diff ("These accounts exist only in the in-memory store and are recreated on every start")

What goes wrong: this is another factual claim about runtime behavior that the diff does not support. It may be accurate, but it is stated as fact. If the demo accounts are defined in code or configuration rather than in the store, the wording is misleading, though harmless.

Confidence: low (about 25%). Worth a quick check, but I would not block on it.

### Not a defect

The appsettings.json change adds `"Microsoft.AspNetCore": "Warning"` under `LogLevel`. The JSON is valid, the comma is placed correctly, and this is a standard way to reduce framework log noise. I found no problem there.

### Summary

There are no code defects in the diff. The one finding worth acting on is the unverified "sign out and sign in again" reset instruction in the README (Finding 1).

# Review: case-09.diff

The diff changes two files: a README paragraph and a logging level in appsettings.json. I found no code defects. The hunk headers and line counts are consistent, and the JSON in appsettings.json stays valid. I did have one finding in the README, with moderate confidence.

## Findings

### 1. README.md, line 25: the reset instruction is probably wrong (confidence: moderate)

"To reset the sample data without restarting, sign out and sign in again."

Nothing in this diff shows a reset mechanism. Sign-in and sign-out only touch the session, so they would not clear the in-memory invoice store. The paragraph just above (lines 21-23) says the accounts and data live in the in-memory store and are recreated only on process start, which makes the sign-out/sign-in claim contradict itself. I could not confirm the actual sign-out behaviour from this file alone. If the store is not cleared by sign-out, the sentence should be removed, or replaced with "restart the process to reset the sample data."

### 2. README.md, line 21: credential pointer goes to an internal file (confidence: low)

"Sign in with one of the demo accounts listed in AGENTS.md."

AGENTS.md is written for AI agents and contributors. A README reader may not have it, or may not expect it to hold the demo credentials. This is a documentation nit, not a defect. Listing the accounts in the README, or pointing to a dedicated setup section, would be clearer.

### 3. appsettings.json, line 5: Microsoft.AspNetCore lowered to Warning (confidence: low, informational)

`"Microsoft.AspNetCore": "Warning"` applies to every environment, since the file has no Development override. This suppresses Information-level framework logs, which can hide useful diagnostics such as authentication or routing details during local debugging. The change looks intentional, so I am noting it only so it is a conscious choice. Moving it into appsettings.Development.json would keep production logging unchanged.

## Checked and found no issues

- Hunk headers match their line counts in both files (`-18,7 +18,11` and `-1,7 +1,8`).
- The comma added after `"Default": "Information"` is correct, and the new key is valid JSON.
- The README wording about in-memory data loss on process stop is consistent with an in-memory repository, as described in the diff's context.

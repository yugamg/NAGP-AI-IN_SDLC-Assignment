# Review of case-09.diff

The diff changes two files: README.md and backend/src/InvoiceApp.Api/appsettings.json.

## Finding 1

- File: README.md, line 25 (new file numbering; diff line 13)
- Text: "To reset the sample data without restarting, sign out and sign in again."
- What goes wrong: The sentence says that signing out and back in resets the sample data. Nothing in this diff supports that. The diff shows that the demo accounts and data live in the in-memory store, and the store is only recreated when the process restarts (the README's previous sentence says the same). Signing out only ends the session. It does not clear the repository, so a user who follows this instruction will think the data was reset when it was not. The documentation is wrong, or it describes a reset feature that the diff does not add. I could not check the rest of the codebase for a reset path, so this rests on the diff and the project notes.
- Confidence: about 75 percent that the claim is incorrect as written.

## Finding 2 (judgment call, not a defect)

- File: backend/src/InvoiceApp.Api/appsettings.json, line 5 (new file numbering; diff line 26)
- Text: `"Microsoft.AspNetCore": "Warning"`
- What goes wrong: This raises the minimum log level for all ASP.NET Core namespaces from Information to Warning. The JSON is valid and the hunk header counts are consistent. The side effect is that Information-level framework logs are suppressed, and these can include authentication and request logs that someone would need when investigating a sign-in problem or an incident. The change is reasonable for reducing noise, but the commit should say that it is intentional and that application logs (not framework logs) are what remain visible.
- Confidence: about 40 percent that this causes a real problem. Low severity.

## Other checks

- Hunk headers in both files match the lines in the hunks.
- The README edit does not contradict AGENTS.md, which describes the in-memory repository and the demo accounts.
- I found no code, business logic, credentials or security-relevant changes in this diff.

## Scope limit

I reviewed only the diff file, as instructed. I did not open the source code, so I could not confirm whether a reset path exists or whether the logging change affects any specific audit code.

# Review of case-09.diff

## Findings

### 1. README.md, line 25: reset claim is not supported by the change
- **What goes wrong:** The added sentence "To reset the sample data without restarting, sign out and sign in again." says signing out and back in resets the sample data. The same paragraph says the data lives only in the in-memory store and is lost when the process stops. Signing out does not stop the process, so the in-memory data should persist across a sign-out and sign-in. The documentation therefore tells readers how to do something that the described system does not do. Nothing in this diff implements a reset.
- **Confidence:** about 75%. I could only see the diff, not the repository code, so I cannot confirm how the in-memory store handles sign-out. The claim is unsupported by anything visible in the diff, and it contradicts the restart-only reset described in the preceding sentence.

### 2. backend/src/InvoiceApp.Api/appsettings.json, lines 24-26: no defect found
- The JSON stays valid. The comma after `"Information"` was added, and the new `"Microsoft.AspNetCore": "Warning"` entry is the last item in the object with no trailing comma.
- The change lowers ASP.NET Core logging to Warning, which hides request and routing information logs. This is a deliberate tradeoff, not a defect.
- **Confidence:** about 90% that there is no defect in this file.

## Summary

One probable defect: the README tells users that signing out and back in resets the sample data, which the in-memory design described in the same paragraph does not support (about 75% confidence). No defects found in the appsettings.json change.

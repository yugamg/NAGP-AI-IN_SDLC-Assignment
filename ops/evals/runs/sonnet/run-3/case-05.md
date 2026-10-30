# Review of case-05.diff

## Finding 1: Delete endpoint has no role restriction (authorization gap)

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Line: 8-9 of the diff (the new `[HttpDelete("{id}")]` / `Delete(string id)` action, roughly new lines 44-51)
- What goes wrong: The new `Delete` action has no `[RequireRole(...)]` attribute. The existing `Update` action directly below it is marked `[RequireRole("Admin")]`, which shows the controller expects role checks on mutating actions. Without the attribute, any signed-in user, whatever their role, can permanently delete any invoice. Deletion is destructive and should be at least as restricted as `Update`, so this needs `[RequireRole("Admin")]`.
- Confidence: High (about 90%). The one uncertainty is whether the controller or `ApiControllerBase` applies a class-level role default. The diff shows no sign of one, because `Update` carries its own explicit attribute.

## Finding 2: Delete bypasses the `Handle(...)` wrapper, so a missing invoice is not handled

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Line: 11-13 of the diff (`_repository.Delete(id);` followed by `return NoContent();`)
- What goes wrong: The neighbouring code uses `Handle(() => ...)` (see the `Add` call in the context lines) to map service errors to HTTP status codes. `Delete` calls the repository directly. If the repository throws for an unknown id, the client gets a 500 instead of a 404. If it silently ignores unknown ids, the client gets 204 and the "deleted" log line is written for something that never existed. Either way the behaviour is wrong or inconsistent with the other endpoints.
- Confidence: Medium (about 60%). It depends on how `_repository.Delete` behaves, and that is not visible in the diff.

## Finding 3: No lifecycle check before deleting

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Line: 11 of the diff (`_repository.Delete(id);`)
- What goes wrong: Invoices appear to follow a lifecycle (the approval flow). The controller deletes straight through the repository with no check of invoice state. A user could delete an Approved or PendingApproval invoice and remove the audit trail. Any rule on which states may be deleted should live in the service or domain layer, not in a controller that talks to the repository directly.
- Confidence: Low to medium (about 40%). The diff does not show the intended business rules.

## Finding 4: No tests included

- File: the whole diff (no test file is touched)
- Line: not applicable
- What goes wrong: A new destructive endpoint is added with no test for the happy path, the unknown id case or the authorization check. A test for the role check would have caught Finding 1.
- Confidence: High (about 85%) that tests are missing from this diff. It is only a defect if the change is meant to ship with its own tests.

## Minor note

- Line 12 of the diff logs `id`, a user-supplied route value, via structured logging. This is fine as written because the template parameters are structured and not concatenated. No action needed.

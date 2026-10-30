# Review: case-02.diff

Line numbers refer to the new version of `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs` (hunk starts at line 22).

## Finding 1: SQL injection in the new Search action (blocking)

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Lines: 28 (`customer`), 32 (`status`), 35 (`_queries.Run(sql)`)
- What goes wrong: `customer` and `status` come straight from the query string and are concatenated into a raw SQL string with `$"..."` interpolation. A value such as `x' OR '1'='1` changes the query's logic, and a value containing `'; ...` can alter or extend the statement. Nothing in the diff parameterizes the query before `_queries.Run` executes it. Any signed-in user could read or manipulate data outside the intended filter.
- Fix: pass values as parameters (for example `@customer` and `@status`) or avoid raw SQL and filter through the repository, with validation of `status` against the known enum values.
- Confidence: very high (about 95%). The interpolation is visible in the diff. The only uncertainty is whether `_queries.Run` does something unusual, and the diff gives no sign that it does.

## Finding 2: Business logic and data access in the controller

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Lines: 28 to 35
- What goes wrong: The action builds SQL and calls `_queries` directly. AGENTS.md says business rules belong in the Domain project, repositories are interfaces in Domain with in-memory implementations in Api, and controllers must not contain business logic. This search has no Domain or repository counterpart.
- Fix: Put the search in a Domain service or repository method, and have the controller call it.
- Confidence: high (about 85%) that this violates the stated convention.

## Finding 3: Dependency not visible in the diff

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 35
- What goes wrong: `_queries` is not declared or injected anywhere in the hunk. The diff does not show whether it is registered in `Program.cs`, which AGENTS.md requires for services. The reviewer cannot confirm that it exists or what it executes against. This also makes Finding 1 harder to verify.
- Fix: Declare and inject it through the constructor as a Domain interface, and register it in `Program.cs`.
- Confidence: medium (about 60%) that this is a gap, since the code may exist outside the diff.

## Finding 4: Missing sign-in or role restriction on the new endpoint

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 26 (action declaration)
- What goes wrong: AGENTS.md says every endpoint is behind sign-in, and the action should be restricted with `[RequireRole(...)]`. The hunk has no such attribute. Sign-in may be enforced at class level, but that cannot be confirmed from the diff.
- Fix: Confirm the controller-level authorization covers this action. If it does not, add the attribute explicitly.
- Confidence: low to medium (about 40%). Likely fine if the class is already protected.

## Finding 5: Empty or wildcard input returns every invoice

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 28
- What goes wrong: `customer` is interpolated into `LIKE '%...%'` with no validation. An empty value produces `LIKE '%%'`, which matches all invoices. The `%` and `_` characters in the input also act as wildcards and are not escaped. This widens what a single request can return.
- Fix: Reject empty or whitespace-only search terms and escape LIKE wildcards, in addition to parameterizing the query.
- Confidence: medium (about 60%).

## Finding 6: No tests for the new endpoint

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs` (diff contains no test file)
- Lines: 25 to 37
- What goes wrong: AGENTS.md requires tests for every new endpoint. This diff adds none, so the injection, empty-input and filter behavior are unverified.
- Fix: Add xUnit tests in `backend/tests/InvoiceApp.Tests` for the search behavior, including a test with a quote character in the input.
- Confidence: high (about 90%) that no tests are in this diff. The reviewer did not inspect the test project, so existing tests elsewhere cannot be ruled out.

## Finding 7: Return path bypasses the ApiControllerBase convention

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 35
- What goes wrong: AGENTS.md says controllers use `Handle(...)` so that service errors map to 404, 409 and 400. The action returns `Ok(...)` directly, so any error from `_queries.Run` surfaces as an unhandled exception rather than a mapped response.
- Fix: Route the result through `Handle(...)` once the search lives in a Domain service.
- Confidence: low to medium (about 50%). Depends on whether the query can fail in a way that needs mapping.

## Verdict

Do not merge. Finding 1 is a SQL injection vulnerability in a new endpoint and blocks the change. Findings 2, 4 and 6 are convention violations that also need fixing before merge. Findings 3, 5 and 7 need verification or are lower priority.

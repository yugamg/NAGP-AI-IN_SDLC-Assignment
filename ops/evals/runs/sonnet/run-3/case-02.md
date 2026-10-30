# Review of case-02.diff

## Finding 1: SQL injection in `Search` (critical)

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Lines: 11 and 15 of the diff. These are the `$"SELECT * FROM Invoices WHERE CustomerName LIKE '%{customer}%'"` line and the `sql += $" AND Status = '{status}'"` line.
- What goes wrong: Both `customer` and `status` are taken from the query string and interpolated straight into a SQL string, which is then run through `_queries.Run(sql)`. Anyone who can call the endpoint can inject SQL. For example, `customer=' OR 1=1 --` returns every invoice, and `customer='; DROP TABLE Invoices; --` or a `UNION SELECT` can destroy or exfiltrate data. `status` is injectable in the same way.
- Fix: Use parameterized queries (`@customer`, `@status`) or an ORM/LINQ filter. Better still, filter through the repository or a Domain service instead of building SQL text.
- Confidence: very high (about 99%).

## Finding 2: `_queries` is not declared or registered (compile error)

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 18 of the diff, `_queries.Run(sql)`.
- What goes wrong: The diff shows no `_queries` field, no constructor parameter, and no registration in `Program.cs`. The surrounding code uses `_repository`. Unless `_queries` already exists in the unchanged part of the file, this won't compile. Note also that the project conventions say the repository is an in-memory implementation with no SQL layer, so a raw-SQL runner has no obvious backing and would introduce a new data-access path.
- Confidence: medium (about 60%). It depends on code outside the diff.

## Finding 3: `customer` is a non-nullable `string` with no validation

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 9 of the diff.
- What goes wrong: `[FromQuery] string customer` is non-nullable with no `[Required]` attribute. If nullable reference types are enabled with the implicit `[Required]` behavior, a missing parameter yields a 400. If not, `customer` is `null` and the query becomes `LIKE '%%'`, which matches everything. Behavior is ambiguous either way, and there is no length limit or trimming.
- Confidence: low to medium (about 40%).

## Finding 4: Business logic in the controller, no tests, no role restriction

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Lines: 8 to 19 of the diff.
- What goes wrong: The project rules say business rules belong in the Domain project, not in controllers, and that every new endpoint needs tests. This diff adds search logic in the controller and no tests. The endpoint also has no `[RequireRole]`. The rules say every endpoint is behind sign-in, which probably applies through a controller or base class, but I can't confirm that from the diff.
- Confidence: medium (about 60% on the missing tests and the placement of logic; about 30% on the missing role attribute).

## Finding 5: Route ordering and minor issues

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 8 of the diff.
- What goes wrong: `[HttpGet("search")]` sits next to `[HttpGet("{id}")]`. ASP.NET Core prefers literal segments over parameters, so this should route correctly. It is not a defect and I'm noting it only for completeness. `status` is also an unvalidated free-form string rather than an enum or an allowed list of the lifecycle states (Draft, PendingApproval, Approved, Rejected).
- Confidence: low (about 20%).

## Summary

The blocking issue is the SQL injection in Finding 1. It must be fixed before merge.

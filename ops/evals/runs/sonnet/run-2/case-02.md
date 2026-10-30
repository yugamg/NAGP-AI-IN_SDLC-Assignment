# Review of case-02.diff

## Finding 1: SQL injection via `customer` parameter

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Line: 11 of the diff (new line in `Search`): `var sql = $"SELECT * FROM Invoices WHERE CustomerName LIKE '%{customer}%'";`
- What goes wrong: The user-supplied `customer` query string value is interpolated directly into a SQL string. A caller can pass something like `' OR 1=1 --` to return every invoice, or use UNION or stacked statements to read other tables, modify data, or drop tables, depending on what `_queries.Run` executes against. This is a classic injection flaw, and it sits on an endpoint that takes untrusted input.
- Fix: Use parameterized queries (`@customer` with a bound value, and escape or bind the `%` wildcards) or go through the repository with LINQ filtering.
- Confidence: Very high (about 99%).

## Finding 2: SQL injection via `status` parameter

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Line: 15 of the diff: `sql += $" AND Status = '{status}'";`
- What goes wrong: The same string interpolation of untrusted input as in Finding 1. Passing `' OR '1'='1` as `status` bypasses the filter, and other payloads allow arbitrary SQL. Validating `status` against the invoice status values would remove this vector.
- Fix: Bind it as a parameter, or parse it into the status enum and reject unknown values.
- Confidence: Very high (about 99%).

## Finding 3: Endpoint has no role restriction, and conventions are broken

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Line: 8-9 of the diff (`[HttpGet("search")] public IActionResult Search(...)`)
- What goes wrong: The action has no `[RequireRole(...)]` attribute. The project says every endpoint is behind sign-in, and the controller likely inherits that, but a raw SQL search that can expose any data deserves an explicit role check. Also, the query logic sits in the controller. Per the project conventions, business rules belong in the Domain project, and a repository interface should provide data access. The controller does not use `Handle(...)`.
- Confidence: Medium (about 60%). This is a convention and defense-in-depth issue, not a certain functional bug. I could not verify the controller's class-level attributes from the diff alone.

## Finding 4: `_queries` is not declared or registered in the diff

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Line: 18 of the diff: `return Ok(_queries.Run(sql));`
- What goes wrong: The diff does not add a `_queries` field, a constructor parameter, a DI registration in `Program.cs`, or a definition of `Run`. If it does not already exist, the code will not compile. If it does exist, it is an arbitrary raw-SQL execution abstraction that does not match the in-memory repository described in the project conventions, which is a design concern in itself.
- Confidence: Medium (about 55%). The diff shows only a small hunk, so the field may exist elsewhere in the file.

## Finding 5: `customer` is a non-nullable string with no validation

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Line: 9 of the diff: `[FromQuery] string customer`
- What goes wrong: With nullable reference types enabled, an omitted `customer` yields a 400 from model validation (implicit required). If nullable is disabled, it is `null`, and the SQL becomes `LIKE '%%'`, which matches every row. No length limit or trimming is applied either. The behavior is inconsistent, and a missing parameter silently becomes a full table scan.
- Confidence: Low to medium (about 40%).

## Finding 6: No tests

- File: backend/tests (none changed in the diff)
- What goes wrong: The project requires tests for every new endpoint or rule. The diff includes none, and an injection test would have caught Findings 1 and 2.
- Confidence: High (about 90%) that tests are missing from this diff.

## Summary

The critical issue is the SQL injection in two places (Findings 1 and 2), which should block the merge. The other findings are lower-confidence design and convention issues.

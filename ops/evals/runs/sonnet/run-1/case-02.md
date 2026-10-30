# Review of case-02.diff

## Finding 1: SQL injection in Search endpoint (critical)

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Lines: 11 (customer) and 15 (status) in the diff, i.e. the `Search` method added around new line 25-34
- What goes wrong: `customer` and `status` come straight from the query string and are interpolated into a SQL string (`$"... LIKE '%{customer}%'"` and `$" AND Status = '{status}'"`). An attacker can send `customer=' OR '1'='1` to dump every invoice, or `customer=x'; DROP TABLE Invoices; --` or a UNION SELECT to modify or exfiltrate data from other tables, depending on what `_queries.Run` executes (stacked statements, permissions). The fix is to use parameterized queries (e.g. `@customer`, `@status` with `LIKE '%' || @customer || '%'` or a parameter value of `$"%{customer}%"`), or to filter in the domain/repository layer.
- Confidence: very high (about 99%). This is a textbook injection sink with unsanitized user input.

## Finding 2: Endpoint has no role restriction and exposes raw query execution (medium)

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Line: 8-9 (the `[HttpGet("search")]` attribute and method signature)
- What goes wrong: Project convention is that every endpoint is behind sign-in and restricted with `[RequireRole(...)]` where appropriate. If sign-in is applied globally this is only a hardening gap, but a raw-SQL endpoint amplifies the injection impact to anyone who can authenticate, including the lowest role.
- Confidence: low to medium (about 40%), because global auth may already cover it and I cannot see the rest of the controller.

## Finding 3: `_queries` is not declared or registered in the diff; likely compile error (medium)

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Line: 18 (`return Ok(_queries.Run(sql));`)
- What goes wrong: The diff only shows `_repository` being used in the surrounding context. No `_queries` field, constructor parameter, or `Program.cs` registration is added. Unless it already exists, the build fails. Also, the project architecture (AGENTS.md conventions as I understand them from the task context) is an in-memory repository with no SQL database; a `Run(string sql)` abstraction does not fit that design.
- Confidence: medium (about 60%), limited by seeing only the hunk, not the whole file.

## Finding 4: `customer` is non-nullable but not validated (low)

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Line: 9
- What goes wrong: `[FromQuery] string customer` is required. If omitted, ASP.NET model validation (with `[ApiController]` and nullable reference types) returns 400, which is acceptable. However, an empty string yields `LIKE '%%'` and returns every invoice, with no limit or pagination. Minor behavior and performance issue.
- Confidence: low (about 30%).

## Finding 5: Business and data-access logic placed in the controller, and no tests (low to medium)

- File: backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs
- Lines: 8-19
- What goes wrong: Query construction lives in the controller instead of the Domain or repository layer, and the diff adds no tests for the new endpoint. This conflicts with the stated project conventions (business rules in Domain, every new endpoint needs tests, controllers use `Handle(...)`).
- Confidence: medium (about 60%) that this violates conventions; it is not a runtime defect.

## Summary

The blocking defect is the SQL injection at lines 11 and 15. This change should not be merged until the query is parameterized (or replaced by a repository-based filter) and tests are added.

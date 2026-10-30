# Review: case-02.diff

## Finding 1: SQL injection in the new Search action (critical)

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 28 (`var sql = $"SELECT * FROM Invoices WHERE CustomerName LIKE '%{customer}%'";`)
- What goes wrong: the raw `customer` query-string value is interpolated straight into the SQL text. Input such as `' OR '1'='1` or `x'; DROP TABLE Invoices; --` changes the statement that runs. Any caller can read or modify data beyond the intended search.
- Confidence: high (about 95%).
- Fix: use parameterized queries (or a query API that takes parameters) and never build SQL from request input.

## Finding 2: Same injection through the `status` filter (critical)

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 32 (`sql += $" AND Status = '{status}'";`)
- What goes wrong: `status` is appended to the SQL unescaped and unvalidated, so it is an independent injection point even if `customer` were fixed. The value is also not checked against the allowed statuses (Draft, PendingApproval, Approved, Rejected).
- Confidence: high (about 95%).
- Fix: bind `status` as a parameter, or validate it against the known enum values before use.

## Finding 3: Raw SQL is executed on the request path (high)

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 35 (`return Ok(_queries.Run(sql));`)
- What goes wrong: the controller passes a composed SQL string to `_queries.Run`. This sends the query to the data layer with no parameter support, which is what makes Findings 1 and 2 exploitable. The search belongs behind a parameterized repository or domain method.
- Confidence: medium (about 70%). The diff does not show what `_queries` is or whether it accepts parameters.

## Finding 4: Empty or wildcard `customer` returns every invoice (medium)

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 28
- What goes wrong: when `customer` is empty, the pattern becomes `LIKE '%%'`, which matches every row. A caller can list all customers' invoices in one request. `%` and `_` typed into `customer` also act as LIKE wildcards.
- Confidence: medium (about 65%). Whether this is intended behaviour cannot be determined from the diff.

## Finding 5: `_queries` dependency not visible in the diff (low, unverified)

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 35
- What goes wrong: `_queries` is used but no field, constructor parameter or registration appears in the diff. If it is not already declared and injected, the file will not compile. The diff alone cannot confirm this.
- Confidence: low (about 30%) that this is a real defect.

## Finding 6: Authorization not visible on the new endpoint (low, unverified)

- File: `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`
- Line: 25-26 (`[HttpGet("search")]` and `public IActionResult Search(...)`)
- What goes wrong: the action has no attribute that restricts who can call it. If the controller class does not carry an authorization attribute, any caller can reach this endpoint, which increases the impact of Findings 1 and 2.
- Confidence: low (about 30%). Class-level attributes are outside the diff.

## Summary

The diff has two critical SQL injection defects (lines 28 and 32) that let a caller alter the executed query. Line 28 also returns every invoice when `customer` is empty. Findings 3, 5 and 6 depend on code outside the diff and are noted with lower confidence.

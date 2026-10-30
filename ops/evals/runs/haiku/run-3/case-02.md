# Review: case-02.diff

Scope: one change to `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs`, adding a `GET search` action. Line numbers refer to the new file (hunk `+22,18`).

## Findings

### 1. SQL injection through `customer` (line 28)
- **File/line:** `InvoicesController.cs`, line 28: `var sql = $"SELECT * FROM Invoices WHERE CustomerName LIKE '%{customer}%'";`
- **What goes wrong:** The query parameter `customer` is interpolated directly into the SQL text. A value such as `x' OR '1'='1` or `'; DROP TABLE Invoices; --` changes the statement that `_queries.Run` executes. The result is data exposure across all customers and, depending on what `Run` permits, data modification.
- **Fix direction:** Use a parameterized query or a repository/domain method that filters in code. Do not build SQL in the controller.
- **Confidence:** High (about 95%). The concatenation is visible in the diff.

### 2. SQL injection through `status` (line 32)
- **File/line:** line 32: `sql += $" AND Status = '{status}'";`
- **What goes wrong:** Same defect as finding 1. `status` is user-controlled and unvalidated, so it can break out of the string literal and alter the statement. The `IsNullOrEmpty` check only guards against empty input.
- **Fix direction:** Parameterize it, or validate it against the known invoice status values (Draft, PendingApproval, Approved, Rejected) before use.
- **Confidence:** High (about 95%).

### 3. Business logic and data access in the controller (lines 26-36)
- **File/line:** the whole `Search` action.
- **What goes wrong:** AGENTS.md requires business rules to live in `backend/src/InvoiceApp.Domain` and says never to add business logic to controllers. This action builds filter logic and SQL in the controller. It also bypasses the repository pattern that AGENTS.md describes (interfaces in Domain, in-memory implementation in Api). That bypass is the reason the injection in findings 1 and 2 is possible.
- **Fix direction:** Add a search method on the domain service or repository interface that takes typed filter values, and call it from the controller through `Handle(...)`.
- **Confidence:** High (about 90%) that this violates the stated convention.

### 4. No tests added (diff-wide)
- **File/line:** the diff contains no test file.
- **What goes wrong:** AGENTS.md says every new endpoint or rule needs tests, using `FakeInvoiceRepository`. None are included, so the injection defects above would not be caught by the test suite.
- **Confidence:** High (about 90%) that the change is incomplete by project convention. The diff alone cannot prove that no test file exists elsewhere in the branch.

### 5. Authorization not visible on the new endpoint (line 26)
- **File/line:** line 26: `public IActionResult Search(...)` with no `[RequireRole]` attribute on the action.
- **What goes wrong:** AGENTS.md says every endpoint is behind sign-in and actions are restricted with `[RequireRole]`. The diff shows no role attribute on this action. If the controller has no class-level attribute either, the endpoint may be reachable without the required role. The diff does not show the class header, so this may be a false alarm.
- **Confidence:** Medium (about 50%). Verify the class-level attributes on `InvoicesController`.

### 6. `_queries` is not declared in the diff (line 35)
- **File/line:** line 35: `return Ok(_queries.Run(sql));`
- **What goes wrong:** `_queries` does not appear in this diff. If it is not a constructor-injected field that already exists, the file will not compile. If it exists, it is a raw-SQL runner that does not fit the in-memory repository design described in AGENTS.md. Either way, the change needs a look.
- **Confidence:** Medium (about 60%). The diff does not show the controller's fields or constructor.

### 7. Unescaped LIKE wildcards in `customer` (line 28)
- **File/line:** line 28.
- **What goes wrong:** Even with parameterization, `%` and `_` in the user's input act as wildcards, so a search for `_` matches every customer. A missing `customer` value binds to null and produces `'%%'`, which matches everything. This is a correctness issue, not a security one.
- **Confidence:** Low to medium (about 40%). It depends on how `customer` binds and what behavior is intended.

### 8. Routing (lines 25, 38)
- **What I checked:** `[HttpGet("search")]` is declared before `[HttpGet("{id}")]`. ASP.NET Core prefers literal segments over route parameters, so there is no routing conflict.
- **Confidence:** High (about 90%) that this is not a defect.

## Summary
Findings 1 and 2 are SQL injection in the new endpoint and should block the change. Findings 3 and 4 are violations of AGENTS.md conventions (logic in the controller, no tests). Finding 5 is a possible missing authorization attribute that needs checking against the class header. Findings 6 and 7 are lower confidence and depend on code that is not in the diff.

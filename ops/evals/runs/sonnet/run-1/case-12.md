# Review of case-12.diff

The diff adds one new file, `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`. It has two architecture tests that keep InvoiceApp.Domain free of web, storage and network dependencies. I found no defect that makes either test fail or crash on the current code. I did find several gaps where a rule violation would not be caught, and a few smaller issues. These are ordered by impact.

## Findings

### 1. `DomainDependencyTests.cs`, lines 22-27 and 64-71: the forbidden-reference list is too narrow for the stated rule
- **What goes wrong:** The class comment (line 13) says Domain depends on "nothing but the .NET base class library". The guidance text (line 19) says Domain must not depend on "storage". The only forbidden System prefixes are `System.Net`, `System.Data` and `System.Web`. Any other System.* assembly is allowed, for example `System.Diagnostics.Process`, `System.Net`-free file or IO helpers, `System.Security.Cryptography`, `System.IO.*` packages and `System.Text.Json`. On .NET 8, `File` and `Directory` live in `System.Runtime`, so file-system access from Domain is never flagged. The test passes while the rule is broken, so it gives false assurance.
- **Confidence:** medium (about 60%). Whether this counts as a defect depends on how strict the rule is meant to be. The test is weaker than its own description says.

### 2. `DomainDependencyTests.cs`, lines 50-62: the csproj test misses references that don't appear as `PackageReference`, `ProjectReference` or `FrameworkReference` elements inside the csproj
- **What goes wrong:** The test only looks at those three element names in `InvoiceApp.Domain.csproj`. It does not catch:
  - an SDK that implies a framework reference (for example `Sdk="Microsoft.NET.Sdk.Web"` on the `<Project>` element);
  - references added through `Directory.Build.props`, `Directory.Build.targets` or `Directory.Packages.props`;
  - `<Reference Include=...>` items with a HintPath;
  - references added with `Update=` or conditional items.

  The first test also cannot catch these if the code never uses the dependency. A Domain project switched to the Web SDK, or one that gets a package from a shared props file, passes both tests.
- **Confidence:** medium-high (about 70%) that the gap exists. Medium that it matters in practice.

### 3. `DomainDependencyTests.cs`, lines 38-47: the compiled-reference test only sees references the compiler actually emitted
- **What goes wrong:** `GetReferencedAssemblies()` returns only assemblies that the compiled code uses. The code comment on line 49 admits this. A package or project that Domain declares but does not yet use (for example a newly added EF Core or HTTP client reference) is not detected here. It is caught only by test 2, which has the gaps in finding 2. Constants are inlined, and type-forwarded types resolve to `System.Runtime`, so some real usage is also invisible. The first test is therefore much weaker than its name `DomainAssembly_ReferencesOnlyTheBaseClassLibrary` suggests.
- **Confidence:** high (about 85%) on the behaviour. Low-medium on whether it is a defect, since the author documented it.

### 4. `DomainDependencyTests.cs`, lines 6-8: the file relies on implicit usings
- **What goes wrong:** The file uses `Select`, `Where`, `OrderBy`, `Any`, `Contains`, `File`, `Path`, `DirectoryInfo`, `AppContext`, `StringComparison` and `FileNotFoundException`. The only explicit usings are `System.Xml.Linq`, `InvoiceApp.Domain` and `Xunit`. It compiles only if the test project has `<ImplicitUsings>enable</ImplicitUsings>`. If it does not, this file does not compile and breaks the whole test project build. I could not see the csproj in this diff.
- **Confidence:** low (about 25%). Most .NET 8 templates enable it.

### 5. `DomainDependencyTests.cs`, lines 73-84: the solution lookup is brittle
- **What goes wrong:** `FindDomainProjectFile` walks up from `AppContext.BaseDirectory` looking for a file named `InvoiceApp.sln`, then builds `src/InvoiceApp.Domain/InvoiceApp.Domain.csproj` relative to it.
  - It throws `FileNotFoundException` when the tests run from a copied or published output outside the repo (for example some CI stages that only ship binaries).
  - It fails if the solution is renamed or migrated to `.slnx`.
  - It fails if the layout changes.

  In those cases test 2 fails with an infrastructure error, not an architecture violation.
- **Confidence:** low-medium (about 35%). It works for the layout in AGENTS.md (`backend/InvoiceApp.sln`, `backend/src/InvoiceApp.Domain`).

### 6. `DomainDependencyTests.cs`, lines 56-59: false positives and an unclear message
- **What goes wrong:**
  - The test flags every `PackageReference`. That includes build-only items such as analyzers with `PrivateAssets="all"`, which add no runtime dependency. A legitimate analyzer package in Domain would fail the test.
  - A `PackageReference` that uses `Update=` instead of `Include=` produces an unhelpful message of the form `PackageReference ` with an empty name.
- **Confidence:** low (about 30%). It is cosmetic or a future false positive, not a current failure.

### 7. `DomainDependencyTests.cs`, lines 13-14: the comment refers to something that does not exist
- **What goes wrong:** The summary says "messaging and PayEx code live in InvoiceApp.Api". The repository conventions say messaging and a payment provider exist only in the planned deployment described under `ops/`, not in the code. The comment describes a system that does not exist yet and could mislead a reader.
- **Confidence:** medium (about 55%). It is documentation accuracy only.

## Checked and found correct
- The `IsBaseLibrary` pattern `x is "System" or "netstandard" or "mscorlib" || ...` has the intended precedence.
- `Assert.True(bool, string)` is valid for xUnit.
- The `for` loop in `FindDomainProjectFile` terminates correctly when `Parent` becomes null.
- Referencing `typeof(ApprovalService).Assembly` is a sound way to reach the Domain assembly.
- Assemblies such as `InvoiceApp.Api` and `Microsoft.*` are correctly treated as non-base libraries.

## Summary
No runtime bug, crash or incorrect assertion in the tests as written. The real weakness is that both tests can pass while the rule they claim to enforce is broken (findings 1 to 3). Findings 4 to 6 are low-probability robustness issues. Finding 7 is a documentation inaccuracy.

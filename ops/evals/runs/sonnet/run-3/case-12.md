# Review of case-12.diff

Scope: one new file, `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`, which adds two architecture tests that keep `InvoiceApp.Domain` free of web, storage and integration dependencies.

I found no high-confidence defects. The diff has no logic errors that I can show from the diff alone. The path logic, the LINQ and the pattern precedence all look correct. The findings below are low-confidence concerns, ordered from most to least likely to matter.

## Findings

### 1. Comment and guidance text describe components that do not exist in this codebase
- File: `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`
- Lines: 12-15 (doc comment), 18-20 (`Guidance` message)
- What goes wrong: The doc comment says "Web, storage, messaging and PayEx code live in InvoiceApp.Api behind Domain interfaces". No messaging or PayEx code exists in the repository today. Per the project conventions, those come from the planned production deployment described under `ops/`, which must not be treated as a description of the current system. The comment therefore states something untrue about the current layout. A developer who sees the failure message may go looking for a PayEx adapter in `InvoiceApp.Api` that is not there.
- Confidence: medium that this is a real inconsistency. Its impact is low because it only affects documentation text.

### 2. The forbidden-assembly list is narrow, so the test may give false assurance
- File: `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`
- Lines: 22-27 and 64-71
- What goes wrong: Only three BCL prefixes are forbidden (`System.Net`, `System.Data`, `System.Web`). The class summary and the `Guidance` text promise that Domain depends on "nothing but the .NET base class library" and must not use "storage, networking or integration code". Several BCL assemblies that could carry storage or integration code pass the check, for example `System.Diagnostics.Process`, `System.Private.Uri`, `System.Net`-adjacent `System.Security.*`, and `System.Data` replacements.
- File and process I/O types mostly resolve through `System.Runtime`, which the test must allow, so file-system access cannot be detected this way at all.
- The test is a reasonable approximation, but the claim in the comment is stronger than what it enforces.
- Confidence: medium. It is a design and coverage gap, not a bug.

### 3. The compiled-reference test only sees assemblies the Domain code actually uses
- File: `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`
- Lines: 36-47
- What goes wrong: `GetReferencedAssemblies()` lists only assemblies that the compiled code touches. A forbidden dependency that is declared but unused, or whose use is inlined (constants), is missed. The diff acknowledges this at line 49 and adds the csproj test as a backstop. The csproj test only inspects `InvoiceApp.Domain.csproj`. It does not look at shared MSBuild imports such as `Directory.Build.props` or `Directory.Packages.props`. A reference added there would pass both tests. This is a narrow gap.
- Confidence: low.

### 4. Conservative failure on legitimate `System.Net.*` usage
- File: `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`
- Lines: 22-27, 69-70
- What goes wrong: The prefix match on `System.Net` would flag harmless types such as `System.Net.HttpStatusCode`, which lives in `System.Net.Primitives`. If the Domain ever uses that type, the architecture test fails with a message about "networking code" even though no networking behaviour is involved. This is unlikely to happen today, because the controllers map errors to HTTP codes rather than the Domain.
- Confidence: low.

### 5. Test class naming and environment assumptions
- File: `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`
- Lines: 16 and 6-8
- What goes wrong:
  - The project convention is one test class per class under test, named `<ClassUnderTest>Tests`. `DomainDependencyTests` has no class under test, so it does not follow that convention. This is a style deviation only.
  - The file uses `Select`, `Where`, `Any`, `File`, `Path`, `DirectoryInfo` and `StringComparison` without importing `System`, `System.IO` or `System.Linq`. That compiles only if the test project has `ImplicitUsings` enabled. I cannot confirm this from the diff, so it is a possible compile failure if the setting is off.
  - `using InvoiceApp.Domain;` assumes `ApprovalService` is in that exact namespace, which I also cannot confirm from the diff.
- Confidence: low on the compile concerns, because they depend on files outside the diff. Medium on the naming deviation, which is cosmetic.

## Things checked that are correct
- `FindDomainProjectFile` walks up from `AppContext.BaseDirectory` to the directory containing `InvoiceApp.sln` and then goes to `src/InvoiceApp.Domain/InvoiceApp.Domain.csproj`. This matches a layout where the solution is `backend/InvoiceApp.sln`.
- The `is "System" or "netstandard" or "mscorlib" || ...` expression parses as intended.
- A missing `Include` attribute on a reference element yields a trailing space in the message but no exception.
- The `Assert.True(count == 0, ...)` calls give a useful failure message. They work, though `Assert.Empty` would be more idiomatic.

## Summary
No blocking defects. The most actionable item is finding 1: remove or correct the references to messaging and PayEx in the comment, since they describe planned rather than existing components. Findings 2 to 5 are low-severity or low-confidence.

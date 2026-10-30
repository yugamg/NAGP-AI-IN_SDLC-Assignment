# Review: case-12.diff

Adds `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`, two architecture tests that enforce "InvoiceApp.Domain depends on nothing but the .NET base class library."

## Findings

### 1. Allow-by-prefix lets non-BCL System.* assemblies through
- File: `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`
- Lines: 22-27 (deny-list), 64-71 (`IsBaseLibrary`)
- What goes wrong: `IsBaseLibrary` accepts any assembly whose name starts with `System.` and only rejects `System.Net`, `System.Data` and `System.Web`. Assemblies such as `System.Messaging`, `System.ServiceProcess.ServiceController`, `System.DirectoryServices`, `System.Configuration.ConfigurationManager` and `System.Drawing.Common` are not BCL, yet they pass the check. The doc comment on lines 13-14 says Domain must not contain "messaging" code, and the guidance on line 19 forbids "networking or integration code", so the test does not enforce the rule it documents. A Domain project that adds a messaging or directory-services reference would still pass the first test.
- Confidence: high (about 85%) that the gap exists. Medium that it matters today, since I cannot see the Domain project's current references from this diff.
- Suggested fix: switch to an allow-list of known BCL assembly names (or the `Microsoft.NETCore.App` reference set), or extend the deny-list to cover messaging, directory, service and configuration assemblies.

### 2. Second test only inspects one project file
- File: `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`
- Lines: 54-61
- What goes wrong: `DomainProject_DeclaresNoPackageProjectOrFrameworkReferences` loads only `src/InvoiceApp.Domain/InvoiceApp.Domain.csproj`. A reference added through `Directory.Build.props`, `Directory.Build.targets` or `Directory.Packages.props` (for example under `backend/`) would apply to the Domain project and is not seen by this test. The first test only catches such references once they are compiled in, so the "declared but unused" case that the comment on line 49 says this test covers is only partly covered.
- Confidence: medium (about 60%).
- Suggested fix: also scan the shared `Directory.Build.*` and `Directory.Packages.props` files above the project, or make the test evaluate the project through MSBuild.

### 3. Missing using directives may fail to compile
- File: `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`
- Lines: 40-44, 56-59, 66-67, 73-83
- What goes wrong: the file uses LINQ (`Select`, `Where`, `OrderBy`, `ToList`, `Any`, `Contains`), `File`, `Path`, `DirectoryInfo`, `FileNotFoundException`, `AppContext`, `StringComparison` and `string.Join`, but only `System.Xml.Linq`, `InvoiceApp.Domain` and `Xunit` are imported. This compiles only if the test project enables `ImplicitUsings` (which provides `System`, `System.IO`, `System.Linq`). The diff does not show the test `.csproj`, so I cannot confirm it.
- Confidence: medium-low (about 50%) that the build fails. Adding `using System; using System.IO; using System.Linq;` removes the dependency on that setting.

### 4. Failure to locate the project file gives an unclear error
- File: `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`
- Lines: 73-83
- What goes wrong: `FindDomainProjectFile` returns the expected csproj path without checking that it exists. If the folder layout changes, the failure comes from `XDocument.Load` as a file-not-found exception rather than a message that names the expected path.
- Confidence: high that this is the behavior. Severity is low.

### 5. Style: `Assert.True` with a count check
- File: `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`
- Lines: 46, 61
- What goes wrong: `Assert.True(forbidden.Count == 0, ...)` and `Assert.True(declared.Count == 0, ...)` would be clearer as `Assert.Empty(...)`, which reports the offending items without the hand-built message. The current messages do include the items, so the impact is small.
- Confidence: high. Nit only.

### 6. Possible convention deviation
- File: `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`
- Line: 16
- What goes wrong: AGENTS.md asks for one test class per class under test, named `<ClassUnderTest>Tests`. This class has no class under test; it checks assembly metadata for the whole Domain project. The convention does not clearly cover architecture tests, so this may be intended.
- Confidence: low that it is a defect.

## Summary

No functional defect is confirmed from the diff alone. The main concern is finding 1: the `System.*` allow-by-prefix check does not enforce the BCL-only rule the test documents. Finding 3 is a compile risk that depends on a project setting not shown in this diff.

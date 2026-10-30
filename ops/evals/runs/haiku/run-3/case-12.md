# Review: case-12.diff

The diff adds one file, `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`. Line numbers below refer to that new file (line 1 is the `using System.Xml.Linq;` line of the diff).

No defect found that would make the tests report a violation as passing when the project actually breaks the rule for the common cases (a forbidden `System.Net`/`System.Data`/`System.Web` reference, a non-System assembly such as Newtonsoft.Json, or a declared PackageReference, ProjectReference or FrameworkReference in the Domain csproj). The findings below are weaknesses and inconsistencies.

## Findings

1. **File line 35-40, `DomainAssembly_ReferencesOnlyTheBaseClassLibrary`, via `IsBaseLibrary` at lines 59-66.**
   The check treats any assembly whose name starts with `System.` as base class library. NuGet packages with `System.` names (for example `System.Text.Json`, `System.Reactive`, `System.Linq.Async`) therefore pass this test. The test name says it checks for base class library only, so it is weaker than it claims. The second test catches these only if they are declared directly in the csproj.
   Confidence: high that the logic accepts these packages. Medium that it matters in practice, because the second test covers the common case.

2. **File line 61-62 and line 65, `IsBaseLibrary` and `ForbiddenBaseLibraryPrefixes` (lines 17-22), against the doc comment at line 9.**
   The summary says Domain "depends on nothing but the .NET base class library", but the code forbids `System.Net`, which is part of the base class library. The rule and its doc comment disagree. Either the summary or the prefix list is wrong, and a future maintainer cannot tell which one reflects intent.
   Confidence: medium.

3. **File line 49-54, `DomainProject_DeclaresNoPackageProjectOrFrameworkReferences`.**
   The check reads only `Include` attributes in the Domain csproj. It misses `Update` and `Remove` items, references added through `Directory.Build.props` or other imported files, and references that a `Sdk` or `Directory.Build.targets` file adds. Those references are not visible to this test.
   Confidence: medium. This is a coverage gap, not a current failure.

4. **File line 53, `element.Attribute("Include")?.Value`.**
   If the element has no `Include` attribute, the message is built as `"PackageReference "` with a trailing space and no name. This only affects the failure message.
   Confidence: high that the message is poor. Low impact.

5. **File line 70-78, `FindDomainProjectFile`, and the call at line 49.**
   The method returns `src/InvoiceApp.Domain/InvoiceApp.Domain.csproj` under the solution folder without checking that the file exists. If the layout changes, `XDocument.Load` throws a `FileNotFoundException` that does not name the test's purpose. The path is also hard-coded to a `src/` layout, which is only a convention.
   Confidence: medium that a clearer failure would help. Low impact.

6. **File line 9, doc comment.**
   The comment says "Web, storage, messaging and PayEx code live in InvoiceApp.Api". PayEx is not in the code. `AGENTS.md` describes a payment provider only as part of a planned deployment, so this comment describes a system that does not exist yet.
   Confidence: medium that this is a stale or wrong reference.

7. **File line 11, class name `DomainDependencyTests`.**
   `AGENTS.md` asks for one test class per class under test, named `<ClassUnderTest>Tests`. This class tests a project's references, not a single class. The name does not follow the convention, and the test is not clearly tied to a class under test. This is a convention question, not a defect.
   Confidence: low.

8. **Possible compile dependency, file lines 1-2 and throughout.**
   The file uses `Select`, `Where`, `OrderBy`, `ToList`, `Contains`, `Any`, `File`, `Path`, `DirectoryInfo` and `StringComparison` without `using System;`, `using System.IO;` or `using System.Linq;`. This compiles only if the test project enables `ImplicitUsings`. The test csproj is not part of this diff, so I cannot confirm it.
   Confidence: low that it fails to compile. The check is only that the csproj enables implicit usings.

## Not a finding

- The `ForbiddenBaseLibraryPrefixes` matching uses `StartsWith` without a trailing dot, so a hypothetical assembly named `System.Netx` would match `System.Net`. No such assembly exists in the framework, so this is not a current problem.
- The `Assert.True(count == 0, ...)` form instead of `Assert.Empty` is a style choice. The message still lists the offending references.

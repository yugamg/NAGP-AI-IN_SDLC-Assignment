# Review: case-12.diff

Scope: one new file, `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`. Line numbers below are the diff line numbers shown in case-12.diff.

## Findings

### 1. Base-library check is looser than the rule it enforces (lines 64-71, 22-27)
- File: `backend/tests/InvoiceApp.Tests/Architecture/DomainDependencyTests.cs`
- What goes wrong: `IsBaseLibrary` accepts any assembly whose name starts with `System.` unless it starts with `System.Net`, `System.Data` or `System.Web`. The test name (`DomainAssembly_ReferencesOnlyTheBaseClassLibrary`) and the summary at lines 13-14 say Domain may depend only on the base class library, and that messaging and integration code must stay out. Assemblies such as `System.Messaging`, `System.ServiceModel`, `System.DirectoryServices`, `System.Management` and `System.Windows.*` all pass this check. A Domain project that references one of them would not fail the test, so the guard does not hold the rule stated in its own docs.
- Suggested direction: use an allowlist of BCL assembly names, or at least add the messaging and integration prefixes to `ForbiddenBaseLibraryPrefixes`.
- Confidence: high (about 85%) that the gap exists as written. The code path is clear from the diff. Whether it matters depends on how strictly the team wants the rule enforced.

### 2. Second test does not check plain `<Reference>` elements (lines 29-34, 56-59)
- File: same
- What goes wrong: `ReferenceElements` covers only `PackageReference`, `ProjectReference` and `FrameworkReference`. A legacy-style assembly reference such as `<Reference Include="System.Net.Http" HintPath="..." />` in `InvoiceApp.Domain.csproj` is not reported. The first test only sees assemblies that are compiled in and used, as the comment at line 49 notes, so an unused `<Reference>` to a forbidden assembly passes both tests.
- Confidence: medium-high (about 70%). It depends on whether the project can contain `<Reference>` items at all, which I did not check because the rules restrict me to this one file.

### 3. Project file lookup depends on the output location (lines 75-80)
- File: same
- What goes wrong: `FindDomainProjectFile` walks up from `AppContext.BaseDirectory` looking for `InvoiceApp.sln`, then hard-codes `src/InvoiceApp.Domain/InvoiceApp.Domain.csproj` relative to that folder. This works when the tests run from the repo's build output, which matches the commands in AGENTS.md. If the test binaries run from a copied location with no `.sln` above them, the test throws `FileNotFoundException` instead of checking anything. A failure there is noisy but does not give a false pass.
- Confidence: low-medium (about 40%) that this affects anyone in practice.

### 4. Implicit usings assumed for compilation (lines 6-8, 40-44, 54, 70-79)
- File: same
- What goes wrong: the file uses `File`, `Path`, `DirectoryInfo`, `FileNotFoundException`, `AppContext`, `StringComparison` and LINQ operators (`Select`, `Where`, `OrderBy`, `ToList`, `Any`, `Contains`) with no `using System;`, `using System.IO;` or `using System.Linq;`. It compiles only if the test project enables `ImplicitUsings`. I could not check the `.csproj` for the test project, so I have not confirmed whether this compiles.
- Confidence: unverified. It is a possible compile error, and I have not confirmed it.

## Checked and not flagged
- The `Assert.True` messages include the offending assembly or project entries, so failures are readable.
- Null handling for `AssemblyName.Name` at line 41 is present.
- The walk-up logic in `FindDomainProjectFile` terminates correctly when the `.sln` is not found (it throws).

## Summary
No correctness bug in the test logic itself, but the main architecture rule is enforced more loosely than its documentation says (finding 1), and the project-file check misses `<Reference>` items (finding 2). Findings 3 and 4 are low-confidence and unverified.

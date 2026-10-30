using System.Text.Json;
using System.Xml.Linq;
using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests.Architecture;

/// <summary>
/// Rule: InvoiceApp.Domain depends on nothing but the .NET base class library.
/// Web, storage, messaging and PayEx code belong in InvoiceApp.Api behind Domain interfaces.
/// </summary>
public class DomainDependencyTests
{
    private const string Guidance =
        "InvoiceApp.Domain must not depend on web, storage, networking or integration code. " +
        "Put that code behind an interface in Domain and implement it in InvoiceApp.Api.";

    private const string AllowedSdk = "Microsoft.NET.Sdk";
    private const string BaseFramework = "Microsoft.NETCore.App";

    private static readonly string[] ForbiddenBaseLibraryPrefixes =
    {
        "System.Net",
        "System.Data",
        "System.Web",
    };

    private static readonly string[] ReferenceElements =
    {
        "PackageReference",
        "ProjectReference",
        "FrameworkReference",
        "Reference",
        "GlobalPackageReference",
    };

    // MSBuild imports the nearest of each of these from the project folder upwards.
    private static readonly string[] InheritedBuildFiles =
    {
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
    };

    [Fact]
    [Trait("Category", "Architecture")]
    public void DomainAssembly_ReferencesOnlyTheBaseClassLibrary()
    {
        var forbidden = typeof(ApprovalService).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !IsBaseLibrary(name))
            .OrderBy(name => name)
            .ToList();

        Assert.True(forbidden.Count == 0, $"{Guidance} Forbidden assembly references: {string.Join(", ", forbidden)}");
    }

    // Compiled references miss constants (inlined by the compiler) and references that are declared but unused.
    [Fact]
    [Trait("Category", "Architecture")]
    public void DomainProject_DeclaresNoPackageProjectOrFrameworkReferences()
    {
        var projectFile = FindDomainProjectFile();
        var project = XDocument.Load(projectFile);
        var problems = new List<string>();

        var sdk = project.Root?.Attribute("Sdk")?.Value;
        if (sdk != AllowedSdk)
        {
            problems.Add($"Sdk=\"{sdk}\" (only {AllowedSdk} is allowed)");
        }

        problems.AddRange(project.Descendants()
            .Where(element => element.Name.LocalName is "Import" or "Sdk")
            .Select(element => $"{element.Name.LocalName} {element.Attribute("Project")?.Value ?? element.Attribute("Name")?.Value}"));

        var buildFiles = new[] { projectFile }.Concat(FindInheritedBuildFiles(Path.GetDirectoryName(projectFile)!));
        foreach (var buildFile in buildFiles)
        {
            problems.AddRange(XDocument.Load(buildFile).Descendants()
                .Where(element => ReferenceElements.Contains(element.Name.LocalName))
                .Select(element => $"{element.Name.LocalName} {element.Attribute("Include")?.Value} in {Path.GetFileName(buildFile)}"));
        }

        Assert.True(problems.Count == 0, $"{Guidance} Found in InvoiceApp.Domain's build files: {string.Join("; ", problems)}");
    }

    // Restore output records what MSBuild actually resolved, however it was declared
    // (SDK switch, imported props file, central package management).
    [Fact]
    [Trait("Category", "Architecture")]
    public void DomainRestore_ResolvedNoPackagesProjectsOrExtraFrameworks()
    {
        var assetsFile = Path.Combine(Path.GetDirectoryName(FindDomainProjectFile())!, "obj", "project.assets.json");
        Assert.True(File.Exists(assetsFile), $"Restore output not found at {assetsFile}. Build the solution before running this test.");

        using var assets = JsonDocument.Parse(File.ReadAllText(assetsFile));
        var root = assets.RootElement;
        var problems = new List<string>();

        problems.AddRange(root.GetProperty("libraries").EnumerateObject().Select(library => $"package {library.Name}"));

        foreach (var framework in root.GetProperty("project").GetProperty("frameworks").EnumerateObject())
        {
            if (framework.Value.TryGetProperty("frameworkReferences", out var frameworkReferences))
            {
                problems.AddRange(frameworkReferences.EnumerateObject()
                    .Where(reference => reference.Name != BaseFramework)
                    .Select(reference => $"framework {reference.Name}"));
            }
        }

        foreach (var framework in root.GetProperty("project").GetProperty("restore").GetProperty("frameworks").EnumerateObject())
        {
            if (framework.Value.TryGetProperty("projectReferences", out var projectReferences))
            {
                problems.AddRange(projectReferences.EnumerateObject().Select(reference => $"project {Path.GetFileName(reference.Name)}"));
            }
        }

        Assert.True(problems.Count == 0, $"{Guidance} Restore resolved: {string.Join(", ", problems)}");
    }

    private static bool IsBaseLibrary(string assemblyName)
    {
        var isSystem = assemblyName is "System" or "netstandard" or "mscorlib"
            || assemblyName.StartsWith("System.", StringComparison.Ordinal);

        return isSystem
            && !ForbiddenBaseLibraryPrefixes.Any(prefix => assemblyName.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static IEnumerable<string> FindInheritedBuildFiles(string projectDirectory)
    {
        foreach (var fileName in InheritedBuildFiles)
        {
            for (var directory = new DirectoryInfo(projectDirectory); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, fileName);
                if (File.Exists(candidate))
                {
                    yield return candidate;
                    break;
                }
            }
        }
    }

    private static string FindDomainProjectFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "InvoiceApp.sln")))
            {
                return Path.Combine(directory.FullName, "src", "InvoiceApp.Domain", "InvoiceApp.Domain.csproj");
            }
        }

        throw new FileNotFoundException("Could not find InvoiceApp.sln above " + AppContext.BaseDirectory);
    }
}

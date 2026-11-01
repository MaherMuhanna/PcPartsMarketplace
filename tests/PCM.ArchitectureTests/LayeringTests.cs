using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;
using PCM.Infrastructure.Services;
using PCM.Repositories;

namespace PCM.ArchitectureTests;

/// <summary>
/// The layer rules from the README, checked on every build:
/// API -> Infrastructure -> Repositories -> DomainEntities, and the UI talks to the API only.
/// </summary>
public sealed partial class LayeringTests
{
    /// <summary>The only project references each project may have.</summary>
    public static TheoryData<string, string[]> AllowedProjectReferences => new()
    {
        { "PCM.DomainEntities", [] },
        { "PCM.Repositories", ["PCM.DomainEntities"] },
        { "PCM.Infrastructure", ["PCM.Repositories"] },
        { "PCM.API", ["PCM.Infrastructure", "PCM.Repositories"] },   // Repositories only for AddDataAccess in Program
        { "PCM.UI", ["PCM.Infrastructure"] }                          // shared DTOs only, never the database
    };

    [Theory]
    [MemberData(nameof(AllowedProjectReferences))]
    public void Project_references_follow_the_layers(string project, string[] allowed)
    {
        var csproj = XDocument.Load(Path.Combine(SourceTree.Root, project, $"{project}.csproj"));
        var references = csproj.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")!.Value.Replace('\\', '/')))
            .Order()
            .ToArray();

        Assert.Equal(allowed.Order().ToArray(), references);
    }

    [Fact]
    public void Domain_entities_do_not_depend_on_web_or_upper_layers()
    {
        var referenced = typeof(PcPartsContext).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        Assert.DoesNotContain(referenced, name => name.StartsWith("Microsoft.AspNetCore.Mvc", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, name => name is "PCM.Repositories" or "PCM.Infrastructure" or "PCM.API");
    }

    [Fact]
    public void UI_code_never_uses_the_database_layers()
    {
        // Infrastructure brings EF Core transitively, so the rule is checked on the UI's own code.
        var offenders = SourceTree.CSharpFiles("PCM.UI")
            .Where(path => DatabaseNamespace().IsMatch(File.ReadAllText(path)))
            .Select(SourceTree.Relative)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Repositories_never_call_SaveChanges()
    {
        // Persisting is the Unit of Work's job, so one business operation commits once.
        var offenders = SourceTree.CSharpFiles("PCM.Repositories")
            .Where(path => Path.GetFileName(path) is not ("UnitOfWork.cs" or "IUnitOfWork.cs"))
            .Where(path => SaveChangesCall().IsMatch(File.ReadAllText(path)))
            .Select(SourceTree.Relative)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Services_reach_the_database_through_repositories_only()
    {
        // No Infrastructure type takes a DbContext: data access goes through IUnitOfWork and repositories.
        var offenders = typeof(IAuthService).Assembly.GetTypes()
            .Where(type => type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => typeof(DbContext).IsAssignableFrom(parameter.ParameterType)))
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Unit_of_work_is_the_only_public_entry_to_saving()
    {
        var saveMethods = typeof(IUnitOfWork).Assembly.GetExportedTypes()
            .Where(type => type.IsInterface)
            .SelectMany(type => type.GetMethods().Select(method => (type, method)))
            .Where(pair => pair.method.Name.StartsWith("SaveChanges", StringComparison.Ordinal))
            .Select(pair => pair.type.Name)
            .Distinct()
            .ToList();

        Assert.Equal(new[] { nameof(IUnitOfWork) }, saveMethods);
    }

    [GeneratedRegex(@"\b(PCM\.Repositories|PCM\.DomainEntities|Microsoft\.EntityFrameworkCore)\b")]
    private static partial Regex DatabaseNamespace();

    [GeneratedRegex(@"\.SaveChanges(Async)?\s*\(")]
    private static partial Regex SaveChangesCall();
}

using MS.Cobroup.Backend.Infrastructure;
using System.Xml.Linq;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Architecture;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void Infrastructure_DoesNotReferenceApi()
    {
        var references = typeof(AssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.DoesNotContain("MS.Cobroup.Backend.Api", references);
    }

    [Fact]
    public void ProjectReferences_StayWithinBackendRepository()
    {
        var repositoryRoot = FindRepositoryRoot();
        var repositoryPrefix = repositoryRoot.FullName.TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var projectFiles = repositoryRoot
            .EnumerateFiles("*.csproj", SearchOption.AllDirectories)
            .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        foreach (var projectFile in projectFiles)
        {
            var document = XDocument.Load(projectFile.FullName);
            var references = document
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(include => !string.IsNullOrWhiteSpace(include));

            foreach (var include in references)
            {
                var normalizedInclude = include!
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar);
                var referencedPath = Path.GetFullPath(normalizedInclude, projectFile.DirectoryName!);
                Assert.True(
                    referencedPath.StartsWith(repositoryPrefix, StringComparison.OrdinalIgnoreCase),
                    $"'{projectFile.Name}' contiene una referencia externa no permitida: '{include}'.");
            }
        }
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.EnumerateFiles("MS.Cobroup.Backend.slnx").Any())
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("No se encontró la raíz de MS.Cobroup.Backend.");
    }
}

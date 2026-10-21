using MS.Cobroup.Backend.Infrastructure;

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
}

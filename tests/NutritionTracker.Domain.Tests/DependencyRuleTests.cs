namespace NutritionTracker.Domain.Tests;

public sealed class DependencyRuleTests
{
    [Fact]
    public void DomainDoesNotReferenceOuterLayers()
    {
        var referencedAssemblies = typeof(AssemblyReference)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name);

        Assert.DoesNotContain("NutritionTracker.Application", referencedAssemblies);
        Assert.DoesNotContain("NutritionTracker.Infrastructure", referencedAssemblies);
        Assert.DoesNotContain("NutritionTracker.Api", referencedAssemblies);
    }
}

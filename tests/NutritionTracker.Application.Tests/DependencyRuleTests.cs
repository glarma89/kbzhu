namespace NutritionTracker.Application.Tests;

public sealed class DependencyRuleTests
{
    [Fact]
    public void ApplicationDoesNotReferenceInfrastructureOrApi()
    {
        var referencedAssemblies = typeof(AssemblyReference)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name);

        Assert.DoesNotContain("NutritionTracker.Infrastructure", referencedAssemblies);
        Assert.DoesNotContain("NutritionTracker.Api", referencedAssemblies);
    }
}

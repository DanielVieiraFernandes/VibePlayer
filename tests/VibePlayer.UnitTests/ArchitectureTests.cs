using NetArchTest.Rules;
using VibePlayer.Core;

namespace VibePlayer.UnitTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Core_NaoDependeDeInfraestruturaUiOuSerilog()
    {
        // Core é o centro da arquitetura: se passar a depender de UI, Serilog ou da
        // infraestrutura, deixa de ser testável isoladamente e as camadas se misturam.
        var result = Types.InAssembly(typeof(IAppPaths).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "VibePlayer.Infrastructure",
                "VibePlayer.App",
                "Avalonia",
                "Serilog",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Tipos do Core com dependência proibida: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}

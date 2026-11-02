using System.Reflection;
using System.Text.RegularExpressions;
using VibePlayer.Core;

namespace VibePlayer.UnitTests;

public sealed partial class AppVersionTests
{
    // SemVer 2.0 com pré-lançamento e metadados opcionais (ex.: 0.1.0, 0.1.1-alpha.0.3+abc123).
    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$")]
    private static partial Regex SemVer();

    [Fact]
    public void VersaoDoAssembly_VemDoMinVer_SegueSemVer()
    {
        var version = typeof(IAppPaths).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        version.ShouldNotBeNull();
        SemVer().IsMatch(version).ShouldBeTrue($"Versão fora do padrão SemVer: {version}");

        // 1.0.0 é o valor padrão do .NET quando nada define a versão: indicaria MinVer desligado.
        version.ShouldNotStartWith("1.0.0");
    }
}

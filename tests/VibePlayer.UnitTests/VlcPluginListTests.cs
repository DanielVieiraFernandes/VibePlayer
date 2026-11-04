using System.Text.RegularExpressions;

namespace VibePlayer.UnitTests;

public sealed partial class VlcPluginListTests
{
    [GeneratedRegex(@"<VlcAllowedPlugins>(?<list>.*?)</VlcAllowedPlugins>", RegexOptions.Singleline)]
    private static partial Regex AllowedPlugins();

    [Fact]
    public void ListaDaCi_EhIgualAoFiltroDoBuild()
    {
        // O build filtra pelo .targets e a CI confere pelo .txt: se divergirem, a conferência deixa
        // de provar que o que foi filtrado é o que foi aprovado.
        var root = FindRepositoryRoot();
        var targets = File.ReadAllText(Path.Combine(root, "src", "VibePlayer.App", "VlcPlugins.targets"));
        var fromBuild = AllowedPlugins().Match(targets).Groups["list"].Value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => name.Length > 0);
        var fromCi = File.ReadAllLines(Path.Combine(root, ".github", "scripts", "vlc-allowed-plugins.txt"))
            .Where(name => name.Length > 0);

        fromBuild.ShouldBe(fromCi, ignoreOrder: true);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VibePlayer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}

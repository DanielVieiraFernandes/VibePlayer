using VibePlayer.Infrastructure;

namespace VibePlayer.UnitTests.Infrastructure;

public sealed class AppPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "VibePlayerTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Construtor_PastasInexistentes_CriaLogsEModels()
    {
        Directory.Exists(_root).ShouldBeFalse();

        var paths = new AppPaths(_root);

        paths.DataDirectory.ShouldBe(_root);
        paths.LogsDirectory.ShouldBe(Path.Combine(_root, "logs"));
        paths.ModelsDirectory.ShouldBe(Path.Combine(_root, "models"));
        Directory.Exists(paths.LogsDirectory).ShouldBeTrue();
        Directory.Exists(paths.ModelsDirectory).ShouldBeTrue();
    }

    [Fact]
    public void Construtor_PastasJaExistem_PreservaConteudo()
    {
        var existingLog = Path.Combine(_root, "logs", "antigo.log");
        Directory.CreateDirectory(Path.GetDirectoryName(existingLog)!);
        File.WriteAllText(existingLog, "conteúdo");

        _ = new AppPaths(_root);

        File.ReadAllText(existingLog).ShouldBe("conteúdo");
    }

    [Fact]
    public void CreateDefault_UsaPastaApplicationDataDoUsuario()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VibePlayer");

        AppPaths.CreateDefault().DataDirectory.ShouldBe(expected);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using VibePlayer.App;
using VibePlayer.Core.Persistence;
using VibePlayer.Core.Settings;

namespace VibePlayer.E2ETests;

public sealed class BackupTests : IDisposable
{
    private static readonly SettingKey<string> Marker = new("teste.marcador", string.Empty);

    private readonly TemporaryAppData _data = new();

    public void Dispose() => _data.Dispose();

    [Fact]
    public async Task BackupComAppGravando_ContemDadosAteOMomento()
    {
        var token = TestContext.Current.CancellationToken;
        using var services = _data.BuildServices();
        AppStartup.Prepare(services);
        var settings = services.GetRequiredService<ISettingsStore>();

        // A gravação fica no -wal (a conexão do app segue aberta no pool): uma cópia de arquivo
        // do .db perderia este valor; o backup online não pode perder.
        await settings.SetAsync(Marker, "gravado antes do backup", token);

        // Outra "tela" continua gravando enquanto o backup roda.
        using var writing = new CancellationTokenSource();
        var writer = Task.Run(
            async () =>
            {
                for (var i = 0; !writing.IsCancellationRequested; i++)
                {
                    await settings.SetAsync(new SettingKey<int>("teste.contador", 0), i, token);
                }
            },
            token);

        var backupPath = services.GetRequiredService<IBackupService>().CreateBackup(BackupKind.Daily);
        await writing.CancelAsync();
        await writer;

        backupPath.ShouldNotBeNull();
        using var connection = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = 'teste.marcador'";
        command.ExecuteScalar().ShouldBe("\"gravado antes do backup\"");
    }
}

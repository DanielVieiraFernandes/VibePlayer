using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibePlayer.Core.Settings;
using VibePlayer.Infrastructure.Settings;

namespace VibePlayer.UnitTests.Infrastructure;

public sealed class SqliteSettingsStoreTests : IDisposable
{
    private static readonly SettingKey<double> Width = new("teste.largura", 380, w => w > 0);
    private static readonly SettingKey<Size> Composite = new("teste.tamanho", new Size(1, 1));
    private static readonly SettingKey<DayOfWeek> Day = new("teste.dia", DayOfWeek.Monday, Enum.IsDefined);

    private readonly InMemoryDatabase _database = new();
    private readonly ListLogger<SqliteSettingsStore> _logger = new();
    private readonly SqliteSettingsStore _store;

    public SqliteSettingsStoreTests()
    {
        _store = new SqliteSettingsStore(_database, new ManualTimeProvider(), _logger);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task ChaveAusente_DevolvePadrao()
    {
        (await _store.GetAsync(Width, Token)).ShouldBe(380);
        (await _store.GetAsync(Composite, Token)).ShouldBe(new Size(1, 1));
    }

    [Fact]
    public async Task ValorGravado_EhLidoDeVolta()
    {
        await _store.SetAsync(Width, 420, Token);
        await _store.SetAsync(Width, 450, Token);
        await _store.SetAsync(Composite, new Size(1440, 900), Token);
        await _store.SetAsync(Day, DayOfWeek.Friday, Token);

        (await _store.GetAsync(Width, Token)).ShouldBe(450);
        (await _store.GetAsync(Composite, Token)).ShouldBe(new Size(1440, 900));
        (await _store.GetAsync(Day, Token)).ShouldBe(DayOfWeek.Friday);
    }

    [Fact]
    public async Task Enum_EhGravadoComoTexto()
    {
        await _store.SetAsync(Day, DayOfWeek.Friday, Token);

        await using var db = _database.CreateDbContext();
        (await db.Settings.SingleAsync(e => e.Key == Day.Name, Token)).Value.ShouldBe("\"Friday\"");
    }

    [Theory]
    [InlineData("-10")]
    [InlineData("{quebrado")]
    [InlineData("\"texto\"")]
    [InlineData("null")]
    public async Task ValorInvalidoOuCorrompido_DevolvePadraoERegistraAviso(string storedJson)
    {
        await using (var db = _database.CreateDbContext())
        {
            db.Settings.Add(new() { Key = Width.Name, Value = storedJson });
            await db.SaveChangesAsync(Token);
        }

        (await _store.GetAsync(Width, Token)).ShouldBe(380);

        var warning = _logger.Entries.ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain(Width.Name);
        warning.Message.ShouldNotContain(storedJson, Case.Sensitive, "O log não leva o valor salvo");
    }

    public sealed record Size(double Width, double Height);
}

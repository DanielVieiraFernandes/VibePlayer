using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VibePlayer.Infrastructure.Persistence;

namespace VibePlayer.UnitTests.Infrastructure;

/// <summary>
/// Banco SQLite em memória com as migrations aplicadas. O banco existe enquanto a conexão está
/// aberta, por isso ela vive o teste inteiro e todos os contextos a compartilham.
/// </summary>
public sealed class InMemoryDatabase : IDbContextFactory<VibePlayerDbContext>, IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<VibePlayerDbContext> _options;

    public InMemoryDatabase()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<VibePlayerDbContext>().UseSqlite(_connection).Options;

        using var db = CreateDbContext();
        db.Database.Migrate();
    }

    public VibePlayerDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}

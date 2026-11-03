using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VibePlayer.Infrastructure.Persistence;

/// <summary>
/// Usada só pelo <c>dotnet ef</c> para montar o contexto sem rodar o app. O banco apontado é
/// descartável: gerar migrations só precisa do modelo, nunca toca no banco real do usuário.
/// </summary>
internal sealed class VibePlayerDesignTimeFactory : IDesignTimeDbContextFactory<VibePlayerDbContext>
{
    public VibePlayerDbContext CreateDbContext(string[] args)
    {
        var path = Path.Combine(Path.GetTempPath(), "VibePlayerDesignTime", "vibePlayer.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var options = new DbContextOptionsBuilder<VibePlayerDbContext>()
            .UseSqlite(VibePlayerDbContext.CreateConnectionString(path))
            .Options;

        return new VibePlayerDbContext(options);
    }
}

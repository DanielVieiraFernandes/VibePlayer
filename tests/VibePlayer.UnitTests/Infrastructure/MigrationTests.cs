using Microsoft.EntityFrameworkCore;

namespace VibePlayer.UnitTests.Infrastructure;

public sealed class MigrationTests
{
    [Fact]
    public void Modelo_NaoTemMudancasSemMigration()
    {
        // Mudar uma entidade e esquecer a migration só quebraria na máquina do usuário, ao abrir
        // a versão nova; aqui quebra no PR.
        using var database = new InMemoryDatabase();
        using var db = database.CreateDbContext();

        db.Database.HasPendingModelChanges().ShouldBeFalse(
            "O modelo mudou sem migration. Rode: dotnet ef migrations add <Nome> --project src/VibePlayer.Infrastructure --startup-project src/VibePlayer.Infrastructure --output-dir Persistence/Migrations");
    }
}

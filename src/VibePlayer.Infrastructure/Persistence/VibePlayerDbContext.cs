using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace VibePlayer.Infrastructure.Persistence;

/// <summary>
/// Banco local do app. Cada operação abre um contexto curto via
/// <see cref="IDbContextFactory{TContext}"/>: o processo vive horas e é usado por várias threads,
/// e um contexto longo acumularia entidades rastreadas e não é thread-safe.
/// </summary>
/// <remarks>
/// Mudou o modelo? Gere a migration (ver CLAUDE.md, "Comandos"); <c>MigrationTests</c> falha se
/// esquecer. Migrations já publicadas nunca são editadas: o banco dos usuários já as aplicou.
/// </remarks>
public sealed class VibePlayerDbContext(DbContextOptions<VibePlayerDbContext> options) : DbContext(options)
{
    public DbSet<SettingEntry> Settings => Set<SettingEntry>();

    public DbSet<LibraryRootEntity> LibraryRoots => Set<LibraryRootEntity>();

    public DbSet<RemovedCourseEntity> RemovedCourses => Set<RemovedCourseEntity>();

    public DbSet<CourseEntity> Courses => Set<CourseEntity>();

    public DbSet<ModuleEntity> Modules => Set<ModuleEntity>();

    public DbSet<LessonEntity> Lessons => Set<LessonEntity>();

    public DbSet<LessonProgressEntity> LessonProgress => Set<LessonProgressEntity>();

    public DbSet<TranscriptEntity> Transcripts => Set<TranscriptEntity>();

    public DbSet<TranscriptSegmentEntity> TranscriptSegments => Set<TranscriptSegmentEntity>();

    public DbSet<LessonTranscriptionEntity> LessonTranscriptions => Set<LessonTranscriptionEntity>();

    /// <summary>
    /// Connection string do banco do app.
    /// </summary>
    /// <remarks>
    /// <c>Foreign Keys</c> já fica ligado para valer nas tabelas das próximas specs (o SQLite vem
    /// com chaves estrangeiras desligadas). <c>Default Timeout</c> também é o tempo que o
    /// Microsoft.Data.Sqlite espera quando o banco está ocupado por outra conexão (ex.: segunda
    /// instância do app) antes de desistir com <c>database is locked</c>.
    /// </remarks>
    public static string CreateConnectionString(string databasePath) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            DefaultTimeout = 5,
        }.ToString();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VibePlayerDbContext).Assembly);
}

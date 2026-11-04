using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VibePlayer.Infrastructure.Persistence.Configurations;

internal sealed class LibraryRootConfiguration : IEntityTypeConfiguration<LibraryRootEntity>
{
    public void Configure(EntityTypeBuilder<LibraryRootEntity> builder)
    {
        builder.ToTable("LibraryRoots");

        // Ids gerados no código (Guid v7), nunca pelo banco. Sem isso, o EF trata uma entidade nova
        // com Id já preenchido, encontrada por navegação, como existente e tenta um UPDATE.
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.HasIndex(r => r.Path).IsUnique();

        // Enums como texto: o banco fica legível e reordenar o enum não muda o significado.
        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(20);
        builder.HasMany(r => r.Courses).WithOne(c => c.Root).HasForeignKey(c => c.RootId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.RemovedCourses).WithOne().HasForeignKey(r => r.RootId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RemovedCourseConfiguration : IEntityTypeConfiguration<RemovedCourseEntity>
{
    public void Configure(EntityTypeBuilder<RemovedCourseEntity> builder)
    {
        builder.ToTable("RemovedCourses");
        builder.HasKey(r => new { r.RootId, r.RelativePath });
    }
}

internal sealed class CourseConfiguration : IEntityTypeConfiguration<CourseEntity>
{
    public void Configure(EntityTypeBuilder<CourseEntity> builder)
    {
        builder.ToTable("Courses");
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasIndex(c => new { c.RootId, c.RelativePath }).IsUnique();
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
        builder.HasMany(c => c.Modules).WithOne().HasForeignKey(m => m.CourseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Lessons).WithOne().HasForeignKey(l => l.CourseId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ModuleConfiguration : IEntityTypeConfiguration<ModuleEntity>
{
    public void Configure(EntityTypeBuilder<ModuleEntity> builder)
    {
        builder.ToTable("Modules");
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.HasIndex(m => new { m.CourseId, m.RelativePath }).IsUnique();
    }
}

internal sealed class LessonConfiguration : IEntityTypeConfiguration<LessonEntity>
{
    public void Configure(EntityTypeBuilder<LessonEntity> builder)
    {
        builder.ToTable("Lessons");
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.HasIndex(l => new { l.CourseId, l.Path }).IsUnique();

        // Módulo some (pasta renomeada): a aula fica sem módulo em vez de sumir junto.
        builder.HasOne<ModuleEntity>().WithMany().HasForeignKey(l => l.ModuleId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class LessonProgressConfiguration : IEntityTypeConfiguration<LessonProgressEntity>
{
    public void Configure(EntityTypeBuilder<LessonProgressEntity> builder)
    {
        builder.ToTable("LessonProgress");
        builder.HasKey(p => p.LessonId);

        // Aula removida da biblioteca leva o progresso junto.
        builder.HasOne<LessonEntity>().WithOne().HasForeignKey<LessonProgressEntity>(p => p.LessonId).OnDelete(DeleteBehavior.Cascade);
    }
}

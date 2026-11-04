using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VibePlayer.Infrastructure.Persistence.Configurations;

internal sealed class TranscriptConfiguration : IEntityTypeConfiguration<TranscriptEntity>
{
    public void Configure(EntityTypeBuilder<TranscriptEntity> builder)
    {
        builder.ToTable("Transcripts");
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.HasIndex(t => new { t.SourceHash, t.Source }).IsUnique();
    }
}

internal sealed class TranscriptSegmentConfiguration : IEntityTypeConfiguration<TranscriptSegmentEntity>
{
    public void Configure(EntityTypeBuilder<TranscriptSegmentEntity> builder)
    {
        builder.ToTable("TranscriptSegments");
        builder.HasKey(s => new { s.TranscriptId, s.Position });
        builder.HasOne<TranscriptEntity>().WithMany(t => t.Segments).HasForeignKey(s => s.TranscriptId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LessonTranscriptionConfiguration : IEntityTypeConfiguration<LessonTranscriptionEntity>
{
    public void Configure(EntityTypeBuilder<LessonTranscriptionEntity> builder)
    {
        builder.ToTable("LessonTranscriptions");
        builder.HasKey(l => l.LessonId);
        builder.HasIndex(l => l.Status);

        // Aula removida leva o vínculo, mas não a transcrição: se o curso voltar, o cache funciona.
        builder.HasOne<LessonEntity>().WithOne().HasForeignKey<LessonTranscriptionEntity>(l => l.LessonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TranscriptEntity>().WithMany().HasForeignKey(l => l.TranscriptId).OnDelete(DeleteBehavior.SetNull);
    }
}

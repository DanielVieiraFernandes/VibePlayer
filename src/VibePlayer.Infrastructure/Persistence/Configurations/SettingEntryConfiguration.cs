using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VibePlayer.Infrastructure.Persistence.Configurations;

internal sealed class SettingEntryConfiguration : IEntityTypeConfiguration<SettingEntry>
{
    public void Configure(EntityTypeBuilder<SettingEntry> builder)
    {
        builder.ToTable("Settings");
        builder.HasKey(e => e.Key);
        builder.Property(e => e.Key).HasMaxLength(200);
        builder.Property(e => e.Value).IsRequired();
    }
}

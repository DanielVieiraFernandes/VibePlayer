namespace VibePlayer.Infrastructure.Persistence;

/// <summary>Uma linha da tabela de preferências; o valor é JSON (ver <c>SqliteSettingsStore</c>).</summary>
public sealed class SettingEntry
{
    public required string Key { get; set; }

    public required string Value { get; set; }

    /// <summary>Só para diagnóstico (quando a preferência mudou pela última vez).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

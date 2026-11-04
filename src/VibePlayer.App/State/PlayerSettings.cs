using VibePlayer.Core.Settings;

namespace VibePlayer.App.State;

/// <summary>Preferências do player lembradas entre aberturas (spec 0006).</summary>
public static class PlayerSettings
{
    public static readonly SettingKey<int> Volume = new("player.volume", 100, v => v is >= 0 and <= 100);

    public static readonly SettingKey<bool> Muted = new("player.muted", false);

    public static readonly SettingKey<double> Rate = new("player.rate", 1.0, r => PlayerRates.All.Contains(r));

    /// <summary>Sem tela nesta spec; existe para a futura tela de configurações.</summary>
    public static readonly SettingKey<bool> AutoAdvance = new("player.autoAdvance", true);

    public static readonly SettingKey<Guid?> LastLessonId = new("player.lastLessonId", null);
}

public static class PlayerRates
{
    public static readonly double[] All = [0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0];
}

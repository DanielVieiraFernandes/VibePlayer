namespace VibePlayer.Core.Settings;

/// <summary>Preferências do usuário que sobrevivem entre aberturas do app.</summary>
/// <remarks>
/// Nunca guarde segredos aqui (chaves de API, tokens): eles vão para o cofre do sistema.
/// </remarks>
public interface ISettingsStore
{
    /// <summary>
    /// Valor salvo, ou <see cref="SettingKey{T}.Default"/> se nunca foi gravado ou se o valor
    /// salvo não é mais válido. Nunca lança por causa do conteúdo salvo.
    /// </summary>
    Task<T> GetAsync<T>(SettingKey<T> key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(SettingKey<T> key, T value, CancellationToken cancellationToken = default);
}

namespace VibePlayer.Core.Settings;

/// <summary>
/// Uma preferência do usuário: nome permanente, tipo, valor padrão e, opcionalmente, o que conta
/// como valor válido.
/// </summary>
/// <param name="Name">
/// Gravado no banco e <b>permanente</b>: renomear faz o app "esquecer" a preferência de todos os
/// usuários. Use prefixo por área (<c>ui.</c>, <c>player.</c>, <c>llm.</c>).
/// </param>
/// <param name="Default">Usado quando a preferência nunca foi gravada ou o valor salvo é inválido.</param>
/// <param name="IsValid">
/// Rejeita só o absurdo (ex.: largura negativa); limites de tela ficam com quem usa o valor.
/// </param>
public sealed record SettingKey<T>(string Name, T Default, Func<T, bool>? IsValid = null);

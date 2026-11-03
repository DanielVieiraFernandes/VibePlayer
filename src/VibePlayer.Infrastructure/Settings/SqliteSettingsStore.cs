using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibePlayer.Core.Settings;
using VibePlayer.Infrastructure.Persistence;

namespace VibePlayer.Infrastructure.Settings;

/// <summary>
/// Preferências na tabela <c>Settings</c>: uma linha por chave, valor em JSON.
/// </summary>
/// <remarks>
/// JSON numa tabela chave/valor, e não uma coluna por preferência, para que preferência nova de
/// qualquer tipo não precise de migration. Quem valida o tipo é o <see cref="SettingKey{T}"/>.
/// O log leva só o nome da chave, nunca o valor.
/// </remarks>
public sealed class SqliteSettingsStore(
    IDbContextFactory<VibePlayerDbContext> contextFactory,
    TimeProvider timeProvider,
    ILogger<SqliteSettingsStore> logger) : ISettingsStore
{
    // Enums como texto ("Chat", não 2): o banco fica legível e reordenar o enum não muda o valor.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<T> GetAsync<T>(SettingKey<T> key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entry = await db.Settings.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key.Name, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            return key.Default;
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(entry.Value, JsonOptions);
            if (value is not null && (key.IsValid is null || key.IsValid(value)))
            {
                return value;
            }
        }
        catch (JsonException)
        {
            // Mesmo tratamento do valor inválido, logo abaixo.
        }

        logger.LogWarning("Preferência {Key} com valor inválido; usando o padrão", key.Name);
        return key.Default;
    }

    public async Task SetAsync<T>(SettingKey<T> key, T value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var json = JsonSerializer.Serialize(value, JsonOptions);
        var now = timeProvider.GetUtcNow();

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Upsert num comando só: ler e depois inserir abriria espaço para duas gravações da mesma
        // chave ao mesmo tempo colidirem na chave primária. A interpolação vira parâmetros SQL.
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO Settings (Key, Value, UpdatedAt) VALUES ({key.Name}, {json}, {now})
            ON CONFLICT (Key) DO UPDATE SET Value = excluded.Value, UpdatedAt = excluded.UpdatedAt
            """,
            cancellationToken).ConfigureAwait(false);
    }
}

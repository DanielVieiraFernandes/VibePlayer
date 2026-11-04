using System.Globalization;

namespace VibePlayer.Core.Library;

/// <summary>
/// Ordem "natural" de nomes de aulas e pastas: "2 - Introdução" antes de "10 - Conclusão".
/// </summary>
/// <remarks>
/// Compara por blocos: sequências de dígitos valem como número (zeros à esquerda ignorados,
/// "1.1" &lt; "2" porque o primeiro bloco já decide), o resto como texto sem diferenciar maiúsculas
/// nem acentos. A cultura é a invariante para a ordem não mudar com o idioma do sistema. Empate
/// em tudo → ordinal, para a ordem ser total e estável (dois nomes diferentes nunca empatam).
/// </remarks>
public sealed class NaturalStringComparer : IComparer<string?>
{
    public static readonly NaturalStringComparer Instance = new();

    private static readonly CompareInfo Text = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions TextOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var result = CompareBlocks(x.AsSpan(), y.AsSpan());
        return result != 0 ? result : string.CompareOrdinal(x, y);
    }

    private static int CompareBlocks(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
    {
        while (!x.IsEmpty && !y.IsEmpty)
        {
            var xBlock = NextBlock(x, out var xIsNumber);
            var yBlock = NextBlock(y, out var yIsNumber);

            var result = xIsNumber && yIsNumber
                ? CompareNumbers(xBlock, yBlock)
                : Text.Compare(xBlock, yBlock, TextOptions);

            if (result != 0)
            {
                return result;
            }

            x = x[xBlock.Length..];
            y = y[yBlock.Length..];
        }

        return x.Length.CompareTo(y.Length);
    }

    private static ReadOnlySpan<char> NextBlock(ReadOnlySpan<char> value, out bool isNumber)
    {
        isNumber = char.IsAsciiDigit(value[0]);
        var length = 1;
        while (length < value.Length && char.IsAsciiDigit(value[length]) == isNumber)
        {
            length++;
        }

        return value[..length];
    }

    // Sem converter para número: blocos de dígitos podem passar do tamanho de um long.
    private static int CompareNumbers(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
    {
        x = x.TrimStart('0');
        y = y.TrimStart('0');
        return x.Length != y.Length ? x.Length.CompareTo(y.Length) : x.SequenceCompareTo(y);
    }
}

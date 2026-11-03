using System.Text;

namespace VibePlayer.Core.Library;

/// <summary>
/// Regras de caminho da biblioteca, iguais em todo lugar que compara ou grava caminhos.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Caixa: Windows e macOS usam sistemas de arquivos que não diferenciam maiúsculas; o Linux
/// diferencia. Comparar errado faria renomear só a caixa no Windows virar uma aula nova.</item>
/// <item>Unicode em NFC: o macOS devolve nomes em NFD, e "Introdução" viraria outra aula ao copiar
/// a pasta entre sistemas.</item>
/// <item>Caminhos relativos com "/" em qualquer SO, para "Localizar pasta" funcionar quando a
/// pasta veio de outro sistema.</item>
/// </list>
/// </remarks>
public static class LibraryPaths
{
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    public static StringComparison Comparison { get; } =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>Caminho absoluto, em NFC e sem separador no fim.</summary>
    public static string NormalizeFull(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).Normalize(NormalizationForm.FormC);

    /// <summary>Caminho de <paramref name="path"/> relativo a <paramref name="baseDirectory"/>, com "/".</summary>
    public static string Relative(string baseDirectory, string path)
    {
        var relative = Path.GetRelativePath(baseDirectory, path);
        return (relative == "." ? string.Empty : relative.Replace('\\', '/')).Normalize(NormalizationForm.FormC);
    }

    /// <summary>Junta uma pasta com um caminho relativo gravado (sempre com "/").</summary>
    public static string Combine(string baseDirectory, string relativePath) =>
        relativePath.Length == 0
            ? baseDirectory
            : Path.Combine([baseDirectory, .. relativePath.Split('/')]);

    /// <summary>Se <paramref name="path"/> é <paramref name="directory"/> ou está dentro dela.</summary>
    public static bool IsSameOrInside(string path, string directory)
    {
        var normalizedPath = NormalizeFull(path);
        var normalizedDirectory = NormalizeFull(directory);
        return normalizedPath.Equals(normalizedDirectory, Comparison)
            || normalizedPath.StartsWith(normalizedDirectory + Path.DirectorySeparatorChar, Comparison);
    }
}

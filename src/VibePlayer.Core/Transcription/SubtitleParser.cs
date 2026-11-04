using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace VibePlayer.Core.Transcription;

/// <summary>
/// Lê SRT e WebVTT num só caminho: os dois são blocos separados por linha em branco, com uma linha
/// de tempo com "-->". O que muda (cabeçalho, vírgula ou ponto, horas opcionais, configurações
/// depois do tempo) cabe nas mesmas regras, e arquivos reais misturam os dois formatos.
/// </summary>
public static partial class SubtitleParser
{
    /// <summary>Blocos sem tempo ou inválidos são pulados: uma legenda quase boa ainda serve.</summary>
    public static IReadOnlyList<TranscriptSegment> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var segments = new List<TranscriptSegment>();
        foreach (var block in BlockSeparator().Split(content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')))
        {
            if (ParseBlock(block) is { } segment)
            {
                segments.Add(segment);
            }
        }

        // OrderBy é estável: blocos no mesmo tempo mantêm a ordem do arquivo.
        return [.. segments.OrderBy(segment => segment.Start)];
    }

    private static TranscriptSegment? ParseBlock(string block)
    {
        var lines = block.Split('\n', StringSplitOptions.TrimEntries);
        var timeLine = Array.FindIndex(lines, line => line.Contains("-->", StringComparison.Ordinal));
        if (timeLine < 0 || lines[0].StartsWith("NOTE", StringComparison.Ordinal) || lines[0].StartsWith("STYLE", StringComparison.Ordinal))
        {
            return null;
        }

        var match = TimeLine().Match(lines[timeLine]);
        if (!match.Success)
        {
            return null;
        }

        var start = ToTime(match, "h1", "m1", "s1", "f1");
        var end = ToTime(match, "h2", "m2", "s2", "f2");
        var text = CleanText(string.Join(' ', lines.Skip(timeLine + 1).Where(line => line.Length > 0)));
        return end < start || text.Length == 0 ? null : new TranscriptSegment(start, end, text);
    }

    private static TimeSpan ToTime(Match match, string hours, string minutes, string seconds, string fraction)
    {
        var hour = match.Groups[hours].Success ? int.Parse(match.Groups[hours].Value, CultureInfo.InvariantCulture) : 0;
        var milliseconds = int.Parse(match.Groups[fraction].Value.PadRight(3, '0'), CultureInfo.InvariantCulture);
        return new TimeSpan(
            0,
            hour,
            int.Parse(match.Groups[minutes].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[seconds].Value, CultureInfo.InvariantCulture),
            milliseconds);
    }

    private static string CleanText(string text)
    {
        var withoutTags = Tags().Replace(text, string.Empty);
        return Spaces().Replace(WebUtility.HtmlDecode(withoutTags), " ").Trim();
    }

    [GeneratedRegex(@"\n[ \t]*\n+")]
    private static partial Regex BlockSeparator();

    [GeneratedRegex(@"(?:(?<h1>\d+):)?(?<m1>\d{1,2}):(?<s1>\d{2})[.,](?<f1>\d{1,3})\s*-->\s*(?:(?<h2>\d+):)?(?<m2>\d{1,2}):(?<s2>\d{2})[.,](?<f2>\d{1,3})")]
    private static partial Regex TimeLine();

    // <i>, <c.amarelo>, <00:00:01.000> (VTT) e {\an8} (SRT de ferramentas de legenda).
    [GeneratedRegex(@"<[^>]*>|\{\\[^}]*\}")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

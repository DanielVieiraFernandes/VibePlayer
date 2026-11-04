using System.Text.RegularExpressions;

namespace VibePlayer.Core.Transcription;

/// <summary>Limpa o que o Whisper devolve.</summary>
public static partial class TranscriptSegmentFilter
{
    /// <summary>
    /// Em silêncio ou música, o Whisper inventa marcações como <c>[MÚSICA]</c> e <c>(aplausos)</c>,
    /// e a spec 0007 mediu isso em 60 s de silêncio. Só vale para o Whisper: numa legenda escrita
    /// por alguém, a marcação é intencional.
    /// </summary>
    public static IReadOnlyList<TranscriptSegment> Apply(IEnumerable<TranscriptSegment> segments) =>
        [.. segments
            .Select(segment => segment with { Text = segment.Text.Trim() })
            .Where(segment => segment.Text.Length > 0 && !OnlyMarkup().IsMatch(segment.Text))];

    [GeneratedRegex(@"^(?:[\[\(][^\]\)]*[\]\)]|[♪♫\s*-])+$")]
    private static partial Regex OnlyMarkup();
}

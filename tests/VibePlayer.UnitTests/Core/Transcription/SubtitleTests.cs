using System.Text;
using VibePlayer.Core.Transcription;

namespace VibePlayer.UnitTests.Core.Transcription;

public sealed class SubtitleParserTests
{
    [Fact]
    public void Parse_Srt_LeTemposETexto()
    {
        var segments = SubtitleParser.Parse("1\r\n00:00:01,500 --> 00:00:03,000\r\nOlá\r\nmundo\r\n\r\n2\r\n01:02:03,004 --> 01:02:04,000\r\nFim\r\n");

        segments.Count.ShouldBe(2);
        segments[0].ShouldBe(new TranscriptSegment(TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(3), "Olá mundo"));
        segments[1].Start.ShouldBe(new TimeSpan(0, 1, 2, 3, 4));
    }

    [Fact]
    public void Parse_Vtt_SemHorasEComConfiguracoes()
    {
        var segments = SubtitleParser.Parse("WEBVTT\n\n01:02.500 --> 01:03.000 align:start position:0%\nTexto\n");

        segments.ShouldHaveSingleItem().Start.ShouldBe(TimeSpan.FromSeconds(62.5));
    }

    [Fact]
    public void Parse_TagsEConfiguracoes_SobraOTexto()
    {
        var content = """
            WEBVTT

            NOTE comentário 00:00:01.000 --> 00:00:02.000

            STYLE
            ::cue { color: red }

            00:00:01.000 --> 00:00:02.000
            <c.amarelo><i>Olá</i></c> {\an8}<00:00:01.500>mundo &amp; todos
            """;

        SubtitleParser.Parse(content).ShouldHaveSingleItem().Text.ShouldBe("Olá mundo & todos");
    }

    [Fact]
    public void Parse_BlocosInvalidos_AproveitaOsValidos()
    {
        var content = "lixo\n\n1\n00:00:05,000 --> quebrado\nRuim\n\n2\n00:00:02,000 --> 00:00:01,000\nInvertido\n\n3\n00:00:03,000 --> 00:00:04,000\nBom\n\n4\n00:00:06,000 --> 00:00:07,000\n";

        SubtitleParser.Parse(content).ShouldHaveSingleItem().Text.ShouldBe("Bom");
    }

    [Fact]
    public void Parse_ForaDeOrdem_OrdenaPorTempo()
    {
        var segments = SubtitleParser.Parse("00:00:05.000 --> 00:00:06.000\nB\n\n00:00:01.000 --> 00:00:02.000\nA\n");

        segments.Select(segment => segment.Text).ShouldBe(["A", "B"]);
    }

    [Fact]
    public void Parse_SemBlocos_DevolveVazio() => SubtitleParser.Parse("não é legenda").ShouldBeEmpty();
}

public sealed class SubtitleEncodingTests
{
    private const string Text = "Atenção à lição";

    [Fact]
    public void Decode_Utf8() => SubtitleEncoding.Decode(Encoding.UTF8.GetBytes(Text)).ShouldBe(Text);

    [Fact]
    public void Decode_Utf8ComBom() => SubtitleEncoding.Decode([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Text)]).ShouldBe(Text);

    [Fact]
    public void Decode_Utf16() => SubtitleEncoding.Decode([0xFF, 0xFE, .. Encoding.Unicode.GetBytes(Text)]).ShouldBe(Text);

    [Fact]
    public void Decode_Utf16BigEndian() => SubtitleEncoding.Decode([0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(Text)]).ShouldBe(Text);

    [Fact]
    public void Decode_Windows1252() => SubtitleEncoding.Decode(Encoding.Latin1.GetBytes(Text)).ShouldBe(Text);
}

public sealed class TranscriptSegmentFilterTests
{
    private static TranscriptSegment Segment(string text) => new(TimeSpan.Zero, TimeSpan.FromSeconds(1), text);

    [Theory]
    [InlineData("[MÚSICA]")]
    [InlineData(" (aplausos) ")]
    [InlineData("[BLANK_AUDIO]")]
    [InlineData("♪ ♪")]
    [InlineData("[Música] [Música]")]
    [InlineData("   ")]
    public void Apply_SoMarcacao_Remove(string text) => TranscriptSegmentFilter.Apply([Segment(text)]).ShouldBeEmpty();

    [Theory]
    [InlineData("A função main (a principal) começa aqui")]
    [InlineData("Use array[0] para o primeiro")]
    public void Apply_FalaComParenteses_Mantem(string text) =>
        TranscriptSegmentFilter.Apply([Segment(text)]).ShouldHaveSingleItem().Text.ShouldBe(text);

    [Fact]
    public void Apply_TiraEspacosDasPontas() =>
        TranscriptSegmentFilter.Apply([Segment("  Olá ")]).ShouldHaveSingleItem().Text.ShouldBe("Olá");
}

public sealed class WorkerProtocolTests
{
    [Fact]
    public void Evento_IdaEVolta()
    {
        var original = new WorkerEvent(WorkerEventKind.Segment, StartMs: 10, EndMs: 20, Text: "olá");

        WorkerProtocol.TryParseEvent(WorkerProtocol.Serialize(original)).ShouldBe(original);
    }

    [Fact]
    public void Job_IdaEVolta()
    {
        var original = new TranscriptionJob("/a b/aula.mp4", "/m.bin", "auto", 4, true);

        WorkerProtocol.ParseJob(WorkerProtocol.Serialize(original)).ShouldBe(original);
    }

    [Fact]
    public void LinhaEstranha_NaoEEvento() => WorkerProtocol.TryParseEvent("whisper_init: carregando").ShouldBeNull();
}

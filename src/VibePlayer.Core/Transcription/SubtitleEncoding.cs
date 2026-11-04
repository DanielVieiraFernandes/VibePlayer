using System.Text;

namespace VibePlayer.Core.Transcription;

/// <summary>Decide a codificação de uma legenda, que não diz qual é.</summary>
public static class SubtitleEncoding
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <remarks>
    /// Legendas brasileiras antigas costumam estar em Windows-1252. UTF-8 estrito vem antes porque
    /// recusa o que não é UTF-8, enquanto o Windows-1252 aceita qualquer sequência de bytes.
    /// </remarks>
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith<byte>([0xEF, 0xBB, 0xBF]))
        {
            return Encoding.UTF8.GetString(bytes[3..]);
        }

        if (bytes.StartsWith<byte>([0xFF, 0xFE]))
        {
            return Encoding.Unicode.GetString(bytes[2..]);
        }

        if (bytes.StartsWith<byte>([0xFE, 0xFF]))
        {
            return Encoding.BigEndianUnicode.GetString(bytes[2..]);
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }
}

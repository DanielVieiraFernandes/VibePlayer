namespace VibePlayer.Infrastructure.Transcription.Worker;

/// <summary>
/// Junta os blocos de áudio decodificados numa linha do tempo contínua, em 16 kHz mono, no formato
/// que o Whisper espera (float de -1 a 1).
/// </summary>
/// <remarks>
/// Os blocos não podem só ser concatenados: um arquivo pode ter trechos sem pacotes de áudio, e o
/// texto depois de um buraco de 30 s ficaria 30 s adiantado. A posição de cada bloco vem do PTS, e
/// o buraco vira silêncio. O PTS do VLC tem base no relógio do sistema, por isso a conta usa a
/// diferença para o primeiro bloco; um atraso no começo do áudio não é recuperável.
///
/// O mesmo mecanismo limita a deriva quando o VLC não tem o speex_resampler e usa o "ugly", que
/// perde amostras a cada bloco (ver a spec 0007).
/// </remarks>
public sealed class PtsGapFiller(int expectedSamples = 0)
{
    public const int SampleRate = 16_000;

    /// <summary>100 ms: variação normal entre blocos fica abaixo disso e não vira silêncio.</summary>
    public const int GapThresholdSamples = SampleRate / 10;

    private float[] _samples = new float[Math.Max(expectedSamples, SampleRate)];
    private long? _firstPts;

    public int Count { get; private set; }

    /// <summary>Total de silêncio inserido, em amostras.</summary>
    public long SilenceSamples { get; private set; }

    public ReadOnlyMemory<float> Samples => _samples.AsMemory(0, Count);

    public void Append(ReadOnlySpan<short> pcm, long ptsMicroseconds)
    {
        _firstPts ??= ptsMicroseconds;
        var position = (ptsMicroseconds - _firstPts.Value) * SampleRate / 1_000_000;
        var gap = position - Count;
        if (gap > GapThresholdSamples)
        {
            EnsureCapacity(Count + (int)gap);
            Count += (int)gap;
            SilenceSamples += gap;
        }

        EnsureCapacity(Count + pcm.Length);
        var target = _samples.AsSpan(Count, pcm.Length);
        for (var i = 0; i < pcm.Length; i++)
        {
            target[i] = pcm[i] / 32768f;
        }

        Count += pcm.Length;
    }

    private void EnsureCapacity(int required)
    {
        if (required > _samples.Length)
        {
            Array.Resize(ref _samples, Math.Max(required, _samples.Length + (_samples.Length / 2)));
        }
    }
}

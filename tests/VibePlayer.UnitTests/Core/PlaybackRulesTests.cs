using VibePlayer.Core.Playback;

namespace VibePlayer.UnitTests.Core;

public sealed class PlaybackRulesTests
{
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

    private static LessonProgress Saved(double seconds, bool completed = false) =>
        new(Guid.NewGuid(), TimeSpan.FromSeconds(seconds), completed, DateTimeOffset.UnixEpoch);

    [Theory]
    [InlineData(5, 0)]      // menos de 10 s: só abriu e fechou
    [InlineData(10, 10)]
    [InlineData(300, 300)]
    [InlineData(569, 569)]  // abaixo de 95%
    [InlineData(570, 0)]    // 95% de 600 s: já é fim
    public void Retomar_EntreDezSegundosENoventaECincoPorCento(double saved, double expected)
    {
        PlaybackRules.ResumePosition(Saved(saved), TenMinutes).TotalSeconds.ShouldBe(expected);
    }

    [Fact]
    public void Retomar_AulaConcluida_RecomecaDoInicio() =>
        PlaybackRules.ResumePosition(Saved(300, completed: true), TenMinutes).ShouldBe(TimeSpan.Zero);

    [Fact]
    public void Retomar_SemDuracaoOuSemProgresso_ComecaDoInicio()
    {
        PlaybackRules.ResumePosition(Saved(300), duration: null).ShouldBe(TimeSpan.Zero);
        PlaybackRules.ResumePosition(null, TenMinutes).ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(569, 600, false)]
    [InlineData(570, 600, true)]
    [InlineData(19.5, 20, false)] // aula curta: só conclui pelo fim do vídeo
    public void Concluida_AosNoventaECincoPorCento(double position, double duration, bool expected) =>
        PlaybackRules.IsCompletedAt(TimeSpan.FromSeconds(position), TimeSpan.FromSeconds(duration)).ShouldBe(expected);

    [Fact]
    public void ProximaAula_PulaIndisponiveisENaUltimaNaoHa()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid(), d = Guid.NewGuid();
        (Guid, bool)[] order = [(a, true), (b, false), (c, true), (d, true)];

        PlaybackRules.NextLesson(order, a).ShouldBe(c);
        PlaybackRules.NextLesson(order, d).ShouldBeNull();
        PlaybackRules.PreviousLesson(order, c).ShouldBe(a);
        PlaybackRules.PreviousLesson(order, a).ShouldBeNull();
        PlaybackRules.NextLesson(order, Guid.NewGuid()).ShouldBeNull();
    }
}

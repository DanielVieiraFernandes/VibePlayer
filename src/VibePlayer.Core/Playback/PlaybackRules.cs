namespace VibePlayer.Core.Playback;

/// <summary>Regras de progresso das aulas (spec 0006), puras para serem testadas sem player.</summary>
public static class PlaybackRules
{
    /// <summary>Fração da duração a partir da qual a aula conta como concluída.</summary>
    public const double CompletionThreshold = 0.95;

    /// <summary>Abaixo disso, retomar não vale: o usuário só abriu e fechou.</summary>
    public static readonly TimeSpan MinimumResume = TimeSpan.FromSeconds(10);

    /// <summary>Aulas mais curtas que isso só concluem pelo fim (95% de 20 s é quase o começo).</summary>
    public static readonly TimeSpan MinimumForPercentage = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Onde a aula começa: na posição salva se ela está entre 10 s e 95% e a aula não foi
    /// concluída; senão, no início. Sem duração conhecida não há como saber os 95%: começa do início.
    /// </summary>
    public static TimeSpan ResumePosition(LessonProgress? progress, TimeSpan? duration)
    {
        if (progress is null || progress.IsCompleted || duration is not { } total || total <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return progress.Position >= MinimumResume && progress.Position < total * CompletionThreshold
            ? progress.Position
            : TimeSpan.Zero;
    }

    /// <summary>Se a posição atual já conta como aula concluída (o fim do vídeo conta sempre, à parte).</summary>
    public static bool IsCompletedAt(TimeSpan position, TimeSpan duration) =>
        duration >= MinimumForPercentage && position >= duration * CompletionThreshold;

    /// <summary>Próxima aula disponível na ordem do curso, ou <c>null</c> na última.</summary>
    public static Guid? NextLesson(IReadOnlyList<(Guid Id, bool IsAvailable)> courseOrder, Guid current) =>
        Neighbor(courseOrder, current, step: 1);

    /// <summary>Aula anterior disponível na ordem do curso, ou <c>null</c> na primeira.</summary>
    public static Guid? PreviousLesson(IReadOnlyList<(Guid Id, bool IsAvailable)> courseOrder, Guid current) =>
        Neighbor(courseOrder, current, step: -1);

    private static Guid? Neighbor(IReadOnlyList<(Guid Id, bool IsAvailable)> order, Guid current, int step)
    {
        var index = order.ToList().FindIndex(l => l.Id == current);
        if (index < 0)
        {
            return null;
        }

        for (var i = index + step; i >= 0 && i < order.Count; i += step)
        {
            if (order[i].IsAvailable)
            {
                return order[i].Id;
            }
        }

        return null;
    }
}

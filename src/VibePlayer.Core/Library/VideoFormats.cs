namespace VibePlayer.Core.Library;

/// <summary>Extensões tratadas como aula. O LibVLC toca todas; a lista só decide o que aparece.</summary>
public static class VideoFormats
{
    public static IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mkv", ".webm", ".mov", ".avi", ".wmv", ".flv", ".mpg", ".mpeg", ".ts",
    };

    public static bool IsVideo(string fileName) => Extensions.Contains(Path.GetExtension(fileName));
}

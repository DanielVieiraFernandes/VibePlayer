using System.Text.RegularExpressions;

namespace VibePlayer.UnitTests;

/// <summary>
/// Garante que as Views só usam tokens do design system (CA2 da spec 0003). Lê os .axaml do
/// repositório como texto: é barato e pega o erro antes de qualquer revisão visual.
/// </summary>
public sealed partial class ViewConventionsTests
{
    // Cor hexadecimal como valor de atributo: Background="#FFF", Color="#80112233".
    [GeneratedRegex(@"=""\s*#[0-9A-Fa-f]{3,8}\s*""")]
    private static partial Regex HexColor();

    // Cor por nome em propriedade de pincel: Foreground="Red". Recursos e bindings começam com "{".
    [GeneratedRegex(@"\b(Background|Foreground|BorderBrush|Fill|Stroke|Color)=""(?!\{)[A-Za-z]+""")]
    private static partial Regex NamedColor();

    [GeneratedRegex("""\bFontSize="\s*\d""")]
    private static partial Regex LiteralFontSize();

    [Fact]
    public void Views_NaoUsamCorOuTamanhoDeFonteLiteral()
    {
        var viewsDirectory = Path.Combine(FindRepositoryRoot(), "src", "VibePlayer.App", "Views");
        var files = Directory.GetFiles(viewsDirectory, "*.axaml", SearchOption.AllDirectories);
        files.ShouldNotBeEmpty();

        var violations = (
            from file in files
            from line in File.ReadLines(file).Select((text, index) => (text, number: index + 1))
            where HexColor().IsMatch(line.text) || NamedColor().IsMatch(line.text) || LiteralFontSize().IsMatch(line.text)
            select $"{Path.GetRelativePath(viewsDirectory, file)}:{line.number}: {line.text.Trim()}").ToList();

        violations.ShouldBeEmpty("Use os tokens de Styles/Tokens.axaml (ver DESIGN.md).");
    }

    [Theory]
    [InlineData("""<Border Background="#FF0000" />""")]
    [InlineData("""<TextBlock Foreground="Red" />""")]
    [InlineData("""<TextBlock FontSize="14" />""")]
    public void Regras_DetectamValoresLiterais(string line) =>
        (HexColor().IsMatch(line) || NamedColor().IsMatch(line) || LiteralFontSize().IsMatch(line)).ShouldBeTrue();

    [Fact]
    public void Regras_AceitamTokens() =>
        NamedColor().IsMatch("""<Border Background="{DynamicResource SurfaceBackgroundBrush}" />""").ShouldBeFalse();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VibePlayer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}

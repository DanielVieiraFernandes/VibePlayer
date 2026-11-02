using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace VibePlayer.E2ETests;

/// <summary>
/// Confere os tokens de cor (CA3 e CA4 da spec 0003). Fica nos E2E porque carregar o .axaml
/// precisa do Avalonia inicializado.
/// </summary>
public sealed class DesignTokenTests
{
    private const double NormalText = 4.5;
    private const double LargeTextAndIcons = 3.0;

    private static readonly string[] Backgrounds = ["AppBackgroundBrush", "SurfaceBackgroundBrush", "SurfaceRaisedBackgroundBrush"];

    /// <summary>Mesma tabela da seção "Contraste" do DESIGN.md; ao mudar uma, mude a outra.</summary>
    public static TheoryData<string, string, double> Pairs()
    {
        var data = new TheoryData<string, string, double>();
        foreach (var background in Backgrounds)
        {
            data.Add("TextPrimaryBrush", background, NormalText);
            data.Add("TextSecondaryBrush", background, NormalText);
            data.Add("AccentTextBrush", background, NormalText);
            data.Add("AccentBrush", background, LargeTextAndIcons);
            data.Add("FocusRingBrush", background, LargeTextAndIcons);
            data.Add("BorderStrongBrush", background, LargeTextAndIcons);
        }

        foreach (var accent in new[] { "AccentBrush", "AccentHoverBrush", "AccentPressedBrush" })
        {
            data.Add("TextOnAccentBrush", accent, NormalText);
        }

        data.Add("TextPrimaryBrush", "AccentSubtleBrush", NormalText);
        data.Add("TextSecondaryBrush", "AccentSubtleBrush", NormalText);

        foreach (var state in new[] { "Error", "Warning", "Success", "Info" })
        {
            data.Add($"{state}Brush", $"{state}SubtleBrush", NormalText);
            data.Add($"{state}Brush", "SurfaceBackgroundBrush", NormalText);
            data.Add("TextPrimaryBrush", $"{state}SubtleBrush", NormalText);
        }

        data.Add("VideoTextBrush", "VideoBackgroundBrush", NormalText);
        data.Add("VideoTextSecondaryBrush", "VideoBackgroundBrush", NormalText);
        return data;
    }

    [AvaloniaFact]
    public void TodoTokenDeCor_ExisteNosTemasClaroEEscuro()
    {
        var light = ThemeTokens(ThemeVariant.Light);
        var dark = ThemeTokens(ThemeVariant.Dark);

        light.ShouldNotBeEmpty();
        light.Keys.Except(dark.Keys).ShouldBeEmpty("Tokens só no tema claro");
        dark.Keys.Except(light.Keys).ShouldBeEmpty("Tokens só no tema escuro");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Pairs))]
    public void ParesDeTextoEFundo_AtendemContrasteWcagAA(string foreground, string background, double minimum)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var tokens = ThemeTokens(theme);
            var ratio = ContrastRatio(tokens[foreground], tokens[background]);

            ratio.ShouldBeGreaterThanOrEqualTo(minimum, $"{foreground} sobre {background} no tema {theme}: {ratio:F2}:1");
        }
    }

    private static Dictionary<string, Color> ThemeTokens(ThemeVariant theme)
    {
        var tokens = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri("avares://VibePlayer/Styles/Tokens.axaml"));
        var themeDictionary = (ResourceDictionary)tokens.ThemeDictionaries[theme];
        // TryGetResource, e não o valor cru do dicionário: o XAML compilado guarda os recursos como
        // conteúdo adiado, que só vira o pincel de verdade quando é pedido.
        return themeDictionary.Keys.Cast<string>().ToDictionary(
            key => key,
            key => themeDictionary.TryGetResource(key, theme, out var value) && value is ISolidColorBrush brush
                ? brush.Color
                : throw new InvalidOperationException($"Token {key} não é um SolidColorBrush."));
    }

    // Fórmulas do WCAG 2.x: luminância relativa em sRGB linearizado e razão (L1 + 0,05) / (L2 + 0,05).
    private static double ContrastRatio(Color a, Color b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }
}

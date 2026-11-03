using VibePlayer.Core.Library;

namespace VibePlayer.UnitTests.Core.Library;

public sealed class NaturalStringComparerTests
{
    private static List<string> Sort(params string[] values) => values.OrderBy(v => v, NaturalStringComparer.Instance).ToList();

    [Fact]
    public void Numeros_OrdenamPeloValor()
    {
        Sort("10 - a.mp4", "2 - b.mp4", "1.1 - c.mp4").ShouldBe(["1.1 - c.mp4", "2 - b.mp4", "10 - a.mp4"]);
    }

    [Fact]
    public void ZerosAEsquerda_NaoMudamAOrdem()
    {
        Sort("Aula 010", "Aula 9", "Aula 02").ShouldBe(["Aula 02", "Aula 9", "Aula 010"]);
    }

    [Fact]
    public void MaiusculasEAcentos_NaoMudamAOrdem()
    {
        Sort("Ética", "eletrônica", "Avançado", "abertura").ShouldBe(["abertura", "Avançado", "eletrônica", "Ética"]);
    }

    [Fact]
    public void Empate_UsaOrdemOrdinalParaSerEstavel()
    {
        var comparer = NaturalStringComparer.Instance;

        comparer.Compare("aula", "Aula").ShouldNotBe(0);
        Math.Sign(comparer.Compare("aula", "Aula")).ShouldBe(-Math.Sign(comparer.Compare("Aula", "aula")));
    }

    [Fact]
    public void NumerosMaioresQueLong_NaoEstouram()
    {
        Sort("99999999999999999999999", "100000000000000000000000").ShouldBe(["99999999999999999999999", "100000000000000000000000"]);
    }

    [Fact]
    public void Nulos_VemPrimeiro()
    {
        NaturalStringComparer.Instance.Compare(null, "a").ShouldBeLessThan(0);
        NaturalStringComparer.Instance.Compare(null, null).ShouldBe(0);
    }
}

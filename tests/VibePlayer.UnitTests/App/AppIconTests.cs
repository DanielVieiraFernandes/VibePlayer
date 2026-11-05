using System.Buffers.Binary;
using System.Text;

namespace VibePlayer.UnitTests.App;

/// <summary>O ícone oficial existe nos formatos de cada SO e o projeto o referencia (spec 0008).</summary>
public sealed class AppIconTests
{
    private static string AppDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VibePlayer.slnx")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull();
        return Path.Combine(dir.FullName, "src", "VibePlayer.App");
    }

    private static byte[] Asset(string name) => File.ReadAllBytes(Path.Combine(AppDirectory(), "Assets", name));

    [Fact]
    public void Ico_TemOsTamanhosDoWindows()
    {
        var bytes = Asset("app.ico");

        // Cabeçalho ICONDIR: reservado 0, tipo 1 (ícone), quantidade de imagens.
        BitConverter.ToUInt16(bytes, 0).ShouldBe((ushort)0);
        BitConverter.ToUInt16(bytes, 2).ShouldBe((ushort)1);
        BitConverter.ToUInt16(bytes, 4).ShouldBeGreaterThanOrEqualTo((ushort)6);
    }

    [Fact]
    public void Icns_TemAssinaturaDoMacOS()
    {
        Encoding.ASCII.GetString(Asset("app.icns"), 0, 4).ShouldBe("icns");
    }

    [Fact]
    public void Png_Tem256Pixels()
    {
        var bytes = Asset("icon-256.png");
        Encoding.ASCII.GetString(bytes, 1, 3).ShouldBe("PNG");

        // Largura e altura no IHDR, big-endian.
        BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)).ShouldBe(256);
        BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)).ShouldBe(256);
    }

    [Fact]
    public void Projeto_UsaOIconeNoExecutavelEEmpacotaOsArquivos()
    {
        var csproj = File.ReadAllText(Path.Combine(AppDirectory(), "VibePlayer.App.csproj"));
        csproj.ShouldContain("<ApplicationIcon>Assets\\app.ico</ApplicationIcon>");
        csproj.ShouldContain("app.icns");
        csproj.ShouldContain("icon-256.png");
    }
}

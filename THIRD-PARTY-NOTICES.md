# Avisos de componentes de terceiros

O VibePlayer usa os componentes abaixo, cada um sob sua própria licença. Esses componentes não são cobertos pela licença do VibePlayer.

## Distribuídos com o aplicativo

| Componente | Licença | Projeto |
|---|---|---|
| Avalonia (Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent) | MIT | https://github.com/AvaloniaUI/Avalonia |
| Avalonia.Fonts.Inter (inclui a fonte Inter) | MIT (Inter: SIL Open Font License 1.1) | https://github.com/AvaloniaUI/Avalonia · https://github.com/rsms/inter |
| CommunityToolkit.Mvvm | MIT | https://github.com/CommunityToolkit/dotnet |
| FluentIcons.Avalonia (inclui os ícones Fluent UI System Icons, da Microsoft) | MIT (ícones: MIT) | https://github.com/davidxuang/FluentIcons · https://github.com/microsoft/fluentui-system-icons |
| Microsoft.Extensions.DependencyInjection | MIT | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Logging | MIT | https://github.com/dotnet/runtime |
| Microsoft.EntityFrameworkCore.Sqlite (inclui Microsoft.Data.Sqlite) | MIT | https://github.com/dotnet/efcore |
| SQLitePCLRaw (bundle_e_sqlite3) | Apache 2.0 | https://github.com/ericsink/SQLitePCL.raw |
| SQLite (biblioteca nativa e_sqlite3) | Domínio público | https://www.sqlite.org/copyright.html |
| FluentValidation | Apache 2.0 | https://github.com/FluentValidation/FluentValidation |
| LibVLCSharp | LGPL 2.1 ou posterior | https://code.videolan.org/videolan/LibVLCSharp |
| LibVLC (VLC media player: bibliotecas e plugins; Windows e macOS) | LGPL 2.1 ou posterior | https://code.videolan.org/videolan/vlc |
| SkiaSharp | MIT | https://github.com/mono/SkiaSharp |
| Serilog | Apache 2.0 | https://github.com/serilog/serilog |
| Serilog.Sinks.File | Apache 2.0 | https://github.com/serilog/serilog-sinks-file |
| Serilog.Extensions.Logging | Apache 2.0 | https://github.com/serilog/serilog-extensions-logging |
| .NET Runtime | MIT | https://github.com/dotnet/runtime |

## Usados apenas no desenvolvimento (não distribuídos)

| Componente | Licença | Projeto |
|---|---|---|
| xUnit.net (xunit.v3, xunit.runner.visualstudio) | Apache 2.0 | https://github.com/xunit/xunit |
| Microsoft.NET.Test.Sdk | MIT | https://github.com/microsoft/vstest |
| NSubstitute | BSD-3-Clause | https://github.com/nsubstitute/NSubstitute |
| Shouldly | BSD-3-Clause | https://github.com/shouldly/shouldly |
| NetArchTest.Rules | MIT | https://github.com/BenMorris/NetArchTest |
| Avalonia.Headless.XUnit | MIT | https://github.com/AvaloniaUI/Avalonia |
| Microsoft.EntityFrameworkCore.Design e dotnet-ef | MIT | https://github.com/dotnet/efcore |

## Bibliotecas sob LGPL

LibVLCSharp e LibVLC são distribuídos como bibliotecas separadas, carregadas em tempo de execução, sem modificações. Você pode substituí-las por outras versões compatíveis trocando `LibVLCSharp.dll` e os arquivos da pasta `libvlc/` do pacote. O código-fonte de cada uma está nos endereços da tabela acima. O texto completo da licença está em `licenses/LGPL-2.1.txt`, incluído em cada pacote.

No VLC, só os plugins necessários para tocar arquivos locais são distribuídos; os que têm licença GPL ficam de fora. No Linux, o VibePlayer usa o VLC instalado no sistema.

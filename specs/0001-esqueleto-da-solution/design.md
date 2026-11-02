# 0001 — Design técnico

## Abordagem

```
VibePlayer.slnx
Directory.Build.props        Configurações comuns a todos os projetos
Directory.Packages.props     Versões de todos os pacotes NuGet (Central Package Management)
.editorconfig
global.json                  Fixa a versão do SDK
src/
  VibePlayer.App/            Avalonia; Program.cs, App.axaml, MainWindow, bootstrap de DI e Serilog
  VibePlayer.Core/           IAppPaths, abstrações; sem dependências externas além de Microsoft.Extensions.Logging.Abstractions
  VibePlayer.Infrastructure/ AppPaths (implementação), extensão AddInfrastructure() para o DI
tests/
  VibePlayer.UnitTests/      xUnit + NSubstitute + Shouldly; teste de AppPaths e teste de arquitetura
  VibePlayer.E2ETests/       xUnit + Avalonia.Headless.XUnit; sobe o App e verifica a janela principal
.github/workflows/ci.yml
```

Referências: `App → Infrastructure → Core`; `App → Core`. Testes referenciam o que testam.

Inicialização (`Program.Main`):
1. Configura o Serilog antes de tudo, para que falhas durante o bootstrap já fiquem registradas.
2. Monta o `ServiceProvider` (`AddCore()`, `AddInfrastructure()`, ViewModels).
3. Registra a estratégia de exceções não tratadas (abaixo).
4. Inicia o Avalonia; no encerramento, `Log.CloseAndFlush()` para não perder as últimas linhas.

### Exceções não derrubam o app

O .NET só permite "salvar" o processo em alguns pontos. Por isso a estratégia tem duas partes: tratar onde é possível e **impedir que o código chegue aos pontos onde não é**.

| Origem da exceção | Tratamento | O app continua? |
|---|---|---|
| Thread de UI (eventos, comandos, bindings) | `Dispatcher.UIThread.UnhandledException` → registra `Error` e marca `Handled = true` | Sim |
| `Task` cuja exceção nunca foi observada | `TaskScheduler.UnobservedTaskException` → registra `Error` e chama `SetObserved()` | Sim |
| Trabalho em segundo plano iniciado pelo app | Sempre via `FireAndForget(ILogger)` (extensão em `Core`), que captura, registra e não relança | Sim |
| Thread criada manualmente, `async void` fora da UI, callback de `Timer` | **Não é recuperável no .NET**: `AppDomain.UnhandledException` só permite registrar `Fatal` antes de o processo encerrar | Não — por isso é proibido por regra (abaixo) |
| Falha nativa (`StackOverflow`, `AccessViolation` em LibVLC/Whisper) | Não capturável em processo nenhum | Não — mitigado em specs futuras isolando o Whisper em processo separado |

Regras de código derivadas (registradas no CLAUDE.md):
- `async void` só em handlers de evento da UI; todo o resto retorna `Task`.
- Nada de `new Thread`, `Timer` de `System.Threading` ou `Task.Run` solto: trabalho em segundo plano passa por `FireAndForget` ou é aguardado.
- Erro tratado globalmente nunca é silencioso: sempre vai para o log com stack trace. Avisar o usuário na tela fica para a spec do layout (aqui ainda não existe UI para isso).

## Decisões

| Decisão | Motivo | Alternativas descartadas |
|---|---|---|
| .NET 10 (LTS), fixado em `global.json` | Suporte longo; SDK já instalado localmente | .NET 11 (STS, suporte curto) |
| Avalonia na versão estável mais recente, fixada em `Directory.Packages.props` | Versão exata confirmada no momento da implementação | — |
| Formato `.slnx` | Padrão do SDK do .NET 10, XML legível, menos conflitos de merge | `.sln` clássico |
| Central Package Management | Uma versão por pacote em toda a solution; evita testes e app usando versões diferentes | Versão em cada `.csproj` |
| `TreatWarningsAsErrors` + `Nullable` habilitados | Problemas aparecem no build, não em produção | Warnings livres |
| `Microsoft.Extensions.DependencyInjection` direto, sem Generic Host | App desktop não precisa de hosted services nem da configuração do Host; menos peças | `Microsoft.Extensions.Hosting` |
| Classes recebem `ILogger<T>` (Microsoft.Extensions.Logging), Serilog só no `App` | `Core` não depende do Serilog; trocar o provedor de log não afeta o domínio | `Serilog.ILogger` injetado direto |
| Pasta de dados via `Environment.SpecialFolder.ApplicationData` | `%APPDATA%` no Windows, `~/.config` no Linux/macOS, sem código por SO | Caminhos fixos por SO |
| Teste de arquitetura com NetArchTest | Garante CA7 automaticamente a cada build | Revisão manual |
| E2E com `Avalonia.Headless.XUnit` | Roda a UI real sem janela visível, funciona na CI dos três SOs | Appium/WinAppDriver (só Windows, mais frágil) |

## Mudanças em dados

Nenhuma (sem banco nesta spec).

## Dependências novas

| Pacote | Projeto | Licença |
|---|---|---|
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter | App | MIT |
| CommunityToolkit.Mvvm | App | MIT |
| Microsoft.Extensions.DependencyInjection | App, Infrastructure | MIT |
| Microsoft.Extensions.Logging.Abstractions | Core | MIT |
| Serilog, Serilog.Sinks.File, Serilog.Extensions.Logging | App | Apache 2.0 |
| xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk | testes | Apache 2.0 / MIT |
| NSubstitute | UnitTests | BSD-3 |
| Shouldly | testes | BSD-3 |
| NetArchTest.Rules | UnitTests | MIT |
| Avalonia.Headless.XUnit | E2ETests | MIT |

Todas compatíveis com distribuição proprietária. Serão listadas em `THIRD-PARTY-NOTICES.md`.

## Logs e erros

- Arquivo: `<pasta de dados>/logs/vibePlayer-<data>.log`, rotação diária, 14 arquivos retidos.
- Nível mínimo `Information` em release e `Debug` em debug; `Microsoft.*` e `System.*` em `Warning` para não poluir.
- Linha de inicialização: versão do app, SO e arquitetura, versão do .NET.
- Exceção tratada globalmente (UI, task não observada, `FireAndForget`): `Error` com stack trace; o app segue rodando.
- Exceção irrecuperável (`AppDomain.UnhandledException`): `Fatal` com stack trace, seguida de `CloseAndFlush()` para garantir que o registro chegue ao disco antes de o processo encerrar.

## Riscos

- **E2E headless na CI do Linux/macOS:** o Avalonia.Headless não precisa de servidor gráfico, mas fontes do sistema podem faltar; usamos `Avalonia.Fonts.Inter` embutida para evitar isso.
- **Engolir exceções esconde estado inconsistente:** um comando que falhou no meio pode deixar o ViewModel num estado parcial. Mitigação: cada comando que altera estado trata os próprios erros esperados; o handler global é rede de segurança, não fluxo normal.
- **ffmpeg local é build GPL** (`full_build` da gyan.dev): irrelevante nesta spec, mas o binário distribuído no futuro precisará ser um build LGPL.

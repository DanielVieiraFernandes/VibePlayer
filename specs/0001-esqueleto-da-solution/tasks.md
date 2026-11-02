# 0001 — Tarefas

## Implementação

- [x] T1 — `git init` com branch `main`; commit inicial com CLAUDE.md e `specs/`; criar branch `feat/0001-esqueleto-da-solution`.
- [x] T2 — `.gitignore` (template .NET), `.gitattributes` (normalização de fim de linha), `.editorconfig`, `global.json`.
- [x] T3 — `Directory.Build.props` e `Directory.Packages.props` com as dependências do design.
- [x] T4 — Criar `VibePlayer.slnx` e os cinco projetos com as referências entre eles.
- [x] T5 — `Core`: interface `IAppPaths` (pasta de dados, logs, models), extensão `FireAndForget(ILogger)` para `Task` e extensão `AddCore()`.
- [x] T6 — `Infrastructure`: `AppPaths` (resolve e cria as pastas) e extensão `AddInfrastructure()`.
- [x] T7 — `App`: `Program.cs` com bootstrap de Serilog, DI e handlers de exceção; `App.axaml` com tema Fluent; `MainWindow` + `MainWindowViewModel` vazios com o título.
- [x] T8 — `.github/workflows/ci.yml` com matriz Windows/macOS/Linux: restore, build, test.
- [x] T9 — `LICENSE` proprietário, `README.md` (descrição, status, aviso de que não aceita contribuições), `THIRD-PARTY-NOTICES.md`.
- [x] T10 — Atualizar CLAUDE.md (`.slnx`, nomes finais, comandos confirmados, regras de exceção: `async void` só em eventos de UI, segundo plano via `FireAndForget`).
- [x] T11 — Criar o repositório público `VibePlayer` no GitHub com `gh repo create`, adicionar como `origin`, fazer push de `main` e da branch da spec, e conferir a primeira execução da CI (valida CA8).

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1 | Manual/CI | `dotnet build` sem warnings |
| CA2 | Manual/CI | `dotnet test` |
| CA3 | E2E | `MainWindowTests.Inicializacao_AbreJanelaComTituloDoApp` |
| CA4 | Manual | Executar o app e conferir o arquivo em `logs/` — conferido no Windows em 2026-11-02 |
| CA5 | Unitário | `TaskExtensionsTests.FireAndForget_TaskFalha_RegistraErroSemRelancar` |
| CA5 | E2E | `UnhandledExceptionTests.ExcecaoEmComandoDaUi_RegistraErroEJanelaContinuaAberta` |
| CA5 | E2E | `UnhandledExceptionTests.TaskNaoObservada_RegistraErroSemEncerrar` |
| CA6 | Unitário | `AppPathsTests.Construtor_PastasInexistentes_CriaLogsEModels` (pasta raiz temporária injetada) |
| CA7 | Unitário | `ArchitectureTests.Core_NaoDependeDeInfraestruturaUiOuSerilog` |
| CA8 | CI | Primeira execução do workflow após o push (T11) passando nos três SOs — conferido no PR #1 |

## Documentação

- [x] CLAUDE.md (T10)
- [x] `DESIGN.md` — não se aplica
- [x] Wiki — não se aplica

## Desvios em relação ao design

- **`AddCore()` não foi criada:** o Core ainda não tem nada a registrar no container, e criá-la exigiria adicionar `Microsoft.Extensions.DependencyInjection.Abstractions` ao Core sem uso. Entra quando o Core tiver o primeiro serviço.
- **Pacotes extras da mesma família (MIT):** `Microsoft.Extensions.DependencyInjection.Abstractions` (Infrastructure, para `AddInfrastructure`) e `Microsoft.Extensions.Logging` (App, para `AddLogging`).
- **xunit.v3 na linha 3.x** (3.2.2) em vez da 4.x: o `Avalonia.Headless.XUnit` 12 depende dela.
- **Avalonia 12.1.5:** no modo headless de testes o Avalonia 12 recria o app e o dispatcher da UI a cada teste. O `GlobalExceptionHandler` passou a controlar o anexo por instância de dispatcher (e não uma vez só), o que também deixa o app correto se o dispatcher for recriado.
- **Testes extras:** `AppPathsTests.Construtor_PastasJaExistem_PreservaConteudo`, `AppPathsTests.CreateDefault_UsaPastaApplicationDataDoUsuario`, `TaskExtensionsTests.FireAndForget_TaskCancelada_RegistraDebugENaoErro` e `TaskExtensionsTests.FireAndForget_TaskConcluiComSucesso_NaoRegistraNada`.
- **Merge do PR #1 em `main`:** feito antes da adoção do GitFlow (spec 0002); `develop` foi criada a partir desse `main`.

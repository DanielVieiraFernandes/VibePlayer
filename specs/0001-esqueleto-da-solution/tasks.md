# 0001 — Tarefas

## Implementação

- [ ] T1 — `git init` com branch `main`; commit inicial com CLAUDE.md e `specs/`; criar branch `feat/0001-esqueleto-da-solution`.
- [ ] T2 — `.gitignore` (template .NET), `.gitattributes` (normalização de fim de linha), `.editorconfig`, `global.json`.
- [ ] T3 — `Directory.Build.props` e `Directory.Packages.props` com as dependências do design.
- [ ] T4 — Criar `VibePlayer.slnx` e os cinco projetos com as referências entre eles.
- [ ] T5 — `Core`: interface `IAppPaths` (pasta de dados, logs, models), extensão `FireAndForget(ILogger)` para `Task` e extensão `AddCore()`.
- [ ] T6 — `Infrastructure`: `AppPaths` (resolve e cria as pastas) e extensão `AddInfrastructure()`.
- [ ] T7 — `App`: `Program.cs` com bootstrap de Serilog, DI e handlers de exceção; `App.axaml` com tema Fluent; `MainWindow` + `MainWindowViewModel` vazios com o título.
- [ ] T8 — `.github/workflows/ci.yml` com matriz Windows/macOS/Linux: restore, build, test.
- [ ] T9 — `LICENSE` proprietário, `README.md` (descrição, status, aviso de que não aceita contribuições), `THIRD-PARTY-NOTICES.md`.
- [ ] T10 — Atualizar CLAUDE.md (`.slnx`, nomes finais, comandos confirmados, regras de exceção: `async void` só em eventos de UI, segundo plano via `FireAndForget`).
- [ ] T11 — Criar o repositório público `VibePlayer` no GitHub com `gh repo create`, adicionar como `origin`, fazer push de `main` e da branch da spec, e conferir a primeira execução da CI (valida CA8).

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1 | Manual/CI | `dotnet build` sem warnings |
| CA2 | Manual/CI | `dotnet test` |
| CA3 | E2E | `MainWindowTests.Inicializacao_AbreJanelaComTituloDoApp` |
| CA4 | Manual | Executar o app e conferir o arquivo em `logs/` |
| CA5 | Unitário | `TaskExtensionsTests.FireAndForget_TaskFalha_RegistraErroSemRelancar` |
| CA5 | E2E | `UnhandledExceptionTests.ExcecaoEmComandoDaUi_RegistraErroEJanelaContinuaAberta` |
| CA5 | E2E | `UnhandledExceptionTests.TaskNaoObservada_RegistraErroSemEncerrar` |
| CA6 | Unitário | `AppPathsTests.Construtor_PastasInexistentes_CriaLogsEModels` (pasta raiz temporária injetada) |
| CA7 | Unitário | `ArchitectureTests.Core_NaoDependeDeInfraestruturaUiOuSerilog` |
| CA8 | CI | Primeira execução do workflow após o push (T11) passando nos três SOs |

## Documentação

- [ ] CLAUDE.md (T10)
- [ ] `DESIGN.md` — não se aplica
- [ ] Wiki — não se aplica

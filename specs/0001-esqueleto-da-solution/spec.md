# 0001 — Esqueleto da solution

**Status:** Aprovada
**Branch:** `feat/0001-esqueleto-da-solution`

## Problema

Não existe código, repositório nem estrutura de build. Antes de qualquer funcionalidade (biblioteca, player, transcrição, chat) é preciso uma base que já cumpra as regras do projeto: separação em camadas, injeção de dependência, logs em arquivo, testes unitários e E2E rodando, CI nos três sistemas operacionais e os arquivos legais de um repositório público com licença proprietária.

## Objetivo

Ao final, `dotnet build` e `dotnet test` passam em Windows, macOS e Linux; o app abre uma janela principal vazia, grava log em arquivo na pasta de dados do usuário; e o repositório git local está pronto para ser publicado no GitHub.

## Escopo

**Inclui:**
- Repositório git local (`main`) com `.gitignore` e `.gitattributes` para .NET.
- Solution com os projetos `App`, `Core`, `Infrastructure`, `UnitTests` e `E2ETests`.
- Configurações de build compartilhadas (versão do .NET, nullable, warnings como erro) e versões de pacotes centralizadas.
- `.editorconfig` com o estilo de código.
- Injeção de dependência e bootstrap do app.
- Serviço que resolve a pasta de dados do usuário em cada SO (`IAppPaths`).
- Serilog gravando em arquivo, com captura de exceções não tratadas.
- Janela principal vazia (título e área em branco), sem layout definitivo.
- Um teste unitário e um teste E2E de fumaça.
- Workflow de CI no GitHub Actions (build + testes, matriz Windows/macOS/Linux).
- `LICENSE` proprietário, `README.md`, `THIRD-PARTY-NOTICES.md`.
- Repositório público `VibePlayer` no GitHub, com o código enviado.

**Não inclui** (specs futuras):
- SQLite / EF Core e migrations.
- `DESIGN.md`, tema, layout real da janela.
- Biblioteca de cursos, player (LibVLC), ffmpeg, Whisper, LLM, `ISecretStore`, FluentValidation (entra na primeira spec com validação de verdade).
- Workflow de release, empacotamento, assinatura/notarização no macOS.
- Aviso visual ao usuário quando um erro é tratado (entra na spec do layout).
- Bloqueio automático de PRs externos (configuração no GitHub ou Action) — spec própria.

## Critérios de aceite

- [ ] CA1 — Dado o repositório recém-clonado, quando rodo `dotnet build`, então compila sem erros nem warnings.
- [ ] CA2 — Quando rodo `dotnet test`, então os testes unitários e E2E passam.
- [ ] CA3 — Dado que inicio o app, então abre uma janela com o título do app.
- [ ] CA4 — Dado que inicio o app, então é criado um arquivo de log em `<pasta de dados>/logs/` com uma linha de inicialização contendo versão do app e SO.
- [ ] CA5 — Dada uma exceção não tratada na thread de UI, numa `Task` não observada ou num trabalho iniciado com `FireAndForget`, então ela é registrada no log com stack trace e **o app continua aberto e funcionando**. Exceções irrecuperáveis pelo .NET (ver design) são registradas como `Fatal` antes do encerramento.
- [ ] CA6 — `IAppPaths` resolve a pasta de dados para `<ApplicationData>/<NomeDoApp>` e cria as subpastas `logs/` e `models/` se não existirem.
- [ ] CA7 — `Core` não referencia `Infrastructure`, `App`, Avalonia nem Serilog (verificado por teste de arquitetura).
- [ ] CA8 — O workflow de CI está definido para rodar build + testes em `windows-latest`, `macos-latest` e `ubuntu-latest` em push e PR para `main`, e a primeira execução no repositório remoto passa nos três SOs.

## Impacto para o usuário

Nenhum ainda — não há release. Wiki ainda não precisa ser criada.

## Questões em aberto

1. ~~**Nome do app.**~~ Resolvido: **VibePlayer** (namespaces `VibePlayer.*`, pasta de dados `VibePlayer`, título da janela).
2. ~~**Repositório remoto.**~~ Resolvido: criado por Claude via `gh` como repositório **público** `VibePlayer` na conta autenticada, ao final da implementação, com push de `main` e da branch da spec.
3. ~~**Titular da licença.**~~ Resolvido: **Daniel Vieira Fernandes** (`LICENSE` e copyright).

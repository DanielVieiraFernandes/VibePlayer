# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Visão geral

Player desktop **multiplataforma** (Windows, macOS e Linux) para cursos em vídeo armazenados localmente — cursos comprados que vêm só como arquivos de vídeo, sem plataforma. Ao lado do vídeo há um chat com LLM que conhece o conteúdo da aula atual e permite:

- fazer perguntas sobre a aula;
- gerar um resumo da aula;
- listar os principais pontos, cada um com timestamp clicável que leva o vídeo àquele momento.

O código-fonte fica em repositório público no GitHub, mas o projeto **não é open source** e **não aceita contribuições externas**: a licença é proprietária (todos os direitos reservados), com permissão apenas para baixar e usar os binários publicados em Releases. Não sugira nem adicione licenças open source, `CONTRIBUTING.md` convidando PRs ou templates de contribuição.

O projeto está no início: a arquitetura abaixo é a **planejada**. Ao criar ou mudar algo estrutural, atualize este arquivo.

## Fluxo de trabalho (SDD — obrigatório)

O desenvolvimento é **Spec-Driven**: nenhuma mudança de código começa sem uma spec aprovada pelo mantenedor.

1. Para cada mudança, crie `specs/NNNN-nome-curto/` a partir de `specs/_template/` (`spec.md`, `design.md`, `tasks.md`), com status **Rascunho**.
2. **Pare e peça aprovação.** Não escreva código de produção nem testes até o mantenedor aprovar ou pedir ajustes. Ajustes pedidos → revise a spec e peça aprovação de novo.
3. Aprovada → status **Aprovada**, implemente seguindo `tasks.md`, marcando cada tarefa ao concluir. Os testes descritos na spec fazem parte da entrega.
4. Se durante a implementação surgir algo que muda o que foi aprovado (escopo, decisão técnica, dependência nova), pare e atualize a spec para nova aprovação em vez de decidir sozinho.
5. Concluída → status **Implementada**; atualize a documentação afetada (este arquivo, `docs/`, wiki, `DESIGN.md`).

Detalhes em [specs/README.md](specs/README.md).

### Git e GitHub

- `main` sempre compilando e com testes passando; uma branch por spec: `feat/NNNN-nome`, `fix/NNNN-nome`.
- Commits no padrão Conventional Commits, em português: `feat(chat): adiciona streaming de respostas`.
- CI (GitHub Actions) roda build + testes unitários + E2E em todo PR, numa matriz Windows / macOS / Linux.
- Releases pelo GitHub Releases, com `dotnet publish` self-contained por RID: `win-x64`, `osx-arm64`, `osx-x64`, `linux-x64`.

## Stack

- **.NET / C#** com **Avalonia UI**, padrão MVVM com `CommunityToolkit.Mvvm`.
- **Vídeo:** LibVLCSharp (`LibVLCSharp.Avalonia`).
- **Transcrição:** Whisper local via `Whisper.net`; ffmpeg extrai o áudio antes. Modelos do Whisper são baixados na primeira execução, não vão no repositório nem no instalador.
- **LLM:** provedores configuráveis — Claude via SDK oficial (`Anthropic`, modelo padrão `claude-opus-5-5`, streaming no chat) e Ollama local (`http://localhost:11434`).
- **Persistência:** SQLite com EF Core (`Microsoft.EntityFrameworkCore.Sqlite`). Migrations aplicadas automaticamente na inicialização, pois o schema evolui entre versões instaladas nas máquinas dos usuários.
- **Validação:** FluentValidation. Validadores ficam em `Core` (regras de domínio e de configuração, ex.: URL do Ollama, chave de API, pasta da biblioteca) e os ViewModels os usam para expor erros à UI via `INotifyDataErrorInfo`. Não use DataAnnotations para regras de validação.
- **Logs:** Serilog, apenas `Serilog.Sinks.File`.
- **Testes:** xUnit, NSubstitute (fakes/mocks), Shouldly (asserções), `Avalonia.Headless.XUnit` (E2E de UI).

> Licenças de terceiros: o app é proprietário, então toda dependência precisa ter licença que permita uso em software fechado (MIT, Apache 2.0, BSD, LGPL com linkagem dinâmica). LibVLC e ffmpeg são LGPL: distribua-os como bibliotecas/binários separados, use builds LGPL do ffmpeg (nunca GPL) e inclua os avisos e textos das licenças em `THIRD-PARTY-NOTICES.md` e no pacote de release. Nunca adicione dependências GPL/AGPL. FluentAssertions 8+ tem licença comercial — não use. Toda spec que adiciona dependência registra a licença no `design.md`.

## Multiplataforma

- Nunca use APIs exclusivas de um sistema operacional diretamente em `Core` ou nos ViewModels; quando inevitável, isole atrás de interface em `Core` com implementação por SO em `Infrastructure`.
- Caminhos sempre com `Path.Combine` e `Environment.GetFolderPath`, nunca separadores ou pastas fixas.
- **Segredos (chaves de API):** interface `ISecretStore` com implementação no cofre nativo de cada SO — Windows: DPAPI/Credential Manager; macOS: Keychain; Linux: libsecret (Secret Service). Se o cofre não estiver disponível (Linux sem keyring), avise o usuário e não grave a chave em texto puro.
- **Binários nativos por SO:** LibVLC (no Linux depende do `libvlc` instalado no sistema), ffmpeg e runtimes do Whisper.net precisam existir para cada RID publicado.
- No macOS, binários sem assinatura e notarização são bloqueados pelo Gatekeeper; a distribuição para macOS precisa tratar isso.

## Estrutura planejada

```
VibePlayer.sln
src/
  VibePlayer.App/             Avalonia: Views, ViewModels, DI/bootstrap, configuração do Serilog
  VibePlayer.Core/            Domínio e interfaces (sem dependência de UI, VLC, Whisper, SDKs ou EF)
  VibePlayer.Infrastructure/  Player VLC, ffmpeg, Whisper, provedores de LLM, DbContext/migrations
tests/
  VibePlayer.UnitTests/
  VibePlayer.E2ETests/
specs/                          Specs do SDD (uma pasta por mudança)
docs/                           Documentação técnica versionada (arquitetura, decisões)
DESIGN.md                       Design system da UI (criado na primeira spec de layout)
```

`Core` não referencia `Infrastructure` nem `App`; tudo que toca bibliotecas externas fica atrás de interfaces em `Core` (`ITranscriptionService`, `ILlmProvider`, `ICourseLibrary` etc.).

## Comandos

```bash
dotnet build
dotnet run --project src/VibePlayer.App
dotnet test
dotnet test tests/VibePlayer.UnitTests
dotnet test --filter "FullyQualifiedName~NomeDaClasseOuTeste"
dotnet ef migrations add NomeDaMigration --project src/VibePlayer.Infrastructure --startup-project src/VibePlayer.App
```

## Testes

- **Unitários:** cobrem `Core` e ViewModels, com dependências externas substituídas por fakes. Não tocam disco, rede, VLC, Whisper nem LLM.
- **E2E:** sobem a UI real com `Avalonia.Headless`, SQLite real em arquivo temporário e uma biblioteca de cursos de exemplo gerada no próprio teste (vídeos curtos em `tests/fixtures`). O provedor de LLM é sempre um fake determinístico: nenhum teste chama Claude ou Ollama de verdade, nem localmente nem na CI.
- Toda spec lista em `tasks.md` quais testes unitários e E2E comprovam seus critérios de aceite.

## Comentários no código

Comentários explicam **o porquê**, não o quê. Não comente o óbvio (`// incrementa o contador`). Comente:

- decisões técnicas e restrições não evidentes, com o motivo — ex.: por que o áudio é convertido para WAV mono 16 kHz (é o formato em que o Whisper foi treinado; outras taxas são reamostradas internamente e só custam tempo);
- fluxos complexos (transcrição em segundo plano, cache, montagem de contexto do LLM) com um bloco maior no início explicando o desenho e as alternativas descartadas;
- workarounds, com o problema que resolvem.

Comentários, nomes de testes e mensagens de commit em português; identificadores de código (classes, métodos, variáveis) em inglês.

## Logs

- Arquivo em `<pasta de dados>/logs/vibePlayer-.log`, rotação diária, mantendo os últimos 14 arquivos.
- Logs estruturados (`Log.Information("Transcrição concluída para {LessonId} em {Elapsed}", ...)`), nunca interpolação de string.
- **Nunca** registrar chaves de API, tokens, conteúdo de transcrições, prompts ou respostas do LLM — só metadados (IDs, durações, tamanhos, contagem de tokens).

## Persistência

Tudo fica na pasta de dados do usuário, `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)/VibePlayer` (resolve para `%APPDATA%` no Windows e `~/.config` no Linux/macOS), nunca dentro da pasta do curso:

- `vibePlayer.db` — SQLite: cursos, aulas, progresso, transcrições (segmentos com timestamps), resumos, pontos principais, histórico de chat, configurações.
- `models/` — modelos do Whisper; `logs/` — Serilog.
- Chaves de API ficam no cofre do SO via `ISecretStore` (ver Multiplataforma), nunca no banco.
- **Sem criptografia do banco (decisão do mantenedor):** o SQLite fica em texto normal, protegido pelas permissões da pasta de dados do usuário. Não adicione criptografia de colunas, SQLCipher, chaves embutidas no código nem senha mestra sem uma spec aprovada.

## Fluxo de dados

1. **Biblioteca:** o usuário aponta uma pasta; cada subpasta é um curso/módulo e cada vídeo uma aula, em ordem natural de nome de arquivo (`2 - y.mp4` antes de `10 - z.mp4`).
2. **Transcrição:** em segundo plano, com progresso e cancelamento. Cache chaveado por hash do arquivo de vídeo (não pelo caminho), para não transcrever de novo se a pasta for movida. Legendas `.srt`/`.vtt` ao lado do vídeo podem substituir o Whisper.
3. **Chat:** a transcrição da aula atual (com timestamps) vai no início do contexto. Com Claude, transcrição + system prompt formam um prefixo estável com prompt caching.
4. **Resumo e pontos principais:** gerados uma vez por aula e salvos. Os pontos usam saída estruturada (JSON com `timestamp` + `texto`) para virarem links de seek no player.

## Provedores de LLM

- Interface única `ILlmProvider` com streaming (`IAsyncEnumerable<string>`); a UI não sabe qual provedor está em uso.
- **Claude — autenticação:** chave de API do usuário ou o perfil OAuth criado por `ant auth login` (o SDK o lê automaticamente). Não existe "login com conta Claude.ai" para apps de terceiros; não implemente OAuth próprio contra a Anthropic.
- Claude Opus 5.5: thinking não pode ser desativado (use `effort`, padrão `medium`); sem prefill de assistente; `tool_choice` forçado retorna 400 — use saída estruturada. Antes de escrever código do SDK, consulte a skill `claude-api` em vez de adivinhar nomes de tipos.
- Modelos locais têm contexto menor: o provedor Ollama divide transcrições longas em blocos.

## Documentação

- **Repositório (`docs/`, `specs/`, `DESIGN.md`):** documentação técnica para quem desenvolve — arquitetura, decisões, specs. Versionada junto com o código e revisada nos PRs.
- **Wiki do GitHub:** documentação para quem usa — instalação, configuração de provedores, uso, FAQ. Atualize a wiki quando uma spec implementada mudar algo visível ao usuário.
- `README.md`: apresentação curta, screenshot, link para Releases e para a wiki, e aviso de que o projeto não aceita contribuições.

## UI

Antes da primeira spec de layout, crie `DESIGN.md` com o design system: escala tipográfica, paleta (tema claro e escuro, contraste WCAG AA), espaçamentos em grade de 4/8 px, raios, tamanhos de componentes e estados (hover, foco, desabilitado). Toda tela nova segue o `DESIGN.md`; cores e tamanhos vêm de recursos/estilos Avalonia centralizados, nunca valores soltos nas Views.

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Visão geral

Player desktop **multiplataforma** (Windows, macOS e Linux) para cursos em vídeo armazenados localmente — cursos comprados que vêm só como arquivos de vídeo, sem plataforma. Ao lado do vídeo há um chat com LLM que conhece o conteúdo da aula atual e permite:

- fazer perguntas sobre a aula;
- gerar um resumo da aula;
- listar os principais pontos, cada um com timestamp clicável que leva o vídeo àquele momento.

O código-fonte fica em repositório público no GitHub, mas o projeto **não é open source** e **não aceita contribuições externas**: a licença é proprietária (todos os direitos reservados), com permissão apenas para baixar e usar os binários publicados em Releases. Não sugira nem adicione licenças open source, `CONTRIBUTING.md` convidando PRs ou templates de contribuição.

O esqueleto (solution, DI, logs, testes, CI), o layout (DESIGN.md), a persistência (SQLite, backups, preferências), a biblioteca, o player e a transcrição existem; o LLM ainda é **planejado**. Ao criar ou mudar algo estrutural, atualize este arquivo.

## Fluxo de trabalho (SDD — obrigatório)

O desenvolvimento é **Spec-Driven**: nenhuma mudança de código começa sem uma spec aprovada pelo mantenedor.

1. Para cada mudança, crie a branch `feature/NNNN-nome-curto` (ou `bugfix/`) a partir de `develop` e a pasta `specs/NNNN-nome-curto/` a partir de `specs/_template/` (`spec.md`, `design.md`, `tasks.md`), com status **Rascunho**.
2. **Pare e peça aprovação.** Não escreva código de produção nem testes até o mantenedor aprovar ou pedir ajustes. Ajustes pedidos → revise a spec e peça aprovação de novo.
3. Aprovada → status **Aprovada**, implemente seguindo `tasks.md`, marcando cada tarefa ao concluir. Os testes descritos na spec fazem parte da entrega.
4. Se durante a implementação surgir algo que muda o que foi aprovado (escopo, decisão técnica, dependência nova), pare e atualize a spec para nova aprovação em vez de decidir sozinho.
5. Concluída → status **Implementada**; atualize a documentação afetada (este arquivo, `docs/`, wiki, `DESIGN.md`).

Detalhes em [specs/README.md](specs/README.md).

### Git e GitHub (GitFlow)

| Branch | Sai de | Entra em | Merge |
|---|---|---|---|
| `feature/NNNN-nome` (uma por spec) | `develop` | `develop` por PR | squash |
| `bugfix/NNNN-nome` (spec de correção) | `develop` | `develop` por PR | squash |
| `release/X.Y.Z` | `develop` | `main` por PR | merge commit |
| `hotfix/X.Y.Z` | `main` | `main` por PR | merge commit |

- `develop` é a branch padrão. `main` só recebe releases e hotfixes; cada merge em `main` é uma versão publicada.
- `main` e `develop` são protegidas: só entram por PR com a CI verde nos três SOs (rulesets do repositório). Nunca faça push direto nelas.
- **Releases e hotfixes não são criados à mão.** O mantenedor roda as pipelines *Iniciar release* / *Iniciar hotfix* (Actions → workflow → Run workflow) com a versão. O merge do PR em `main` é sempre do mantenedor; ao mesclar, *Publicar release* cria a tag `vX.Y.Z`, publica os pacotes no GitHub Releases e abre o PR de volta para `develop` (mescla sozinho se não houver conflito).
- **Versão:** calculada pelo MinVer a partir das tags `vX.Y.Z`. Nunca edite número de versão em arquivo nem crie tags manualmente.
- As pipelines usam o secret `RELEASE_TOKEN` (fine-grained PAT do mantenedor, só deste repositório, com Contents, Pull requests e Workflows em leitura e escrita), porque PRs abertos com o `GITHUB_TOKEN` não disparam a CI. Quando vencer, as pipelines de release falham com aviso explícito.
- Commits no padrão Conventional Commits, em português: `feat(chat): adiciona streaming de respostas`. O título do PR segue o mesmo padrão, porque vira o commit do squash e as notas da release.
- Pacotes publicados: self-contained, sem assinatura, um por RID (`win-x64`, `osx-arm64`, `osx-x64`, `linux-x64`), cada um com `LICENSE` e `THIRD-PARTY-NOTICES.md`.

## Stack

- **.NET / C#** com **Avalonia UI**, padrão MVVM com `CommunityToolkit.Mvvm`.
- **Vídeo:** LibVLCSharp (sem `LibVLCSharp.Avalonia`: o VLC decodifica e o app desenha os quadros; ver "Reprodução").
- **Transcrição:** Whisper local via `Whisper.net`; o próprio VLC extrai o áudio (sem ffmpeg). Modelos do Whisper são baixados no primeiro uso, não vão no repositório nem no instalador.
- **LLM:** provedores configuráveis — Claude via SDK oficial (`Anthropic`, modelo padrão `claude-opus-5-5`, streaming no chat) e Ollama local (`http://localhost:11434`).
- **Persistência:** SQLite com EF Core (`Microsoft.EntityFrameworkCore.Sqlite`). Migrations aplicadas automaticamente na inicialização, pois o schema evolui entre versões instaladas nas máquinas dos usuários.
- **Validação:** FluentValidation. Validadores ficam em `Core` (regras de domínio e de configuração, ex.: URL do Ollama, chave de API, pasta da biblioteca) e os ViewModels os usam para expor erros à UI via `INotifyDataErrorInfo`. Não use DataAnnotations para regras de validação.
- **Logs:** Serilog, apenas `Serilog.Sinks.File`.
- **Testes:** xUnit, NSubstitute (fakes/mocks), Shouldly (asserções), `Avalonia.Headless.XUnit` (E2E de UI).

> Licenças de terceiros: o app é proprietário, então toda dependência precisa ter licença que permita uso em software fechado (MIT, Apache 2.0, BSD, LGPL com linkagem dinâmica). LibVLC é LGPL: distribua-a como biblioteca separada e inclua os avisos e textos das licenças em `THIRD-PARTY-NOTICES.md` e no pacote de release. Nunca adicione dependências GPL/AGPL. FluentAssertions 8+ tem licença comercial — não use. Toda spec que adiciona dependência registra a licença no `design.md`.

## Multiplataforma

- Nunca use APIs exclusivas de um sistema operacional diretamente em `Core` ou nos ViewModels; quando inevitável, isole atrás de interface em `Core` com implementação por SO em `Infrastructure`.
- Caminhos sempre com `Path.Combine` e `Environment.GetFolderPath`, nunca separadores ou pastas fixas.
- **Segredos (chaves de API):** interface `ISecretStore` com implementação no cofre nativo de cada SO — Windows: DPAPI/Credential Manager; macOS: Keychain; Linux: libsecret (Secret Service). Se o cofre não estiver disponível (Linux sem keyring), avise o usuário e não grave a chave em texto puro.
- **Binários nativos por SO:** LibVLC (no Linux depende do `libvlc` instalado no sistema) e runtimes do Whisper.net precisam existir para cada RID publicado. `WhisperRuntimes.targets` deixa no pacote só os nativos do RID.
- No macOS, binários sem assinatura e notarização são bloqueados pelo Gatekeeper; a distribuição para macOS precisa tratar isso.

## Estrutura

```
VibePlayer.slnx
Directory.Build.props         net10.0, Nullable, warnings como erro, metadados do produto
Directory.Packages.props      Versão de TODOS os pacotes NuGet (os .csproj não declaram versão)
global.json                   SDK do .NET 10
src/
  VibePlayer.App/             Avalonia: Views, ViewModels, Program (bootstrap), AppComposition (composition root), Serilog
  VibePlayer.Core/            Domínio e interfaces (sem dependência de UI, VLC, Whisper, SDKs ou EF)
  VibePlayer.Infrastructure/  Player VLC, transcrição (fila, processo separado, Whisper), provedores de LLM, DbContext/migrations
tests/
  VibePlayer.UnitTests/
  VibePlayer.E2ETests/
  VibePlayer.WorkerStub/      Processo falso que morre e trava, para testar o processo separado
specs/                          Specs do SDD (uma pasta por mudança)
docs/                           Documentação técnica versionada (arquitetura, decisões)
DESIGN.md                       Design system da UI (tokens em src/VibePlayer.App/Styles)
```

`Core` não referencia `Infrastructure` nem `App`; tudo que toca bibliotecas externas fica atrás de interfaces em `Core` (`ITranscriptionService`, `ILlmProvider`, `ICourseLibrary` etc.). Isso é verificado por `ArchitectureTests` nos testes unitários.

Registro de serviços: implementações de `Infrastructure` entram por `AddInfrastructure()`; ViewModels e serviços de UI em `AppComposition.BuildServices`. Classes recebem `ILogger<T>` (Microsoft.Extensions.Logging), nunca o Serilog diretamente. A classe da aplicação Avalonia se chama `VibePlayerApp` (e não `App`) para não colidir com o namespace `VibePlayer.App`.

## Exceções

Uma exceção não pode derrubar o app. `GlobalExceptionHandler` registra e mantém o app aberto para exceções na thread de UI e em tasks não observadas; o .NET **não permite** recuperar exceções em threads próprias ou `async void` fora da UI. Por isso:

- `async void` só em handlers de evento da UI; todo o resto retorna `Task`.
- Nada de `new Thread`, `System.Threading.Timer` ou `Task.Run` solto: trabalho em segundo plano usa `task.FireAndForget(logger, "NomeDaOperacao")` (`VibePlayer.Core.Threading`) ou é aguardado.
- Comandos tratam seus erros esperados; o handler global é rede de segurança para bugs, não fluxo normal.

## Comandos

```bash
dotnet build VibePlayer.slnx
dotnet run --project src/VibePlayer.App
dotnet test VibePlayer.slnx
dotnet test tests/VibePlayer.UnitTests
dotnet test tests/VibePlayer.E2ETests
dotnet test --filter "FullyQualifiedName~NomeDaClasseOuTeste"
dotnet tool restore                      # instala o dotnet-ef na versão fixada em dotnet-tools.json
dotnet ef migrations add NomeDaMigration --project src/VibePlayer.Infrastructure --startup-project src/VibePlayer.Infrastructure --output-dir Persistence/Migrations
```

## Testes

- **Unitários:** cobrem `Core` e ViewModels, com dependências externas substituídas por fakes. Não tocam disco, rede, VLC, Whisper nem LLM.
- **E2E:** sobem a UI real com `Avalonia.Headless` (via `TestAppBuilder`, que usa o mesmo `AppComposition` de produção e grava logs no `InMemorySink` para os testes verificarem). O Avalonia 12 recria o app e o dispatcher **a cada teste**; não guarde estado de UI em campos estáticos. Nas specs futuras: SQLite real em arquivo temporário e uma biblioteca de cursos de exemplo gerada no próprio teste (vídeos curtos em `tests/fixtures`). O provedor de LLM é sempre um fake determinístico: nenhum teste chama Claude ou Ollama de verdade, nem localmente nem na CI.
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

## Reprodução

- `PlayerViewModel` só conhece `IMediaPlayer` (Core). `VlcMediaPlayer` (Infrastructure) é a implementação; os E2E usam `FakeMediaPlayer`, e `VlcIntegrationTests` cobre o VLC real sem janela, com as amostras de `tests/fixtures/videos`.
- **Clique e tela cheia (spec 0008):** clique simples no `VideoArea` alterna pausa só depois do intervalo de duplo clique da plataforma; o duplo clique cancela a pausa e alterna a tela cheia. A barra de controles **não pode ter `Grid.Row` local** em `PlayerPanel.axaml`: valor local vence o estilo `fullscreen` (`Controls.axaml`) que a põe sobre o vídeo, e a linha reservada deixava uma faixa vazia quando a barra sumia.
- **Desenho:** `VlcFrameBuffer` recebe os quadros por callbacks (RV32, dois buffers, limite de 1920 px) e `VideoView` os copia para um `WriteableBitmap`. O vídeo é um controle comum: pode ter controles por cima, recebe mouse e teclado e funciona no headless. Não use `NativeControlHost`: sobre ele o app não recebe mouse, e o VLC 3 não troca de janela durante a reprodução.
- **Regra de threads do VLC (derruba o processo se quebrada):** nunca chame o LibVLC de dentro de um evento ou callback dele. Isso inclui o código depois de `await media.Parse(...)`, que continua na thread do VLC: saia dela com `await Task.Yield()` antes de liberar a mídia. Eventos só atualizam estado e avisam; quem reage passa pela thread de UI ou pelo pool.
- **Depois do `Dispose`:** tarefa agendada que chame o `MediaPlayer` passa pelo `_releaseLock` e confere se ele ainda existe. Usar o player nativo já liberado é violação de acesso, que nenhum `catch` pega.
- **Uma instância de `LibVLC` por processo**, nunca liberada (`VlcRuntime`). Carregada no primeiro uso, não na abertura do app.
- **Plugins por lista de permissão** em `src/VibePlayer.App/VlcPlugins.targets`, com cópia em `.github/scripts/vlc-allowed-plugins.txt` que a CI usa para conferir o build e os pacotes (`VlcPluginListTests` falha se as duas divergirem). Plugin novo: confira a licença antes (GPL é proibido).
- **Linux:** usa o VLC instalado no sistema; sem ele, o player mostra como instalar e o resto do app funciona.
- **Progresso:** `ILessonProgressStore` (tabela `LessonProgress`) e regras puras em `PlaybackRules`. Limiares: retoma entre 10 s e 95%; conclui aos 95% ou no fim.
- **Miniaturas:** `ThumbnailService` (cache em `thumbnails/`, nome com tamanho e data do vídeo, fila com prioridade, uma por vez).

## Transcrição

- **Processo separado:** extração e Whisper rodam no mesmo executável com `--transcription-worker` (`WorkerEntryPoint`), sem Serilog, banco ou Avalonia, em prioridade baixa. O .NET não recupera falha nativa: em processo único, um `abort` do whisper.cpp fecharia o app. Protocolo em `WorkerProtocol`: job em JSON no stdin, eventos em JSON por linha no stdout. Stdin fechado significa que o app morreu, e o worker sai.
- **Extração:** `VlcAudioExtractor` usa `transcode` + `smem`, com instância própria do LibVLC no worker. Não troque por `amem` (1x, ou perde áudio) nem por WAV (perde o PTS). `--audio-resampler=speex_resampler` é obrigatório: sem ele o VLC usa o `ugly` e perde 1,17 s a cada 20 min. A qualidade fica em 0: em 44,1 kHz, a padrão extrai só a 16x, contra 100x. Testes com arquivos de tom em tempo conhecido (`tests/fixtures/audio`) pegam deriva e buracos.
- **Limite conhecido:** áudio que começa depois do vídeo, sem preenchimento no arquivo, sai adiantado nessa medida, porque o PTS do VLC é relativo.
- **Dados:** `Transcripts` e `TranscriptSegments` são por hash do arquivo de origem; `LessonTranscriptions` liga a `LessonEntity.Id`. Segmentos entram no banco só no fim, numa transação; uma aula interrompida recomeça.
- **Modelos:** `WhisperModelStore` baixa de um commit fixo do Hugging Face, retoma com `Range` e confere o SHA-256 antes de usar. É a única conexão de rede da transcrição.
- **GPU:** ordem `[Vulkan, Cpu, CpuNoAvx]`; se o worker morrer na GPU, o app repete na CPU e grava `transcription.forceCpu`.
- **Testes:** `WhisperIntegrationTests` roda o executável real; o modelo vem de `VIBEPLAYER_WHISPER_MODEL` (a CI baixa o tiny, localmente o teste é pulado sem a variável). `VlcIntegrationTests` e `AudioExtractionIntegrationTests` ficam na mesma coleção: duas instâncias do LibVLC subindo juntas no processo de testes travam a reprodução.

## Persistência

Tudo fica na pasta de dados do usuário, `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)/VibePlayer` (resolve para `%APPDATA%` no Windows e `~/.config` no Linux/macOS), nunca dentro da pasta do curso:

- `vibePlayer.db` — SQLite: cursos, aulas, progresso, transcrições (segmentos com timestamps), resumos, pontos principais, histórico de chat, configurações.
- `backups/` — backups do banco: um diário na primeira abertura do dia (mantém 7) e um antes de cada atualização do banco (mantém 3), sempre verificados com `integrity_check`. Restaurar é manual por enquanto (fechar o app e copiar o backup sobre `vibePlayer.db`).
- `models/` — modelos do Whisper (`.part` durante o download); `logs/` — Serilog; `thumbnails/` — miniaturas das aulas (cache regenerável, fora dos backups).
- **Banco:** `VibePlayerDbContext` (Infrastructure), sempre via `IDbContextFactory` com contextos curtos e acesso `async`; WAL ligado. Cada spec cria suas tabelas com uma migration própria; migrations publicadas nunca são editadas. `MigrationTests` falha se o modelo mudar sem migration.
- **Inicialização:** `AppStartup.Prepare` (usado pelo `Program` e pelos E2E) roda o `DatabaseInitializer` antes da UI: backup → migrate → em falha, restaura o backup, mostra `DatabaseErrorWindow` e sai com código 2. Nunca abra a janela principal sobre um banco que não ficou pronto.
- **Preferências:** `ISettingsStore` (Core) com chaves `SettingKey<T>` (nome permanente com prefixo por área, padrão, validação), gravadas como JSON na tabela `Settings`. Valor inválido volta ao padrão com aviso no log (só a chave). Layout da janela: `UiSettings` + `WindowStateTracker` (grava 500 ms depois da última mudança e ao fechar).
- **Pasta de dados alternativa:** a variável de ambiente `VIBEPLAYER_DATA_DIR` troca a pasta de dados (para testar o app de verdade sem tocar na biblioteca real).
- Chaves de API ficam no cofre do SO via `ISecretStore` (ver Multiplataforma), nunca no banco.
- **Sem criptografia do banco (decisão do mantenedor):** o SQLite fica em texto normal, protegido pelas permissões da pasta de dados do usuário. Não adicione criptografia de colunas, SQLCipher, chaves embutidas no código nem senha mestra sem uma spec aprovada.

## Fluxo de dados

1. **Biblioteca** (spec 0005):
   - **Entrada:** o usuário adiciona uma pasta, e o app sempre pergunta "Um curso" ou "Vários cursos", com prévia. Também pode adicionar vídeos avulsos, que viram um curso novo ou vão para "Vídeos avulsos".
   - **Estrutura:** curso → módulos (subpastas; aninhadas viram "Módulo 1 / Parte A") → aulas (vídeos), em ordem natural (`NaturalStringComparer`: `2 - y.mp4` antes de `10 - z.mp4`).
   - **Leitura das pastas:** `LibraryScanner` e a detecção ficam no `Core`, sobre `IFileSystemReader`, e são testados com `FakeFileSystem`. `CourseLibrary` (Infrastructure) sincroniza com o banco pelo **caminho relativo** (com "/", NFC e caixa conforme o SO; ver `LibraryPaths`).
   - **Identidade:** aula que some fica `IsAvailable = false` com o **mesmo Id**. Dados futuros (progresso, transcrição, chat) devem apontar para `LessonEntity.Id`. Nunca apague aulas na sincronização.
   - **Atualização:** ao abrir o app e pelo botão "Atualizar", sem `FileSystemWatcher`. "Localizar pasta" reconecta uma raiz movida.
2. **Transcrição** (spec 0007): só a pedido do usuário, em segundo plano, com fila, progresso e cancelamento. Cache chaveado pelo SHA-256 do arquivo de vídeo (não pelo caminho), para não transcrever de novo se a pasta for movida. Legendas `.srt`/`.vtt` ao lado do vídeo substituem o Whisper automaticamente, salvo se o usuário escolher "Transcrever com o Whisper".
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

Toda tela segue o [`DESIGN.md`](DESIGN.md) (paleta clara/escura, tipografia, espaçamento, componentes, estados, acessibilidade). Layout: player à esquerda; à direita, painel lateral recolhível com as abas *Aulas*, *Transcrição*, *Chat*, *Resumo* e *Pontos principais*, mostradas só como ícones (com dica e nome acessível; o nome da ativa aparece acima). A janela abre com 1100 × 700. O ícone do app está em `src/VibePlayer.App/Assets` (`.ico`, `.png`, `.icns`, gerados por `gerar-icones.py` a partir de `icon.svg`).

- **Tokens** em `src/VibePlayer.App/Styles/Tokens.axaml` (cores em `ThemeDictionaries` Light/Dark, sempre via `DynamicResource`; tamanhos, espaçamentos e raios via `StaticResource`) e **estilos por classe** em `Styles/Controls.axaml` (`TextBlock.title`, `Button.primary`, `Button.icon`, `StackPanel.emptyState`...). Views nunca têm cor, tamanho de fonte ou espaçamento literal — `ViewConventionsTests` falha se tiverem. Cor nova: nos dois temas e com par de contraste em `DESIGN.md` e `DesignTokenTests.Pairs`.
- **Textos** em `src/VibePlayer.App/Resources/Strings.resx` (pt-BR; classe `Strings` gerada no build), usados com `{x:Static res:Strings.Area_Elemento}`. Nenhuma string visível solta em View ou ViewModel.
- **Views** em `Views/<Área>/` (`Shell`, `Player`, `Library`, `Assistant`), uma `UserControl` por região, com `x:DataType` do ViewModel. Ícones: `FluentIcons.Avalonia` (`<ic:SymbolIcon Symbol="..." />`).
- **Acessibilidade:** controle sem texto visível leva `AutomationProperties.Name` (`AccessibilityTests` verifica). Atalhos usam `PlatformSettings.HotkeyConfiguration.CommandModifiers` (Ctrl, ou ⌘ no macOS), nunca Ctrl fixo.
- Função ainda não implementada: controle desabilitado com dica "Disponível em breve", não um botão que não faz nada.
- Avisos ao usuário passam por `INotificationService` (canto inferior direito); o `GlobalExceptionHandler` já mostra um aviso de erro genérico, limitado a um a cada 5 s.

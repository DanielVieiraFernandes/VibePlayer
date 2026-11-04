# 0007 — Design técnico

## Abordagem

```
App (processo principal)                     Worker (mesmo executável, --transcription-worker)
────────────────────────                     ──────────────────────────────────────────────
TranscriptionViewModel                       stdin: um job em JSON
  │                                          stdout: eventos em JSON, um por linha
ITranscriptionService (fila, cache)
  ├─ ITranscriptStore (SQLite)               VlcAudioExtractor
  ├─ ISubtitleSource (.srt/.vtt)               └─ VLC: transcode + smem → PCM 16 kHz mono com PTS
  ├─ IFileHasher (SHA-256)                   WhisperTranscriber
  ├─ IWhisperModelStore (download)             └─ Whisper.net: GPU ou CPU → segmentos
  └─ ITranscriptionWorker ──── pipes ──────▶
```

```
Core/Transcription/
  ITranscriptionService     Fila, estado por aula, eventos de progresso
  ITranscriptStore          Lê e grava transcrições e o vínculo com a aula
  ITranscriptionWorker      Roda um job e devolve eventos (fake nos testes)
  IWhisperModelStore        Modelo presente? Baixar, com progresso
  ISubtitleSource           Procura a legenda ao lado do vídeo
  IFileHasher               SHA-256 de um arquivo, assíncrono
  SubtitleParser            SRT e VTT para segmentos (puro)
  SubtitleEncoding          Decide a codificação do texto (puro)
  TranscriptSegmentFilter   Remove marcações inventadas e segmentos vazios (puro)
  TranscriptionModels       Modelos disponíveis, com nome, tamanho e SHA-256
  WorkerProtocol            Tipos do job e dos eventos (puro)
Infrastructure/Transcription/
  TranscriptionService, TranscriptStore, WhisperModelStore, PhysicalSubtitleSource, Sha256FileHasher
  ProcessTranscriptionWorker   Abre o processo, lê os eventos, cancela
  Worker/WorkerEntryPoint      Entrada do worker
  Worker/VlcAudioExtractor     Extração com VLC
  Worker/PtsGapFiller          Preenche com silêncio pelo PTS (puro)
  Worker/WhisperTranscriber    Whisper.net
App/
  ViewModels/TranscriptionViewModel
  Views/Assistant/TranscriptionView
```

`Program.Main` verifica `--transcription-worker` na primeira linha e entra em `WorkerEntryPoint`, sem Serilog, banco ou Avalonia.

### Extração de áudio

**Decisão recomendada (Q1):** a cadeia de saída do VLC 3, com estas opções:

```
Instância:  --no-video --no-sub-autodetect-file --audio-resampler=speex_resampler
Mídia:      :no-video :sout-keep
            :sout=#transcode{vcodec=none,scodec=none,acodec=s16l,channels=1,samplerate=16000}
                  :smem{audio-prerender-callback=<ptr>,audio-postrender-callback=<ptr>,no-time-sync}
```

Cada detalhe dessa cadeia veio de uma falha real no protótipo:

- **`amem` não serve.** Ele respeita o relógio de reprodução: fica em 1x. Com `rate` 2 ou 4, o VLC comprime o áudio e entrega metade ou um quarto das amostras. Com `rate` 8, não entrega nada.
- **`--audio-resampler=speex_resampler` é obrigatório.** Sem ele, o VLC escolhe o módulo `ugly`, que descarta o resto de cada bloco. Em AAC a 48 kHz, cada bloco de 1024 amostras vira 341 em vez de 341,33, e o áudio perde 1,17 s a cada 20 min. Timestamps saem errados e crescem com o tempo. Como opção de mídia, ele é ignorado: precisa ser opção da instância.
- **`smem`, e não WAV em arquivo.** O WAV não guarda timestamps: um buraco de 30 s no áudio deixa tudo depois dele 30 s adiantado. O `smem` entrega o PTS de cada bloco, e o worker preenche com silêncio quando a posição calculada passa 100 ms da gravada.
- **`no-time-sync`.** Sem isso, o `smem` espera o tempo real. `time-sync=false` não funciona: a sintaxe é a de booleano da cadeia.
- **PTS relativo.** O PTS vem em base de relógio do sistema, então a conta usa a diferença para o primeiro bloco. É por isso que um atraso no começo do áudio não pode ser recuperado (ver Riscos).
- **`:no-sout-all`.** Sem isso, um arquivo com duas faixas de áudio entrega as duas no mesmo callback, emendadas: 150 s viraram 300 s no teste. Vale a primeira faixa.
- **Callbacks.** São funções `UnmanagedCallersOnly` e não chamam o LibVLC. Só copiam para o buffer. O endereço vai como número na opção, que é a interface do `smem`.

O áudio fica em memória como `float[]` e vai para `WhisperProcessor.ProcessAsync(ReadOnlyMemory<float>)`, sem arquivo temporário nem cópia do áudio em disco.

**Medições** (Windows, VLC 3.0.24, só com a lista de permissão mais os dois plugins, salvo indicação):

| Caminho | Velocidade | Resultado |
|---|---|---|
| `amem`, `rate` 1 | 1x | Correto, mas 1 h de aula leva 1 h |
| `amem`, `rate` 2 / 4 / 8 | 2x / 4x / 8x | Áudio perdido: metade, um quarto, nada |
| `transcode` + WAV, padrão (`ugly`) | cerca de 1000x | **Deriva:** −1,17 s em 20 min |
| `transcode` + WAV, `speex_resampler` | cerca de 550x | Sem deriva, mas perde buracos de PTS |
| **`transcode` + `smem`, `speex_resampler`** | **cerca de 620x** | **1200,000 s exatos; buraco de 30 s preservado** |
| ffmpeg do sistema (GPL, só como referência) | cerca de 1400x | Referência da comparação |

Comparação com o ffmpeg, por correlação de janelas de 5 s (vídeo de 20 min, H.264/AAC, 1080p, 468 MB):

| Arquivo | Duração | Offset constante | Correlação |
|---|---|---|---|
| AAC 48 kHz (20 min) | 1200,000 s | −2 ms | 1,000 |
| AAC 44,1 kHz | 150,001 s | −2 ms | 1,000 |
| E-AC-3 5.1 | 149,984 s | −7 ms | 0,996 |
| AC-3 5.1 | 149,984 s | −7 ms | 0,996 |
| FLAC em MKV | 149,856 s | −2 ms | 1,000 |
| Vorbis em MKV | 150,012 s | −2 ms | 0,998 |
| MP3 em MP4 | 150,024 s | −25 ms | 0,996 |
| WMA em WMV | 149,888 s | +41 ms | 0,996 |
| PCM em MOV | 149,981 s | −2 ms | 1,000 |
| MP2 em TS | 150,000 s | −12 ms | 0,996 |
| 3 horas (AAC) | 10800,000 s, 423x | — | — |

Repetido com o caminho final (`smem`), pelo próprio `VlcAudioExtractor` (T5):

| Arquivo | Duração | Tempo |
|---|---|---|
| AAC 44,1 kHz | 150,001 s | — |
| E-AC-3 5.1 / AC-3 5.1 | 149,984 s | 0,44 s |
| FLAC em MKV | 149,856 s | 0,33 s |
| Vorbis em MKV | 150,012 s | 0,44 s |
| MP3 em MP4 | 149,976 s | 0,35 s |
| WMA em WMV | 149,888 s | 0,30 s |
| PCM em MOV | 149,981 s | 0,26 s |
| MP2 em TS | 149,952 s | 0,34 s |
| Buraco de 30 s | 150,001 s | 0,41 s |
| Duas faixas de áudio | 150,037 s | 0,50 s |
| 20 min de vídeo 1080p | 1200,000 s | 2,10 s |
| 3 horas (AAC) | 10800,107 s | 16,7 s |

A primeira abertura depois de instalar leva mais (9 a 17 s no teste), provavelmente pela verificação do antivírus nos plugins novos; as seguintes abrem em menos de 0,5 s.

Casos de borda com `smem`:

| Caso | Resultado |
|---|---|
| Sem faixa de áudio | 0 blocos: vira "Sem áudio" |
| Arquivo corrompido | 0 blocos, em 0,04 s, sem crash: vira "Falhou" |
| Sem `speex_resampler` (plugin removido) | Cai no `ugly`; a correção por PTS limita o erro a 113 ms, mas o áudio piora muito (correlação de 0,2 a 0,3) |
| Fixtures (MP4, MKV, AVI, WebM) | 0,13 a 0,30 s cada, já contando abrir o VLC |

Os dois primeiros casos terminam igual, com 0 blocos, e o VLC **não** dispara `EncounteredError` no arquivo corrompido. A distinção vem das faixas lidas por `Media.Parse`: nenhuma faixa significa arquivo ilegível; faixa de vídeo sem áudio significa "Sem áudio".

### Alternativa: ffmpeg LGPL

Levantado em fonte primária (README, `variants/` e releases da BtbN; configuração publicada de cada build):

| RID | Build | Licença | Tamanho | Situação |
|---|---|---|---|---|
| `win-x64` | BtbN `win64-lgpl-shared` (n8.1 / n9.0) | LGPL v3 (`--enable-version3`) | 73 a 76 MB (zip) | Serve |
| `linux-x64` | BtbN `linux64-lgpl-shared` (n8.1 / n9.0) | LGPL v3 | 60 a 62 MB (tar.xz) | Serve (glibc 2.28 ou maior) |
| `osx-arm64` | `cutdone-ffmpeg` 9.0.2 | LGPL 2.1+, com fontes e material de religação | Não medido | Projeto de terceiro, sem histórico; binário não verificado |
| `osx-x64` | Nenhum encontrado | — | — | Só compilando |

Descartados no macOS: Martin Riedl (`--enable-gpl --enable-version3`, com x264 e x265) e evermeet.cx (GPL, sem Apple Silicon). A BtbN não publica macOS. O `ffmpeg-static` do npm é GPL.

Sem VLC, a opção real seria compilar um ffmpeg mínimo em CI para os quatro RIDs, o que dá um job novo para manter e obrigações de fonte. Não estimei o tamanho nem o tempo dessa compilação. O ganho seria tratar o atraso inicial do áudio com `aresample=async=1:first_pts=0`, caso raro. Por isso a recomendação é o VLC.

`IAudioExtractor`, interno ao worker, deixa a troca possível no futuro, se uma atualização do VLC quebrar o `smem`.

### Processo separado

**Por que separar:** o .NET não recupera falhas nativas. `GlobalExceptionHandler` não pega um acesso inválido ou um `abort` no whisper.cpp, um `SIGILL` em CPU sem a instrução esperada, uma falha de driver de GPU ou um arquivo que quebra o VLC. Qualquer um desses, em processo único, fecharia o app no meio de uma aula.

**O que se mediu:**
- Modelo truncado ou com lixo: `WhisperModelLoadException` gerenciável, sem crash. Esse caso se resolveria sem processo separado, mas os demais acima não.
- **Custo de abrir o processo:** abrir o VLC e extrair 3 s leva de 0,13 a 0,30 s, e carregar o modelo, de 0,2 a 0,9 s. Isso é desprezível perto de minutos de transcrição.

**Ganhos que vão além da segurança:**
- **Memória:** o pico de 750 MB a 2 GB some quando o processo termina, sem depender do GC ou de liberações nativas.
- **Cancelamento:** matar o processo é imediato. No processo principal, o cancelamento depende de o whisper.cpp chegar a um ponto de verificação.
- **Prioridade:** `Process.PriorityClass = BelowNormal` vale para tudo, incluindo as threads nativas.
- **Regra de threads do VLC:** o worker tem o próprio `LibVLC`, com opções próprias (o `--audio-resampler` não atinge o player) e sem disputar a instância única do `VlcRuntime`.

**Como funciona:**
- O app abre `Environment.ProcessPath --transcription-worker` com stdin, stdout e stderr redirecionados, e escreve um job em JSON no stdin.
- O worker responde com eventos em JSON, um por linha, no stdout: `stage`, `progress`, `segment`, `language`, `noAudio`, `done`, `error` (com código, sem texto de usuário).
- **Cancelar:** `Process.Kill(entireProcessTree: true)`.
- **Se o app morrer:** o stdin fecha, o worker percebe o fim do fluxo e sai. Não sobra processo órfão.
- **Fim sem `done`:** conta como falha, e o log registra só o código de saída. O stderr é descartado, porque o conteúdo não é controlado.
- **Logs:** o worker não grava arquivo. O app registra os metadados que chegam pelos eventos.

**Alternativa descartada:** segundo executável. Exigiria outro apphost, `deps.json` e mudanças nos pacotes e no futuro instalador, sem ganho.

### Whisper

- **Pacotes:** `Whisper.net` e `Whisper.net.Runtime` (CPU, e Metal em `macos-arm64`), `Whisper.net.Runtime.Vulkan` (`win-x64`, `linux-x64`) e `Whisper.net.Runtime.NoAvx` (CPU antiga).
- **Ordem de runtime:** `[Vulkan, Cpu, NoAvx]`. O carregador do Whisper.net verifica compatibilidade e passa para o seguinte.
- **Falha com GPU:** se o worker morrer usando Vulkan ou Metal, o app repete a aula uma vez só com CPU e grava `transcription.forceCpu = true`. Driver ruim não pode virar falha em toda aula. Em `macos-arm64`, o Metal vem no mesmo runtime da CPU, por isso a repetição também passa `UseGpu = false`.
- **Threads:** `Math.Clamp(ProcessorCount / 2, 1, 8)`.
- **Idioma:** preferência `transcription.language`, padrão `auto`, com seletor na aba (Automático, Português, Inglês, Espanhol). O idioma detectado acertou no teste, com cerca de 10% a mais de tempo.
- **Silêncio:** no teste de 60 s de silêncio, o modelo inventou `[MÚSICA]`. `TranscriptSegmentFilter` remove segmentos que são só uma marcação entre colchetes ou parênteses, e segmentos vazios.
- **Precisão dos tempos:** por segmento, cerca de 0,3 s no início da fala depois do silêncio. Timestamp por palavra não entra: é mais lento e os links só precisam da frase.
- **Publicação:** o `Whisper.net.Runtime` copia todas as plataformas desktop para a saída (18 MB). O pacote de cada RID deve levar só as suas.

**Medições** (Ryzen 7 5700G, 8 núcleos, 16 GB, Radeon integrada; 180 s de fala pt-BR, 8 threads):

| Modelo | Download | CPU: velocidade | CPU: 1 h de aula | Pico RAM | Vulkan: velocidade | WER* |
|---|---|---|---|---|---|---|
| `base` | 148 MB | 20,9x | 3 min | 485 MB | — | 1,7% |
| **`small-q5_1`** | **190 MB** | **7,8x** | **8 min** | **641 MB** | **11,7x** | **2,5%** |
| `small` | 488 MB | 7,5x | 8 min | 925 MB | — | 2,5% |
| `medium-q5_0` | 539 MB | 2,9x | 21 min | 1163 MB | — | 2,8% |
| `large-v3-turbo-q5_0` | 574 MB | 1,6x | 38 min | 1117 MB | 4,7x | 2,2% |

\* **A WER aqui não decide nada.** A fala foi gerada por voz sintética, limpa e sem sotaque, e a estimativa tem ruído: a diferença entre modelos fica dentro do erro. Em aula real, a diferença entre modelos deve ser maior. É preciso testar com uma aula real antes de fechar o padrão (Q3).

**Outras medições:**
- **CPU consumida:** `small-q5_1` gastou 183 s de CPU no processador e **7 s** com Vulkan, para os mesmos 180 s de áudio. O texto saiu com a mesma WER.
- **Threads:** 4 threads, 5,3x; 8 threads, 7,8x.
- **Memória e duração:** cerca de 346 MB por hora de áudio, além do pico da tabela (áudio em float mais mel). Uma aula de 3 h com `small-q5_1` dá cerca de 1,7 GB.
- **Hash:** SHA-256 a cerca de 630 MB/s na CPU; 2 GB levam cerca de 3 s em SSD, e o disco limita o resto.

**Limites destes números:** só uma máquina, só Windows. Linux e macOS não foram medidos, e o Vulkan foi medido só em GPU integrada AMD.

### Modelos

- `<dados>/models/`, baixados de `https://huggingface.co/ggerganov/whisper.cpp/resolve/<commit>/<arquivo>`, com o commit fixado: `5359861c739e955e79d9a303bcbc70fb988958b1`.
- `TranscriptionModels` traz o SHA-256 de cada modelo. Hashes atuais do Hugging Face:
  - `ggml-small-q5_1.bin`: `ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb` (190.085.487 bytes)
  - `ggml-large-v3-turbo-q5_0.bin`: `394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2` (574.041.195 bytes)
- **Download próprio** sobre `HttpClient`, e não `WhisperGgmlDownloader`: é preciso ter retomada, progresso e verificação de hash.
- Baixa para `<arquivo>.part`, retoma com `Range`, confere o SHA-256 e só então renomeia. Se o espaço livre for insuficiente, avisa antes de começar.
- Sem autenticação e sem dados do usuário na requisição.

### Fila e cache

```
Pedido para a aula
  ├─ legenda ao lado? → lê, salva e pronto
  ├─ hash (só se tamanho ou data mudaram desde o último)
  ├─ já existe transcrição desse hash? → liga à aula e pronto
  └─ entra na fila (Queued)

Fila (uma por vez; a aula aberta vai para a frente)
  ├─ modelo presente? senão, pede para baixar
  ├─ Running → worker
  ├─ segmentos chegam → vão para a tela, em memória
  └─ done → filtro → grava tudo numa transação → Done
```

- **Reabertura:** `Running` volta a `Queued` ao iniciar o app, e a fila recomeça sozinha.
- **Por que hash do arquivo inteiro:** é o que o `CLAUDE.md` pede, e o custo é pequeno perto da transcrição, feito uma vez só por versão do arquivo. O resultado fica junto com `FileSize` e `LastWriteUtc` para não ser recalculado a cada abertura.
- **Sem retomada parcial:** recomeçar é simples e evita juntar segmentos na fronteira. Custo: até uma aula de trabalho se o app fechar no meio.
- **Threads:** a fila usa `FireAndForget` (`VibePlayer.Core.Threading`). Os eventos chegam à UI pelo dispatcher.

### Legendas

- **Procura:** ao abrir a aula e em "Atualizar", na pasta do vídeo: `<nome>.srt`, `<nome>.vtt` e `<nome>.<idioma>.srt|vtt`, com `pt` e `pt-BR` antes dos demais, e SRT antes de VTT.
- **Codificação:** BOM (UTF-8, UTF-16); depois UTF-8 estrito; depois Windows-1252.
- **Mudança na legenda:** chave pelo hash do arquivo de legenda. Legenda alterada vira nova transcrição.
- **Formato:** `SubtitleParser` aceita vírgula ou ponto nos milissegundos, horas opcionais em VTT, e blocos numerados ou não. Blocos inválidos são pulados.

### Interface

Aba **Transcrição** no painel, depois de "Aulas" (Q5). `SidePanelTab` ganha o valor novo; a aba salva em `UiSettings` precisa ser mapeada com cuidado, porque o valor numérico é o índice.

- **Estados:** os do CA26, cada um com texto em `Strings.resx`.
- **Em andamento:** etapa (Extraindo áudio, Transcrevendo), barra, "Cancelar".
- **Concluída:** lista virtualizada de segmentos, com o tempo como botão, e a origem ("Legenda" ou "Whisper · modelo").
- **Menu da aula:** "Transcrever"; **menu do curso:** "Transcrever curso".
- **Primeiro uso:** diálogo com o tamanho do modelo, a opção "Mais preciso", e o aviso de que nada sai da máquina.
- **Avisos:** falhas passam por `INotificationService`, no máximo um por fila.
- **Chat, Resumo e Pontos:** seguem com os estados vazios atuais.

## Decisões

| Decisão | Motivo | Alternativas descartadas |
|---|---|---|
| Áudio pelo VLC (`transcode` + `smem`) | Sem binário novo, cerca de 620x, PTS por bloco | ffmpeg LGPL (sem build pronto no macOS, de 60 a 76 MB por RID nos builds prontos); `amem` (1x, ou perde áudio) |
| `speex_resampler` | Sem deriva; já na lista de permissão | Padrão `ugly` (perde 1,17 s em 20 min) |
| Silêncio nos buracos, pelo PTS | Tempos corretos depois do buraco; limita a deriva a 113 ms sem o speex | WAV (perde o tempo) |
| Worker no mesmo executável | Falha nativa não derruba o app; sem mudar o pacote | Em processo (falha fecha o app); segundo executável |
| Áudio em memória | Sem arquivo temporário com conteúdo da aula | WAV temporário (limpeza, e disco sem necessidade) |
| Vulkan com volta para CPU | 96% menos CPU na GPU integrada testada | Só CPU; CUDA (135 MB ou mais por SO) |
| `small-q5_1` como padrão (provisório) | Melhor equilíbrio medido | `base` (a confirmar em aula real), `medium` (mais lento, sem ganho medido) |
| Transcrição por hash, vínculo por aula | Reaproveita entre cursos e pastas; dados ligados a `LessonEntity.Id` | Só por `LessonId` (repetiria em cópias) |
| Segmentos salvos só no fim | Sem transcrição pela metade no banco | Gravar por segmento |
| Download próprio do modelo | Retomada, progresso, hash | `WhisperGgmlDownloader` |

## Mudanças em dados

Migration `AddTranscription`. Migrations publicadas não mudam.

```
Transcripts
  Id             Guid, PK
  SourceHash     TEXT, SHA-256 do vídeo (ou da legenda)
  Source         INT   Whisper | Subtitle
  Model          TEXT, nulo em legenda
  Language       TEXT, nulo se desconhecido
  CreatedAt      DateTimeOffset
  UNIQUE (SourceHash, Source)

TranscriptSegments
  TranscriptId   Guid, FK, cascade
  Position       INT
  StartMs, EndMs BIGINT
  Text           TEXT
  PK (TranscriptId, Position)

LessonTranscriptions
  LessonId       Guid, PK, FK para Lessons, cascade
  Status         INT   Queued | Running | Done | Failed | NoAudio
  TranscriptId   Guid, nulo, FK, set null
  VideoHash      TEXT, nulo
  HashedFileSize BIGINT, nulo
  HashedLastWriteUtc  DATETIME, nulo
  ErrorCode      TEXT, nulo
  QueuedAt       DateTimeOffset, nulo
  PreferWhisper  BOOL, o usuário escolheu o Whisper apesar da legenda
  UpdatedAt      DateTimeOffset
```

- Aula sem linha em `LessonTranscriptions` é "Não transcrita".
- Remover um curso apaga o vínculo, mas não a transcrição: se o curso voltar, o cache funciona. A limpeza de órfãs fica fora (uma hora de aula dá cerca de 50 KB).
- Bancos antigos recebem as tabelas vazias.

**Preferências novas:** `transcription.model`, `transcription.language`, `transcription.forceCpu`.

## Dependências novas

| Pacote | Versão | Licença | Por quê |
|---|---|---|---|
| `Whisper.net` | 1.9.1 | MIT (repositório e nuspec) | Interface com o whisper.cpp |
| `Whisper.net.Runtime` | 1.9.1 | MIT | Nativos CPU e Metal |
| `Whisper.net.Runtime.Vulkan` | 1.9.1 | MIT | GPU em `win-x64` e `linux-x64` (Q4) |
| `Whisper.net.Runtime.NoAvx` | 1.9.1 | MIT | CPU sem AVX |
| `Microsoft.Extensions.AI.Abstractions` | 10.2.0 | MIT | Transitivo do Whisper.net |

**Código nativo:** whisper.cpp e ggml, MIT.

**Modelos:** os pesos do Whisper são da OpenAI, com repositório MIT, e o repositório `ggerganov/whisper.cpp` no Hugging Face declara MIT. Eles não vão no pacote: são baixados pelo usuário.

**Plugins do VLC novos** (LGPL 2.1+, conferido no cabeçalho do código, branch 3.0.x):
- `libstream_out_transcode_plugin` (72 KB)
- `libstream_out_smem_plugin` (48 KB)

Eles existem nos pacotes Windows e macOS (arm64 e x64), e no `vlc-plugin-base` do Ubuntu 22.04/24.04 e do Debian 12, e no `vlc-plugins-base` do Arch.

Atualizar `THIRD-PARTY-NOTICES.md`. Nada GPL ou AGPL entra.

## Logs e erros

Sempre metadados, nunca texto:

- `Information`: início e fim, com `LessonId`, duração da aula, tempo gasto, modelo, runtime (`Vulkan`, `Cpu`, `Metal`), idioma e contagem de segmentos.
- `Warning`: código de saída anormal, volta para CPU, legenda inválida (só o `LessonId`), módulo `ugly` em uso.
- Caminhos de arquivo ficam fora; usa-se `LessonId`, como nas specs anteriores.

| Falha | O que o usuário vê |
|---|---|
| Rede ou Hugging Face fora | Aviso, com "Tentar de novo" |
| Hash do modelo errado | Aviso; arquivo descartado |
| Sem espaço em disco | Aviso antes de baixar |
| Arquivo ilegível | "Falhou", com "Tentar de novo" |
| Sem áudio | "Sem áudio" |
| Worker morreu | "Falhou"; a fila segue |
| Sem memória | Igual a worker morto, com dica de escolher modelo menor |
| VLC ausente (Linux) | Explicação na aba |

## Riscos

- **Atraso inicial do áudio (limite conhecido):** se o áudio começar N segundos depois do vídeo sem preenchimento no arquivo, os tempos saem N segundos adiantados. Medido com 3 s. Não dá para corrigir com o PTS relativo; o ffmpeg trataria esse caso. Avaliar em aulas reais.
- **macOS e Linux não medidos:** os plugins existem, mas extração e Whisper só rodaram no Windows. **Mitigação:** `AudioExtractionIntegrationTests` e `WhisperIntegrationTests` rodam nos três SOs da CI, e T2 valida o caminho antes do resto.
- **`speex_resampler` no Linux:** não apareceu em `vlc-plugins-base` do Arch, e o Fedora não foi verificado. Sem ele, os tempos ficam a menos de 113 ms, mas a qualidade do áudio cai. **Mitigação:** detectar o módulo pelo log do VLC, registrar `Warning` e documentar na wiki.
- **VLC futuro:** `smem` é uma interface pouco usada e pode mudar no VLC 4. **Mitigação:** `IAudioExtractor` isola, e a CI acusa.
- **Aulas muito longas:** cerca de 350 MB por hora; 5 h passam de 2,3 GB. **Mitigação:** em falta de memória, a aula falha sozinha, sem derrubar o app. Dividir em blocos fica para depois, se aparecer em uso real.
- **Reprodução durante a transcrição:** não medida. Sem GPU, o worker ocupa 8 threads, mesmo em prioridade baixa. **Mitigação:** verificação manual, coordenada com a spec 0008. Se engasgar, reduzir as threads enquanto o vídeo toca.
- **Qualidade em aula real:** só testada com voz sintética. **Mitigação:** Q3.
- **Driver de GPU:** Vulkan com driver ruim pode falhar. **Mitigação:** volta automática para CPU, testada no CA12.
- **Handles herdados no Windows:** um processo filho herda arquivos abertos por código nativo (`fopen` do Skia, por exemplo) no instante em que nasce. Se o worker abrir enquanto uma miniatura é gravada, esse arquivo fica preso até ele terminar. Efeito em produção: uma miniatura antiga deixa de ser apagada nessa limpeza, e sai na seguinte. Nos testes, os que abrem processo ficam na coleção `VLC real`.
- **Hugging Face fora ou URL alterada:** commit fixado reduz o risco, mas não o elimina. Um espelho próprio não entra nesta spec.

## Achado durante a implementação (aguardando decisão)

**Condição de corrida em `VlcMediaPlayer.OnPlaying`, da spec 0006, fora do escopo aprovado.** Medido com dump do crash: `OnPlaying` agenda no pool uma tarefa que chama `player.Volume`, `Mute` e `SetRate`. Se `Dispose` chegar antes de ela rodar, o ponteiro nativo já foi liberado e o processo cai com violação de acesso (`0xC0000005`), o que `GlobalExceptionHandler` não pega.

- **Em produção:** `Dispose` só acontece ao fechar o app, então seria preciso fechar a janela no mesmo instante em que uma aula começa a tocar.
- **Nos testes:** cada teste do VLC libera o player, e os testes novos deixam o pool mais ocupado. Foram 4 crashes em 8 execuções completas locais, sem nunca ter derrubado a CI até aqui. Pode tornar a CI instável.
- **Proposta:** proteger a tarefa com um lock e uma marca de "liberado", em `VlcMediaPlayer`. Mudança pequena, com um teste que libera o player logo depois de `Playing`.

A correção muda arquivo fora do escopo aprovado, por isso espera a decisão do mantenedor.

# 0006 — Design técnico

## Abordagem

```
Core/Playback/
  IMediaPlayer            Abstração do player: Open(path, startAt), Play, Pause, Seek, Volume, Mute, Rate,
                          estado e eventos (posição, duração, terminou, erro). Sem tipos do VLC.
  IMediaInfoReader        Lê a duração de um arquivo sem tocar (para a biblioteca)
  IThumbnailService       Caminho da miniatura de uma aula (se já existe) e fila de geração com prioridade
  ILessonProgressStore    Posição, concluída, última aula assistida
  PlaybackRules           Regras puras: retomar ou não, quando conta como concluída, próxima aula
Infrastructure/Playback/
  VlcRuntime              Carrega o LibVLC uma vez (Core.Initialize), opções, detecção de "VLC ausente"
  VlcMediaPlayer          IMediaPlayer sobre LibVLCSharp.MediaPlayer
  VlcMediaInfoReader      Media.Parse local (só metadados), em lote e em segundo plano
  VlcThumbnailGenerator   Um quadro via callbacks de vídeo do VLC (sem janela), codificado em JPEG com SkiaSharp
  ThumbnailService        Cache em <dados>/thumbnails, fila com prioridade, uma geração por vez
  LessonProgressStore     Tabela LessonProgress + preferências player.*
App/
  Controls/VideoView      NativeControlHost próprio que entrega a janela nativa ao VLC
  ViewModels/PlayerViewModel   Estado da barra, comandos, atalhos, salvamento periódico, contagem da próxima aula
  Views/Player/PlayerPanel     Vídeo + barra; estados vazio, carregando, erro, VLC ausente
  Views/Player/FullscreenWindow  Janela sem bordas em tela cheia com o mesmo VideoView
```

`PlayerViewModel` só conhece `IMediaPlayer`. Os testes unitários e os E2E usam um `FakeMediaPlayer`: o modo headless não tem janela nativa para o VLC desenhar, e a CI não precisa tocar vídeo para verificar regras, controles e atalhos. O VLC de verdade é coberto por um teste de integração separado (`VlcIntegrationTests`) com um vídeo curto em `tests/fixtures` e pela verificação manual nos três SOs.

### Renderização do vídeo

O `LibVLCSharp.Avalonia` (o `VideoView` oficial) ainda depende do Avalonia 11 e não funciona no 12. O app terá um `VideoView` próprio, pequeno:
- é um `NativeControlHost` que cria a janela nativa e entrega o identificador ao `MediaPlayer` do VLC;
- no Windows, pelo `Hwnd`; no Linux (X11), pelo `XWindow`; no macOS, pelo `NsObject`.

Assim o VLC desenha direto na GPU, com decodificação por hardware: é o caminho mais leve para aulas longas em notebook na bateria.

**Consequência (airspace):** nenhum controle do Avalonia pode ficar *por cima* da janela nativa do vídeo.
- Na janela normal isso não importa, porque a barra fica abaixo do vídeo.
- Em tela cheia, os controles ficam num `Popup`. No desktop, o `Popup` do Avalonia é uma janela própria e aparece acima do vídeo.

**Alternativa descartada:** callbacks de vídeo do VLC copiando cada quadro para um `WriteableBitmap`. Permitiria sobrepor controles, mas custa uma cópia de memória por quadro (cerca de 240 MB/s em 1080p a 30 fps), mais CPU e bateria, e não usa a GPU para exibir.

### Fluxo de reprodução

```
Biblioteca: aula selecionada → PlayerViewModel.Load(lesson)
  ├─ salva a posição da aula anterior
  ├─ caminho do arquivo (raiz + relativo, ou absoluto em vídeo avulso)
  ├─ posição inicial = PlaybackRules.ResumePosition(progresso salvo, duração)
  └─ IMediaPlayer.Open(caminho, posição) → toca (ou fica pausado, se veio da reabertura do app)

Durante a reprodução:
  ├─ PositionChanged → atualiza a barra (no máximo 4 vezes por segundo na UI)
  ├─ a cada 5 s tocando → salva a posição
  ├─ posição ≥ 95% → marca como concluída (uma vez)
  └─ Ended → concluída + contagem de 5 s → próxima aula disponível do curso (PlaybackRules.NextLesson)
```

- Eventos do VLC chegam em threads do próprio VLC. O `VlcMediaPlayer` repassa sem bloquear, e o `PlayerViewModel` agenda na thread de UI (`Dispatcher.UIThread.Post`).
- Nunca chamar o `MediaPlayer` do VLC de dentro de um evento dele (trava, comportamento documentado do LibVLC). Comandos vindos de eventos (ex.: tocar a próxima) passam primeiro pela thread de UI.
- `Open` com posição inicial usa a opção `:start-time` da mídia, e não um seek depois de abrir, para não tocar meio segundo do começo antes de pular.

### Regras (`PlaybackRules`, Core, puras e testadas)

- **Retomar:** se a aula não está concluída e a posição salva está entre 10 s e 95% da duração, a aula retoma dela. Senão, começa do início.
- **Concluída:** posição ≥ 95% da duração, ou o evento de fim. Aulas com menos de 30 s só contam pelo fim (95% de 20 s seria quase o começo).
- **Próxima aula:** a próxima disponível na ordem do curso (atravessando módulos). Nenhuma na última aula. Aulas indisponíveis são puladas.
- **Duração desconhecida** (arquivo ainda não lido): sem retomar, e sem concluir por porcentagem.

### Biblioteca

- `VlcMediaInfoReader` lê a duração das aulas sem duração gravada, em lote, depois de cada atualização da biblioteca.
  - Roda em segundo plano, com no máximo 2 leituras ao mesmo tempo, e cancela ao fechar o app.
  - Grava `Lessons.DurationMs` e avisa a árvore para recarregar, a cada 50 aulas ou no fim.
  - Arquivo que mudou (tamanho ou data diferentes) tem a duração relida.
- **Ícones na linha da aula:** concluída (`CheckmarkCircle`, cor `Success`), em andamento (anel parcial, cor `AccentText`), não iniciada (nada). A duração aparece como segunda linha ("12:34").
- **Curso:** a segunda linha passa de "20 aulas" para "8 de 20 aulas · 3 h 40 min". A duração total soma as aulas com duração conhecida.
- **Menu "⋯" da aula** ganha "Marcar como concluída" ou "Marcar como não concluída". Aulas de pasta passam a ter ações, só essas duas, porque continuam saindo da biblioteca apenas com o curso.

### Miniaturas

- **Como o quadro é tirado:** um `MediaPlayer` do VLC sem janela, com *callbacks* de vídeo, recebe os pixels em memória.
  - Ele abre o vídeo com `:start-time` em 10% da duração (sem duração conhecida, em 5 s) e `:no-audio`.
  - Guarda o primeiro quadro decodificado e para.
  - O quadro é reduzido para **320 × 180** (letterbox para manter a proporção) e gravado em JPEG, qualidade 80, com SkiaSharp.
- O `snapshot` nativo do VLC não serve: ele exige uma saída de vídeo visível.
- **Cache em disco:** `<dados>/thumbnails/<LessonId>-<FileSize>-<LastWriteTicks>.jpg`.
  - Arquivo de vídeo alterado (tamanho ou data) → o nome esperado muda e a miniatura é gerada de novo. A antiga é apagada na próxima limpeza.
  - Nenhuma coluna no banco: a existência do arquivo é o estado.
  - A pasta fica fora dos backups, porque o conteúdo é regenerável.
- **Fila com prioridade**, uma geração por vez (cada uma abre um decodificador, de 0,2 a 0,5 s):
  1. a aula selecionada;
  2. as aulas dos cursos expandidos na árvore;
  3. a primeira aula de cada curso (capa).

  As demais aulas só têm miniatura gerada quando o curso é aberto. Uma biblioteca de 2.000 aulas não vira meia hora de decodificação logo na primeira abertura.
- **Falha** ao gerar (arquivo corrompido, codec sem suporte) → `Debug` no log e ícone de vídeo permanente para aquela versão do arquivo. Não tenta de novo a cada abertura.
- **UI:**
  - **Linha da aula:** miniatura de 64 × 36 com raio 4, à esquerda do título; enquanto não existe, o ícone `Video` em `TextSecondary`.
  - **Linha do curso:** a capa no mesmo tamanho.
  - **Player:** com a aula selecionada e ainda sem tocar, a miniatura aparece grande, centralizada, com o título e o botão de reproduzir por cima. É controle do Avalonia, então o `VideoView` fica oculto até a reprodução começar (airspace).
- **Carregar as imagens na UI:** decodificar o JPEG em `Bitmap` com largura de 128 px (2× para telas de alta densidade) fora da thread de UI. Há um cache em memória das últimas 300, para a árvore rolar sem reler o disco.

### Tela cheia

- Uma `FullscreenWindow` sem bordas, em `WindowState.FullScreen`, recebe o mesmo `MediaPlayer`. Trocar o identificador da janela nativa não recarrega o vídeo.
- Os controles ficam num `Popup` preso à parte de baixo da janela, que aparece com o movimento do ponteiro e some depois de 3 s parado (`DispatcherTimer`). O cursor some junto.
- Os atalhos de teclado valem igual. Esc sai da tela cheia.

### Atalhos

- Os atalhos ficam na `MainWindow` e na `FullscreenWindow`, com o mesmo modificador do sistema da spec 0003 para os combinados (Ctrl/⌘).
- Teclas simples (Espaço, setas, letras) são ignoradas quando o foco está num `TextBox`, para não atrapalhar a digitação no chat.
- Teclas simples também não valem quando o foco está na árvore da biblioteca, porque lá as setas navegam.
  - Para isso, os atalhos simples só agem com o foco no player ou em lugar nenhum. Clicar no vídeo põe o foco no player.
  - **Exceção:** Espaço funciona sempre que não houver campo de texto com foco.

### VLC: carregamento, opções e licença

- `Core.Initialize()` do LibVLCSharp roda uma vez, na primeira vez que o player é usado, e não na abertura do app: o app abre rápido mesmo com o VLC demorando a carregar plugins.
  - **Opções:** `--avcodec-hw=any` (hardware), `--no-video-title-show`, `--no-sub-autodetect-file` (as legendas entram com a transcrição) e `--quiet`.
  - **Logs do VLC:** só `Warning` e acima são repassados ao log do app (o VLC é muito verboso).
- **VLC ausente (Linux):** `Core.Initialize` lança `VLCException`. O `VlcRuntime` registra uma vez e informa o estado `Unavailable`, e o player mostra a orientação de instalação.
- **Windows** (`VideoLAN.LibVLC.Windows`) e **macOS** (`VideoLAN.LibVLC.Mac`, que traz `osx-arm64` e `osx-x64`): binários no pacote, filtrados por **lista de permissão** de plugins.
  - **Fica:** o necessário para os formatos aceitos. Isso inclui demuxers (mp4, mkv, avi, ts, ps, webm), decodificação via `avcodec` e hardware (d3d11va/dxva2, videotoolbox), saídas de vídeo e áudio e filtros de conversão.
  - **Sai:** plugins GPL (ex.: `x264`, `goom`) e tudo que não é reprodução local (rede, streaming, codificadores, visualizações, interfaces).
  - O pacote do Windows aceita o filtro nativamente (`VlcWindowsX64IncludeFiles`). Para o do macOS, o filtro é um passo do build próprio sobre os arquivos copiados.
- **CI:** um passo verifica, nos pacotes publicados, que nenhum plugin fora da lista de permissão foi incluído (CA12). O Ubuntu da CI instala `libvlc-dev` e `vlc-plugin-base` para rodar `VlcIntegrationTests`.

## Decisões

| Decisão | Motivo | Alternativas descartadas |
|---|---|---|
| `VideoView` próprio sobre `NativeControlHost` | `LibVLCSharp.Avalonia` ainda é Avalonia 11; desenho nativo com GPU | Callbacks de quadro em `WriteableBitmap` (CPU e bateria); esperar o pacote oficial (bloqueia a spec) |
| `IMediaPlayer` no Core e player falso nos testes | Headless não tem janela nativa; CI não precisa tocar vídeo para testar regras | Testar só manualmente |
| Plugins por lista de permissão | Tira GPL e reduz o pacote (os plugins do Windows somam cerca de 107 MB) | Lista de exclusão (um plugin GPL novo numa atualização do VLC entraria sem ninguém notar) |
| VLC do sistema no Linux | Bibliotecas de sistema variam por distribuição; o pacote `vlc` existe em todas | Embutir no tar.gz (frágil); Flatpak (spec própria) |
| Progresso numa tabela própria, não em `Lessons` | Progresso é dado do usuário; `Lessons` é espelho dos arquivos e é reescrita a cada atualização | Colunas em `Lessons` |
| `start-time` na abertura em vez de seek | Não toca o começo antes de pular | Seek depois de `Playing` |
| Durações lidas pelo VLC (`Media.Parse`) | Já é dependência; leitura só de metadados é rápida | ffprobe (outro binário; o ffmpeg entra com a transcrição) |
| Miniaturas por callbacks do VLC + SkiaSharp | Sem janela; mesmo decodificador da reprodução; SkiaSharp já vem com o Avalonia | `snapshot` do VLC (exige saída de vídeo visível); ffmpeg (entra só com a transcrição) |
| Cache por nome de arquivo com tamanho e data | Invalidação automática sem estado no banco | Coluna no banco (mais um estado para manter coerente) |
| Geração sob demanda com prioridade | Primeira abertura de uma biblioteca grande não ocupa a máquina por minutos | Gerar tudo depois de cada atualização |

## Mudanças em dados

Migration `ProgressoDasAulas`:

| Tabela | Colunas | Observação |
|---|---|---|
| `Lessons` | + `DurationMs` (nulo = ainda não lida) | Relida quando tamanho/data do arquivo mudam |
| `LessonProgress` | `LessonId` (PK, FK → `Lessons` `CASCADE`), `PositionMs`, `IsCompleted`, `CompletedAt`, `LastWatchedAt` | Uma linha por aula já aberta |

Preferências (tabela `Settings`, spec 0004): `player.volume`, `player.muted`, `player.rate`, `player.autoAdvance` (sem tela nesta spec; padrão `true`) e `player.lastLessonId`.

## Dependências novas

| Pacote | Projeto | Licença | Observação |
|---|---|---|---|
| LibVLCSharp 3.10.2 | Infrastructure (e App, pelo `VideoView`) | LGPL-2.1+ | DLL separada, linkagem dinâmica |
| VideoLAN.LibVLC.Windows 3.0.24 | App (só RID `win-*`) | LGPL-2.1+ (plugins filtrados) | Binários nativos |
| VideoLAN.LibVLC.Mac 3.1.3.2 | App (só RID `osx-*`) | LGPL-2.1+ (plugins filtrados) | Binários nativos arm64 e x64 |
| SkiaSharp 3.119.4 | Infrastructure | MIT | Já vem com o Avalonia; referência explícita na mesma versão para codificar as miniaturas |

`THIRD-PARTY-NOTICES.md` recebe o texto da LGPL 2.1 e a indicação de onde obter o código-fonte do VLC e do LibVLCSharp, como a LGPL exige.

## Logs e erros

- `Information`: "Reprodução iniciada: {LessonId} em {StartMs}"; "Aula concluída: {LessonId}"; "Durações lidas: {Count} em {Elapsed}"; "Miniaturas geradas: {Count} em {Elapsed}" (por lote, não por aula).
- `Warning`: VLC indisponível (uma vez); mensagens do próprio VLC de nível Warning.
- `Error`: falha ao abrir mídia ({LessonId}, motivo do VLC). Nunca o caminho em `Information` (nomes de pasta podem ser pessoais; ficam em `Debug`).
- A área do vídeo mostra "Não foi possível reproduzir esta aula" + motivo curto ("arquivo não encontrado", "formato não suportado").

## Riscos

- **Linux/Wayland:** o `NativeControlHost` do Avalonia usa X11 (XWayland em sessões Wayland). A verificação manual no Linux cobre isso; se falhar, o plano B é renderização por callbacks só no Linux.
- **Assinatura no macOS:** dylibs do VLC sem assinatura agravam o aviso do Gatekeeper. Fica documentado; resolve com a spec de assinatura.
- **Tamanho dos pacotes:** mesmo filtrados, os plugins pesam. A meta é ficar abaixo de 80 MB a mais, medida no PR.
- **Plugins que faltam:** a lista de permissão pode cortar algo necessário a um formato. Mitigação: o teste de integração toca as amostras de cada formato do CA12, e o log do VLC acusa módulo ausente.
- **Miniaturas em bibliotecas enormes:** mesmo sob demanda, abrir um curso de 500 aulas enfileira 500 gerações (de 2 a 4 min). A fila prioriza a seleção e o que está visível, e cancela ao fechar. Se pesar, a próxima mudança é gerar só as linhas visíveis na tela.
- **Eventos do VLC e travamentos:** chamadas reentrantes ao `MediaPlayer` travam o processo. Mitigação: regra de passar pela thread de UI, revisada no PR e coberta pelo teste de integração (abrir, terminar e abrir a próxima).

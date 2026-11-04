# 0006 — Tarefas

Marque cada tarefa ao concluir.

## Implementação

- [x] T1 — Pacotes do VLC, lista de permissão de plugins (Windows e macOS), avisos de licença (LGPL 2.1 e origem do código-fonte).
- [x] T2 — `IMediaPlayer`, `IMediaInfoReader`, `ILessonProgressStore` e `PlaybackRules` (Core).
- [x] T3 — `VlcRuntime`, `VlcMediaPlayer` e `VlcMediaInfoReader` (Infrastructure), com repasse de eventos sem reentrância e logs filtrados.
- [x] T4 — Migration `ProgressoDasAulas` e `LessonProgressStore`.
- [x] T5 — Quadros do VLC por callbacks (RV32, dois buffers, limite de 1920 px) e `VideoView` que desenha num `WriteableBitmap`.
- [x] T6 — `PlayerViewModel`: carregar aula, controles, salvamento periódico, concluída, próxima aula com contagem, preferências, última aula.
- [x] T7 — `PlayerPanel`: barra funcional, estados (vazio, carregando, erro, VLC ausente), contagem da próxima aula.
- [x] T8 — Tela cheia na própria `MainWindow` (só o player), controles sobrepostos com ocultação automática, duplo clique.
- [x] T9 — Atalhos de teclado, com as regras de foco.
- [x] T10 — Biblioteca: durações em segundo plano, ícones de progresso, andamento do curso, marcar/desmarcar concluída.
- [x] T11 — Reabrir na última aula assistida.
- [x] T11a — Miniaturas: `IThumbnailService` (Core), `VlcThumbnailGenerator` + `ThumbnailService` (Infrastructure, cache e fila com prioridade), exibição na árvore (aula e capa do curso) e pôster no player.
- [x] T12 — CI: `libvlc` no Ubuntu e verificação dos plugins nos pacotes de release.
- [x] T13 — Textos, nomes acessíveis, `DESIGN.md` (barra do player, ícones de progresso, tela cheia), CLAUDE.md e capturas.

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1 | E2E + manual | `PlayerTests.SelecionarAula_TocaEHabilitaControles` (player falso); conferido no Windows pelo mantenedor; **falta** macOS e Linux |
| CA2 | E2E | `PlayerTests.Seek_VolumeEVelocidade_SobrevivemAReabertura`, `Arraste_BarraSegueOPonteiroEVideoVaiParaOFinal` e `PassarOPonteiroNaBarra_MostraOTempoDaquelePontoSobOPonteiro` |
| CA3 | E2E | `PlayerShortcutTests.AtalhosDoPlayer_FuncionamComFocoNoVideo` e `Atalhos_NaoDisparamDigitandoNoChatNemNaArvore` |
| CA4 | E2E | `PlayerTests.TelaCheia_SoOPlayer_ControlesSomemEVoltam` |
| CA5 | Unitário + E2E | `PlaybackRulesTests.Retomar*`; `PlayerTests.Posicao_SalvaERetomada` |
| CA6 | Unitário + E2E | `PlaybackRulesTests.Concluida*`; `PlayerTests.Concluir_AtualizaBibliotecaEMarcarDesmarcar` |
| CA7 | Unitário + E2E | `PlaybackRulesTests.ProximaAula*`; `PlayerTests.FimDaAula_ContagemEProxima` e `CancelarContagem_MantemAulaENaUltimaNadaAcontece` |
| CA8 | E2E | `LibraryPlaybackTests.Duracoes_EmSegundoPlano_ComPastaGrande` (leitor de mídia falso) |
| CA9 | E2E | `PlayerTests.Reabertura_SelecionaUltimaAulaPausadaComPoster` |
| CA10 | E2E + integração | `PlayerTests.ErroAoAbrir_MostraMensagemESegueFuncionando`; `VlcIntegrationTests.ArquivoCorrompido_InformaErro` e `ArquivoInexistente_InformaErroSemChamarOVlc` |
| CA11 | E2E + manual | `PlayerTests.VlcAusente_MostraOrientacao` (player falso); **falta** conferir num Linux sem VLC |
| CA12 | Integração + CI + manual | `VlcIntegrationTests.TocaAmostras` (mp4, mkv, webm, avi em `tests/fixtures`); `check-vlc-plugins.sh` na CI e na release, `VlcPluginListTests`; **falta** abrir os pacotes de macOS e Linux |
| CA13 | E2E | `AccessibilityTests.Player_ControlesTemNomeEEstadoEhAnunciado` |
| CA14 | Unitário + E2E + integração | `ThumbnailServiceTests.*` (cache, invalidação, prioridade, falha sem nova tentativa); `LibraryPlaybackTests.MiniaturasEPlaceholder`, `MiniaturaQueFalha_FicaComIcone` e `PlayerTests.AulaPausada_MostraPosterComMiniatura` (gerador falso); `VlcIntegrationTests.GeraMiniatura` |

As amostras de `tests/fixtures/videos` são vídeos de 3 s com padrões de teste gerados pelo ffmpeg (H.264, H.265, VP9, MPEG-4), sem conteúdo de terceiros. A saída de um codificador não carrega a licença dele.

## Documentação

- [x] CLAUDE.md (camada de reprodução, regras de thread do VLC, VLC no Linux)
- [x] `DESIGN.md` (inclui miniaturas na árvore e o pôster)
- [x] `THIRD-PARTY-NOTICES.md` (LGPL 2.1 completa e origem do código)
- [ ] Wiki: "Assistindo às aulas" e dependência do VLC no Linux (pendente enquanto não houver wiki)

## Desvios em relação ao design

- **Desenho do vídeo pelo app** (aprovado pelo mantenedor durante a implementação): quadros do VLC num `WriteableBitmap`, e não janela nativa. Tela cheia na própria `MainWindow`, sem `FullscreenWindow`. O `design.md` já está atualizado.
- **Abrir pausado não desenha quadro:** com `:start-paused`, o VLC não entrega nenhum quadro antes de tocar. Por isso o pôster fica na tela até o primeiro "reproduzir".
- **Altura do quadro:** o VLC informa a altura com o preenchimento do decodificador (192 ou 194 linhas para um vídeo de 180). O buffer é alinhado a 32 e o tamanho visível vem de `MediaPlayer.Size`, consultado fora das callbacks.
- **`LibVLC` único e nunca liberado por processo:** cada instância abandonada com callback de log derrubaria o processo quando o GC coletasse o delegate.
- **Log do VLC:** erros dele viram `Warning` no log do app e avisos viram `Debug`, porque o VLC chama de "warning" coisas rotineiras.
- **Miniatura sem quadro:** se não sair quadro na posição pedida (vídeo curto com duração ainda desconhecida), tenta o começo do vídeo antes de desistir.
- **Barra compacta:** abaixo de 560 px o player esconde anterior/próxima e o controle de volume, para caber nos 400 px mínimos.
- **Tempo sob o ponteiro e arraste** (pedido do mantenedor ao testar): ver "Barra de posição" no `design.md`.
- **Recarga da árvore durante a leitura de durações:** no máximo a cada 2 s e uma vez no fim, e não a cada lote.
- **`player.lastLessonId` e `IAppPaths.ThumbnailsDirectory`** entraram como previsto; `ILessonDurationUpdater` é uma interface a mais no Core para a leitura de durações.
- **Notas de CA12:** o filtro de plugins é um só para Windows e macOS, no `VlcPlugins.targets`, sobre os itens `Content` dos pacotes.

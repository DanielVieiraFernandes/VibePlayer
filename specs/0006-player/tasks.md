# 0006 — Tarefas

Marque cada tarefa ao concluir.

## Implementação

- [ ] T1 — Pacotes do VLC, lista de permissão de plugins (Windows e macOS), avisos de licença (LGPL 2.1 e origem do código-fonte).
- [ ] T2 — `IMediaPlayer`, `IMediaInfoReader`, `ILessonProgressStore` e `PlaybackRules` (Core).
- [ ] T3 — `VlcRuntime`, `VlcMediaPlayer` e `VlcMediaInfoReader` (Infrastructure), com repasse de eventos sem reentrância e logs filtrados.
- [ ] T4 — Migration `ProgressoDasAulas` e `LessonProgressStore`.
- [ ] T5 — `VideoView` (`NativeControlHost`) para Windows, Linux (X11) e macOS.
- [ ] T6 — `PlayerViewModel`: carregar aula, controles, salvamento periódico, concluída, próxima aula com contagem, preferências, última aula.
- [ ] T7 — `PlayerPanel`: barra funcional, estados (vazio, carregando, erro, VLC ausente), contagem da próxima aula.
- [ ] T8 — Tela cheia (`FullscreenWindow`, controles em `Popup` com ocultação automática).
- [ ] T9 — Atalhos de teclado, com as regras de foco.
- [ ] T10 — Biblioteca: durações em segundo plano, ícones de progresso, andamento do curso, marcar/desmarcar concluída.
- [ ] T11 — Reabrir na última aula assistida.
- [ ] T11a — Miniaturas: `IThumbnailService` (Core), `VlcThumbnailGenerator` + `ThumbnailService` (Infrastructure, cache e fila com prioridade), exibição na árvore (aula e capa do curso) e pôster no player.
- [ ] T12 — CI: `libvlc` no Ubuntu e verificação dos plugins nos pacotes de release.
- [ ] T13 — Textos, nomes acessíveis, `DESIGN.md` (barra do player, ícones de progresso, tela cheia), CLAUDE.md e capturas.

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1 | E2E + manual | `PlayerTests.SelecionarAula_TocaEHabilitaControles` (player falso); roteiro manual nos três SOs |
| CA2 | Unitário + E2E | `PlayerViewModelTests.Seek*`, `Volume*`, `Velocidade*`; `PlayerTests.VolumeEVelocidade_SobrevivemAReabertura` |
| CA3 | E2E | `PlayerShortcutTests.*` e `Atalhos_NaoDisparamDigitandoNoChat` |
| CA4 | E2E + manual | `FullscreenTests.*` (controles e ocultação); manual para a janela nativa |
| CA5 | Unitário + E2E | `PlaybackRulesTests.Retomar*`; `PlayerTests.Posicao_SalvaERetomada` |
| CA6 | Unitário + E2E | `PlaybackRulesTests.Concluida*`; `PlayerTests.Concluir_AtualizaBiblioteca` e `MarcarDesmarcarConcluida` |
| CA7 | Unitário + E2E | `PlaybackRulesTests.ProximaAula*`; `PlayerTests.FimDaAula_ContagemEProxima` e `CancelarContagem` |
| CA8 | E2E | `LibraryDurationTests.Duracoes_EmSegundoPlano_ComPastaGrande` (leitor de mídia falso com atraso) |
| CA9 | E2E | `PlayerTests.Reabertura_SelecionaUltimaAulaPausada` |
| CA10 | Unitário + E2E + manual | `PlayerViewModelTests.ErroAoAbrir_MostraMensagem`; `VlcIntegrationTests.ArquivoCorrompido_InformaErro` |
| CA11 | E2E + manual | `PlayerTests.VlcAusente_MostraOrientacao` (runtime falso); manual num Linux sem VLC |
| CA12 | Integração + CI + manual | `VlcIntegrationTests.TocaAmostras` (mp4, mkv, webm, avi em `tests/fixtures`); passo da CI que compara os plugins publicados com a lista de permissão |
| CA13 | E2E | `AccessibilityTests` estendido com o player e com o anúncio de estado |
| CA14 | Unitário + E2E + integração | `ThumbnailServiceTests.*` (cache, invalidação, prioridade, falha sem nova tentativa); `LibraryThumbnailTests.MiniaturasEPlaceholder` e `PlayerTests.AulaPausada_MostraPoster` (gerador falso); `VlcIntegrationTests.GeraMiniatura` |

As amostras de `tests/fixtures` são vídeos de 2 s gerados com codecs livres (VP9/Opus, AV1), sem conteúdo de terceiros. O MP4 usa H.264 gerado localmente: a saída de um codificador não carrega a licença dele.

## Documentação

- [ ] CLAUDE.md (camada de reprodução, regras de thread do VLC, VLC no Linux)
- [ ] `DESIGN.md` (inclui miniaturas na árvore e o pôster)
- [ ] `THIRD-PARTY-NOTICES.md` (LGPL 2.1 completa e origem do código)
- [ ] Wiki: "Assistindo às aulas" e dependência do VLC no Linux (pendente enquanto não houver wiki)

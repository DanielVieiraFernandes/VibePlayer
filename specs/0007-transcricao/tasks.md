# 0007 — Tarefas

Marque cada tarefa ao concluir.

## Implementação

Validação primeiro, porque as decisões mais arriscadas só foram testadas no Windows:

- [ ] T1 — Comparar os modelos com a aula real enviada pelo mantenedor e fechar o padrão (Q3). Se o resultado mudar a decisão, atualizar a spec.
- [ ] T2 — `VlcAudioExtractor` no worker, incluindo a distinção entre "sem áudio" e arquivo ilegível, com a CI rodando `AudioExtractionIntegrationTests` nos três SOs **antes** do restante. Falhou no Linux ou no macOS: parar e rever a spec.
- [ ] T3 — Plugins `libstream_out_transcode_plugin` e `libstream_out_smem_plugin` em `VlcPlugins.targets` e `vlc-allowed-plugins.txt`.
- [ ] T4 — Fixtures novos: tom contínuo de 60 s (deriva), áudio com buraco de PTS, vídeo sem áudio, fala curta de domínio público (`jfk.wav`, do whisper.cpp).
- [ ] T5 — Repetir a tabela de codecs do `design.md` com o caminho final (`smem`).

Core e dados:

- [ ] T6 — `Core/Transcription`: interfaces, modelos, `WorkerProtocol`.
- [ ] T7 — `SubtitleParser`, `SubtitleEncoding`, `TranscriptSegmentFilter`.
- [ ] T8 — Entidades, configurações e migration `AddTranscription`; `TranscriptStore`.

Worker:

- [ ] T9 — `WorkerEntryPoint` e entrada em `Program.Main`, antes de Serilog e Avalonia.
- [ ] T10 — `WhisperTranscriber`, com ordem de runtime e `forceCpu`.
- [ ] T11 — `ProcessTranscriptionWorker`: abrir, prioridade, eventos, cancelar, fim anormal.

Serviços:

- [ ] T12 — `WhisperModelStore`: download com retomada, hash e espaço em disco.
- [ ] T13 — `Sha256FileHasher` e `PhysicalSubtitleSource`.
- [ ] T14 — `TranscriptionService`: fila, prioridade, cache, reabertura, volta para CPU.
- [ ] T15 — Pacotes e `Directory.Packages.props`; `AddInfrastructure`; cada RID leva só os nativos dele.

Interface:

- [ ] T16 — `TranscriptionViewModel`, `TranscriptionView` e a aba nova em `SidePanelTab`.
- [ ] T17 — Menus da aula e do curso; diálogo de primeiro uso.
- [ ] T18 — Textos em `Strings.resx`.

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1 | Integração | `AudioExtractionIntegrationTests.Extract_FormatosDosFixtures_SaiPcm16kMonoComDuracaoDoVideo` |
| CA1 | Integração | `AudioExtractionIntegrationTests.Extract_TomDe60s_NaoDeriva` |
| CA2 | Integração | `AudioExtractionIntegrationTests.Extract_ComBuracoDePts_PreencheComSilencio` |
| CA2 | Unitário | `PtsGapFillerTests.Append_PosicaoPassaDoLimite_InsereSilencio` |
| CA3 | Integração | `AudioExtractionIntegrationTests.Extract_SemFaixaDeAudio_InformaSemAudio` |
| CA3 | Unitário | `TranscriptionServiceTests.Run_WorkerInformaSemAudio_FicaComoSemAudioENaoRepete` |
| CA4 | Integração | `AudioExtractionIntegrationTests.Extract_ArquivoCorrompido_InformaErro` |
| CA5 | Unitário | `VlcPluginListTests` (já existente, cobre as duas listas) |
| CA5 | CI | `check-vlc-plugins.sh` |
| CA6 | Integração | `WhisperIntegrationTests.Transcribe_FalaDeDominioPublico_TrazPalavrasESegmentosEmOrdem` (com `ggml-tiny-q5_1`, em cache na CI) |
| CA7 | Unitário | `TranscriptionServiceTests.Run_EventosDeProgresso_ChegamAoAssinante` |
| CA7 | Unitário | `TranscriptionServiceTests.Run_AntesDoFim_NadaFicaGravado` |
| CA8 | Integração | `ProcessTranscriptionWorkerTests.Cancel_ProcessoEmAndamento_TerminaEmDoisSegundos` |
| CA9 | Unitário | `TranscriptionServiceTests.Enqueue_AulaAberta_VaiParaAFrente` |
| CA9 | Unitário | `TranscriptionServiceTests.Run_VariasAulas_UmaDeCadaVez` |
| CA10 | E2E | `TranscriptionTests.Reabertura_AulasPendentes_ContinuamNaFila` |
| CA11 | Integração | `ProcessTranscriptionWorkerTests.Run_ProcessoMorre_InformaFalhaComCodigoDeSaida` (com um worker de teste) |
| CA11 | Unitário | `TranscriptionServiceTests.Run_WorkerMorre_MarcaFalhaESegueAFila` |
| CA12 | Unitário | `TranscriptionServiceTests.Run_FalhaComGpu_RepeteNaCpuEGravaPreferencia` |
| CA13 | Unitário | `TranscriptSegmentFilterTests.Apply_SoMarcacao_Remove` |
| CA14 | E2E | `TranscriptionTests.PrimeiroUso_SemModelo_PedeConfirmacaoEBaixa` |
| CA15 | Unitário | `WhisperModelStoreTests.Download_ArquivoParcial_RetomaComRange` (HTTP falso) |
| CA16 | Unitário | `WhisperModelStoreTests.Download_HashDiferente_DescartaEFalha` (HTTP falso) |
| CA17 | E2E | `TranscriptionTests.Reconectar_PastaMovida_MantemTranscricao` |
| CA18 | Unitário | `TranscriptionServiceTests.Enqueue_MesmoHash_ReaproveitaSemWorker` |
| CA19 | Unitário | `TranscriptionServiceTests.Status_ArquivoAlterado_VoltaANaoTranscrita` |
| CA20 | Unitário | `SubtitleSourceTests.Find_VariacoesDeNome_EscolheNaOrdem` |
| CA20 | E2E | `TranscriptionTests.Aula_ComLegenda_MostraSemWhisper` |
| CA21 | Unitário | `SubtitleEncodingTests.Decode_Codificacoes_MantemAcentos` |
| CA22 | Unitário | `SubtitleParserTests.Parse_TagsEConfiguracoes_SobraOTexto` |
| CA23 | Unitário | `SubtitleParserTests.Parse_BlocosInvalidos_AproveitaOsValidos` |
| CA24 | E2E | `TranscriptionTests.Tempo_Clique_LevaOVideoAoPonto` |
| CA25 | E2E | `TranscriptionTests.VlcAusente_ComLegenda_Funciona` e `TranscriptionTests.VlcAusente_SemLegenda_Explica` |
| CA26 | E2E | `TranscriptionTests.Estados_CadaUm_MostraOTextoCerto`, `AccessibilityTests`, `ViewConventionsTests` |
| CA27 | E2E | `TranscriptionTests.Logs_NaoContemTexto` (com `InMemorySink`) |
| CA27 | Unitário | `ArchitectureTests` (Core sem Whisper.net nem HTTP) |

O provedor de Whisper real só roda em `WhisperIntegrationTests`. Os E2E usam um `ITranscriptionWorker` falso, determinístico. Os testes de integração não entram no grupo de unitários, porque tocam disco, VLC e Whisper.

**CI:** baixar `ggml-tiny-q5_1.bin` (31 MB, SHA-256 conferido) com `actions/cache`, nos três SOs. Não há outro jeito de saber se os nativos de cada SO funcionam.

**Verificação manual nos três SOs:** transcrever uma aula real inteira; tocar vídeo durante a transcrição (coordenar com a spec 0008); cancelar; fechar o app no meio; aula com legenda.

## Documentação

- [ ] `CLAUDE.md`: stack (sem ffmpeg, se Q1 for aprovada), licenças, seção de transcrição, regras do worker, estrutura.
- [ ] `docs/backlog.md`: tirar a transcrição das previstas; incluir legendas sobre o vídeo.
- [ ] `DESIGN.md`: aba Transcrição.
- [ ] `THIRD-PARTY-NOTICES.md`: Whisper.net, whisper.cpp, ggml, `Microsoft.Extensions.AI.Abstractions`.
- [ ] Wiki: página de transcrição, tempos esperados, legendas aceitas.
- [ ] `specs/0007-transcricao`: status Implementada.

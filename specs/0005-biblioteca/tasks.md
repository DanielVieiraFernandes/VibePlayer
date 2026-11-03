# 0005 — Tarefas

Marque cada tarefa ao concluir.

## Implementação

- [x] T1 — FluentValidation no `Core` (pacote, licença em `THIRD-PARTY-NOTICES.md`).
- [x] T2 — `NaturalStringComparer`.
- [x] T3 — `IFileSystemReader` (Core) e `PhysicalFileSystemReader` (Infrastructure), sem seguir links e pulando ocultos/sistema.
- [x] T4 — `LibraryScanner` (filtros, módulos aninhados, ordem, NFC) e `LibraryDetection`.
- [x] T5 — `LibraryFolderValidator`.
- [x] T6 — Entidades, configurações e migration `Biblioteca`.
- [x] T7 — `CourseLibrary`: preview das duas interpretações, add de pasta e de vídeos (curso novo ou "Vídeos avulsos"), remove (curso e aula avulsa), relocate, leitura da árvore e sincronização com progresso, cancelamento e uma atualização por vez.
- [x] T8 — `IFilePicker` (pasta e vídeos) sobre o `StorageProvider`; `ConfirmDialog`.
- [x] T9 — `LibraryViewModel`, `AddLibraryFolderViewModel`, `AddVideosViewModel`, `LibraryView` (árvore, ações, progresso, faixa de erro, menu com Remover/Localizar pasta), `AddLibraryFolderDialog` e `AddVideosDialog`.
- [x] T10 — Aula atual no `PlayerViewModel` e no `PlayerPanel`.
- [x] T11 — Atualização da biblioteca em segundo plano ao abrir o app e cancelamento ao fechar.
- [x] T12 — Textos em `Strings.resx`, nomes acessíveis, `longPathAware` no manifesto se faltar.
- [x] T13 — CLAUDE.md (estrutura e fluxo da biblioteca) e capturas de tela para o PR.

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1 | Unitário + E2E | `LibraryDetectionTests.*`, `LibraryDialogViewModelTests.*`; `LibraryViewTests.AdicionarPasta_PerguntaInterpretacaoComPrevia`, `AdicionarPasta_Cancelado_NaoGravaNada`, `DialogoDePasta_TrocarOpcaoAtualizaPreviaEConfirmar` |
| CA2 | Unitário | `NaturalStringComparerTests.*` |
| CA3 | Unitário | `LibraryScannerTests.IgnoraNaoVideosOcultosSistemaELinks` e `ModulosAninhados_ViramNomeComposto` |
| CA4 | Unitário + E2E | `LibraryFolderValidatorTests.*`; `LibraryViewTests.AdicionarPastaInvalida_MostraFaixaDeErro` |
| CA5 | E2E | `LibraryTests.Reabertura_MantemBiblioteca` |
| CA6 | E2E | `LibraryTests.Atualizar_NovoAparece_RemovidoFicaIndisponivel_VoltaComMesmoId` |
| CA7 | E2E | `LibraryTests.RaizInacessivel_CursosIndisponiveisRestoFunciona` |
| CA8 | E2E | `LibraryTests.RemoverCurso_ApagaRegistrosENaoTocaArquivos` |
| CA9 | E2E + unitário | `LibraryTests.PastaGrande_AtualizaEmSegundoPlanoComProgresso` e `SubpastaSemPermissao_RegistraEContinua` (este roda no Linux e no macOS; no Windows o caso fica com `LibraryScannerTests.SubpastaIlegivel_EhPuladaEContada`) |
| CA10 | E2E | `LibraryViewTests.SelecionarAula_MostraTituloNoPlayer` e `AulaIndisponivel_NaoPodeSerSelecionada` |
| CA12 | Unitário + E2E | `VideoSelectionValidatorTests.*`; `LibraryTests.AdicionarVideos_CursoNovoOuAvulsos` e `RemoverAulaAvulsa` |
| CA13 | E2E | `LibraryTests.LocalizarPasta_ReconectaComMesmosIds` e `LocalizarPasta_SemAulasConhecidas_PedeConfirmacao` |
| CA11 | E2E | `AccessibilityTests.ElementosInterativos_TemNomeAcessivel` e `ElementosInterativos_ComBibliotecaPreenchida_TemNomeAcessivel` e `LibraryViewTests.Teclado_NavegaESeleciona` |

A biblioteca de exemplo é gerada pelo próprio teste em pasta temporária (arquivos `.mp4` vazios bastam: nesta spec ninguém lê o conteúdo do vídeo).

## Documentação

- [x] CLAUDE.md (T13)
- [x] `DESIGN.md`: faixa de erro, árvore e item indisponível
- [ ] Wiki: "Adicionando cursos" (pastas, vídeos avulsos, indisponível, Localizar pasta) (**pendente**: a wiki ainda não existe)
- [x] `docs/backlog.md` com a ideia de atualização automática do app

## Desvios em relação ao design

- **Tabela `RemovedCourses`:** remover um curso de uma "pasta de cursos" precisa ser lembrado; sem isso, a próxima atualização encontraria a subpasta e traria o curso de volta. Remover o último curso de uma pasta tira a pasta inteira da biblioteca.
- **`IFolderPicker` virou `IFilePicker`** (pasta e vídeos), e os diálogos ficam atrás de `IDialogService` (`WindowDialogService` em produção, respostas roteirizadas nos E2E).
- **Ações dos itens mais à vista** (pedido do mantenedor na revisão do PR):
  - botão "⋯" sempre visível em cada item com ações;
  - clique direito em qualquer ponto da linha, inclusive no recuo e no espaço à direita do texto;
  - "Localizar pasta…" como link direto na linha do curso cuja pasta sumiu.

  O menu é tratado no `TreeView`, e não preso ao conteúdo do item, e a árvore não tem mais rolagem horizontal, para nomes longos serem cortados em vez de empurrar o botão para fora. Testes: `LibraryViewTests.CliqueDireito_EmQualquerPontoDaLinha_AbreAcoesDoCurso`, `BotaoDeAcoes_AbreMenuEItemSemAcoesNaoAbreODoCurso` e `PastaIndisponivel_MostraLocalizarPastaNaLinha`.
- **Aula indisponível recebe foco e menu**, em vez de ficar desabilitada na árvore, para dar para remover um vídeo avulso que sumiu. Ela só não vira a aula atual (CA10).
- **Ordem dos cursos:** natural pelo título, com "Vídeos avulsos" por último. A coluna `Position` dos cursos guarda a ordem do scan, mas a árvore não a usa.
- **Ids com `ValueGeneratedNever`:** gerados no código (Guid v7). Sem isso, o EF tentava `UPDATE` em entidades novas encontradas por navegação.
- **Validação de vídeos para no primeiro arquivo com problema**, e a faixa de erro mostra uma mensagem por vez, citando o arquivo.
- **Teclado:** com foco mas sem seleção, a primeira seta do `TreeView` do Avalonia só seleciona o item com foco; as seguintes navegam.
- **`longPathAware`** adicionado ao `app.manifest` (faltava).
- **Upgrade real conferido:** o app aberto sobre o banco da spec 0004 fez o backup de atualização e aplicou a migration `Biblioteca`.

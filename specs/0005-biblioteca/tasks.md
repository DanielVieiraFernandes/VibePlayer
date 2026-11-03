# 0005 — Tarefas

Marque cada tarefa ao concluir.

## Implementação

- [ ] T1 — FluentValidation no `Core` (pacote, licença em `THIRD-PARTY-NOTICES.md`).
- [ ] T2 — `NaturalStringComparer`.
- [ ] T3 — `IFileSystemReader` (Core) e `PhysicalFileSystemReader` (Infrastructure), sem seguir links e pulando ocultos/sistema.
- [ ] T4 — `LibraryScanner` (filtros, módulos aninhados, ordem, NFC) e `LibraryDetection`.
- [ ] T5 — `LibraryFolderValidator`.
- [ ] T6 — Entidades, configurações e migration `Biblioteca`.
- [ ] T7 — `CourseLibrary`: preview das duas interpretações, add de pasta e de vídeos (curso novo ou "Vídeos avulsos"), remove (curso e aula avulsa), relocate, leitura da árvore e sincronização com progresso, cancelamento e uma atualização por vez.
- [ ] T8 — `IFilePicker` (pasta e vídeos) sobre o `StorageProvider`; `ConfirmDialog`.
- [ ] T9 — `LibraryViewModel`, `AddLibraryFolderViewModel`, `AddVideosViewModel`, `LibraryView` (árvore, ações, progresso, faixa de erro, menu com Remover/Localizar pasta), `AddLibraryFolderDialog` e `AddVideosDialog`.
- [ ] T10 — Aula atual no `PlayerViewModel` e no `PlayerPanel`.
- [ ] T11 — Atualização da biblioteca em segundo plano ao abrir o app e cancelamento ao fechar.
- [ ] T12 — Textos em `Strings.resx`, nomes acessíveis, `longPathAware` no manifesto se faltar.
- [ ] T13 — CLAUDE.md (estrutura e fluxo da biblioteca) e capturas de tela para o PR.

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1 | Unitário + E2E | `LibraryDetectionTests.*`; `LibraryViewTests.AdicionarPasta_PerguntaInterpretacaoComPrevia` |
| CA2 | Unitário | `NaturalStringComparerTests.*` |
| CA3 | Unitário | `LibraryScannerTests.IgnoraNaoVideosOcultosSistemaELinks` e `ModulosAninhados_ViramNomeComposto` |
| CA4 | Unitário | `LibraryFolderValidatorTests.*` |
| CA5 | E2E | `LibraryTests.Reabertura_MantemBiblioteca` |
| CA6 | E2E | `LibraryTests.Atualizar_NovoAparece_RemovidoFicaIndisponivel_VoltaComMesmoId` |
| CA7 | E2E | `LibraryTests.RaizInacessivel_CursosIndisponiveisRestoFunciona` |
| CA8 | E2E | `LibraryTests.RemoverCurso_ApagaRegistrosENaoTocaArquivos` |
| CA9 | E2E | `LibraryTests.PastaGrande_AtualizaEmSegundoPlanoComProgresso` e `SubpastaSemPermissao_RegistraEContinua` |
| CA10 | E2E | `LibraryViewTests.SelecionarAula_MostraTituloNoPlayer` e `AulaIndisponivel_NaoPodeSerSelecionada` |
| CA12 | Unitário + E2E | `VideoSelectionValidatorTests.*`; `LibraryTests.AdicionarVideos_CursoNovoOuAvulsos` e `RemoverAulaAvulsa` |
| CA13 | E2E | `LibraryTests.LocalizarPasta_ReconectaComMesmosIds` e `LocalizarPasta_SemAulasConhecidas_PedeConfirmacao` |
| CA11 | E2E | `AccessibilityTests.ElementosInterativos_TemNomeAcessivel` (estendido com biblioteca preenchida) e `LibraryViewTests.Teclado_NavegaESeleciona` |

A biblioteca de exemplo é gerada pelo próprio teste em pasta temporária (arquivos `.mp4` vazios bastam: nesta spec ninguém lê o conteúdo do vídeo).

## Documentação

- [ ] CLAUDE.md (T13)
- [ ] `DESIGN.md`: faixa de erro, árvore e item indisponível
- [ ] Wiki: "Adicionando cursos" (pastas, vídeos avulsos, indisponível, Localizar pasta)
- [ ] `docs/backlog.md` com a ideia de atualização automática do app

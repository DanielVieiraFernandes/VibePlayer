# 0003 — Tarefas

## Implementação

- [x] T1 — Escrever `DESIGN.md` com valores finais de cores, tipografia, espaçamento, raios, tamanhos, estados, movimento e acessibilidade.
- [x] T2 — `Styles/Tokens.axaml` (claro e escuro) e `Styles/Controls.axaml`; registrar no `VibePlayerApp.axaml`.
- [x] T3 — `Resources/Strings.resx` com geração de classe no build.
- [x] T4 — Adicionar FluentIcons.Avalonia e registrar a licença em `THIRD-PARTY-NOTICES.md`.
- [x] T5 — Shell: `MainWindow` com barra superior, grid de 2 colunas (player | painel lateral), divisória e limites de largura; `MainWindowViewModel` com visibilidade/largura do painel, aba selecionada, comandos e atalhos.
- [x] T6 — `PlayerPanel`, `SidePanel` com as abas e as Views de cada aba (`LibraryView`, `ChatView`, `SummaryView`, `KeyPointsView`) com estados vazios e controles desabilitados; ViewModels correspondentes; dados de exemplo só para o previewer.
- [x] T7 — `INotificationService` + integração com `GlobalExceptionHandler` (thread de UI e limite de uma notificação a cada 5 s).
- [x] T8 — Nomes acessíveis (`AutomationProperties.Name`) em todos os elementos interativos e ordem de Tab.
- [x] T9 — Capturas de tela nos temas claro e escuro para o PR.
- [x] T10 — Atualizar CLAUDE.md (seção UI) com a estrutura de Views, tokens e textos.

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1 | Revisão | `DESIGN.md` no PR |
| CA2 | Unitário | `ViewConventionsTests.Views_NaoUsamCorOuTamanhoDeFonteLiteral` (+ testes das próprias regras) |
| CA3 | E2E | `DesignTokenTests.TodoTokenDeCor_ExisteNosTemasClaroEEscuro` |
| CA4 | E2E | `DesignTokenTests.ParesDeTextoEFundo_AtendemContrasteWcagAA` (38 pares × 2 temas) |
| CA5 | E2E | `MainWindowLayoutTests.Abertura_MostraRegioesComEstadosVazios` |
| CA6 | E2E | `MainWindowLayoutTests.BotaoEAtalho_AlternamPainelLateral` e `AtalhosDeAba_AbremPainelNaAbaPedida` |
| CA7 | E2E | `MainWindowLayoutTests.TamanhoMinimo_SemCortes` |
| CA8 | E2E | `UnhandledExceptionTests.ExcecaoEmComandoDaUi_MostraNotificacaoERegistraErro` |
| CA9 | E2E + captura | `AccessibilityTests.ElementosInterativos_TemNomeAcessivel`; foco visível conferido em `docs/screenshots/foco-teclado.png` |
| CA10 | Manual | Capturas em `docs/screenshots/`, referenciadas no PR |

Também: `MainWindowViewModelTests` (abrir/fechar, abas, limites de largura) e `NotificationThrottleTests` (uma notificação a cada 5 s).

## Documentação

- [x] `DESIGN.md` (T1)
- [x] CLAUDE.md (T10)
- [x] Wiki — não se aplica

## Desvios em relação ao design

- **Divisória com `Thumb`, não `GridSplitter`:** o `GridSplitter` grava larguras fixas na coluna (o risco previsto no design). A divisória é um `Thumb` que repassa o arraste ao `MainWindowViewModel.ResizeSidePanel`; a janela informa a largura disponível (`UpdateAvailableWidth`) para o painel encolher e o player nunca ficar abaixo de 400 px.
- **Atalhos com `HotkeyConfiguration`, não `OnPlatform`:** os `KeyBinding`s são criados em código com `PlatformSettings.HotkeyConfiguration.CommandModifiers`, que já dá Ctrl no Windows/Linux e ⌘ no macOS.
- **Tokens a mais:** `AccentTextBrush` (o violeta do `AccentBrush` não atinge 4,5:1 como texto pequeno no tema escuro) e `VideoTextBrush`/`VideoTextSecondaryBrush` (texto sobre a área do vídeo, igual nos dois temas).
- **Botões sem função ficam desabilitados** ("Adicionar pasta de cursos", configurações, controles do player, caixa do chat), com dica "Disponível em breve", em vez de botões habilitados que não fazem nada.
- **`AppComposition.BuildServices` ganhou o parâmetro `overrides`** (só para testes): os E2E trocam o `TimeProvider` por um relógio que avança sozinho, para o limite de 5 s entre notificações não fazer um teste depender da ordem dos outros. O limite em si é testado em `NotificationThrottleTests`.
- **Testes unitários referenciam o projeto App**, para testar ViewModels e o throttle sem subir o Avalonia.

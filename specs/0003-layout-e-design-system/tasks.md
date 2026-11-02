# 0003 — Tarefas

## Implementação

- [ ] T1 — Escrever `DESIGN.md` com valores finais de cores, tipografia, espaçamento, raios, tamanhos, estados, movimento e acessibilidade.
- [ ] T2 — `Styles/Tokens.axaml` (claro e escuro) e `Styles/Controls.axaml`; registrar no `VibePlayerApp.axaml`.
- [ ] T3 — `Resources/Strings.resx` com geração de classe no build.
- [ ] T4 — Adicionar FluentIcons.Avalonia e registrar a licença em `THIRD-PARTY-NOTICES.md`.
- [ ] T5 — Shell: `MainWindow` com barra superior, grid de 3 colunas, divisórias e limites de largura; `MainWindowViewModel` com visibilidade/largura dos painéis, comandos e atalhos.
- [ ] T6 — `LibraryPanel`, `PlayerPanel` e `AssistantPanel` com estados vazios e controles desabilitados; ViewModels correspondentes; dados de exemplo só para o previewer.
- [ ] T7 — Regra de abertura < 1200 px recolhendo o assistente.
- [ ] T8 — `INotificationService` + integração com `GlobalExceptionHandler` (thread de UI e limite de uma notificação a cada 5 s).
- [ ] T9 — Nomes acessíveis (`AutomationProperties.Name`) em todos os elementos interativos e ordem de Tab.
- [ ] T10 — Capturas de tela nos temas claro e escuro para o PR.
- [ ] T11 — Atualizar CLAUDE.md (seção UI) com a estrutura de Views, tokens e textos.

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1 | Revisão | `DESIGN.md` no PR |
| CA2 | Unitário | `ViewConventionsTests.Views_NaoUsamCorOuTamanhoDeFonteLiteral` |
| CA3 | E2E | `DesignTokenTests.TodoTokenDeCor_ExisteNosTemasClaroEEscuro` |
| CA4 | E2E | `DesignTokenTests.ParesDeTextoEFundo_AtendemContrasteWcagAA` |
| CA5 | E2E | `MainWindowLayoutTests.Abertura_MostraRegioesComEstadosVazios` |
| CA6 | E2E | `MainWindowLayoutTests.BotaoEAtalho_AlternamBibliotecaEAssistente` |
| CA7 | E2E | `MainWindowLayoutTests.JanelaEstreita_AbreComAssistenteRecolhido` e `TamanhoMinimo_SemCortes` |
| CA8 | E2E | `UnhandledExceptionTests.ExcecaoEmComandoDaUi_MostraNotificacaoERegistraErro` |
| CA9 | E2E + manual | `AccessibilityTests.ElementosInterativos_TemNomeAcessivel`; foco visível conferido manualmente |
| CA10 | Manual | Capturas anexadas ao PR |

## Documentação

- [ ] `DESIGN.md` (T1)
- [ ] CLAUDE.md (T11)
- [ ] Wiki — não se aplica

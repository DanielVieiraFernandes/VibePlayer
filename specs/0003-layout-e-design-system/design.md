# 0003 — Design técnico

## Abordagem

```
DESIGN.md                                  Padrão visual (documento de referência, na raiz)
src/VibePlayer.App/
  Styles/
    Tokens.axaml        Cores (ThemeDictionaries Light/Dark), tipografia, espaçamentos, raios, tamanhos
    Controls.axaml      Estilos por classe: Button.primary/.ghost/.icon, TextBlock.title/.body/.caption, painéis
  Resources/
    Strings.resx        Textos da UI (pt-BR), com classe gerada no build para uso via {x:Static}
  Views/
    MainWindow.axaml    Shell: barra superior + grid de 2 colunas (player | painel lateral) com divisória
    Shell/TopBar.axaml
    Shell/SidePanel.axaml           Abas Aulas | Chat | Resumo | Pontos principais
    Player/PlayerPanel.axaml
    Library/LibraryView.axaml       Conteúdo da aba Aulas
    Assistant/ChatView.axaml, SummaryView.axaml, KeyPointsView.axaml
  ViewModels/
    MainWindowViewModel  Visibilidade e largura do painel lateral, aba selecionada, comandos
    LibraryViewModel, PlayerViewModel, AssistantViewModel (só estado vazio por enquanto)
  Services/
    INotificationService + implementação sobre o WindowNotificationManager do Avalonia
```

### Layout da janela

```
┌──────────────────────────── Barra superior (48 px) ────────────────────────────┐
│ VibePlayer                                                     [◨ Painel] [⚙] │
├────────────────────────────────────────────────────┬┬──────────────────────────┤
│ Área do vídeo (fundo sempre escuro)                ││ Aulas|Chat|Resumo|Pontos │
│                                                    ││ 380 px (320–560)         │
│               "Selecione uma aula"                 ││                          │
│                                                    ││ estado vazio da aba      │
├────────────────────────────────────────────────────┤│                          │
│ ▶  00:00 ━━━━━━━━━━━━━━━━━━━━━━━━ 00:00  🔊  1x  ⛶ ││ [Adicionar…] [mensagem…] │
└────────────────────────────────────────────────────┴┴──────────────────────────┘
                                                      ↑ divisória    notificações ↘
```

- Player à esquerda porque é o foco da tela. Biblioteca e assistente são apoio e dividem o mesmo painel, em abas, já que raramente são usados ao mesmo tempo (o usuário escolhe a aula, depois conversa sobre ela).
- O painel abre na aba *Aulas*. A aba escolhida fica guardada só durante a sessão (lembrar entre aberturas depende da persistência).
- Painel recolhido: a coluna vai a zero e a divisória some; ao reabrir, volta à última largura usada na sessão.
- Player com largura mínima de 400 px. Na janela mínima (960 px) com o painel na largura padrão sobram 580 px para o player, então não há recolhimento automático em janelas estreitas.
- Atalhos (no macOS, **⌘** no lugar de Ctrl, via `OnPlatform`):
  - **Ctrl+B** mostra/oculta o painel lateral;
  - **Ctrl+1** a **Ctrl+4** abrem o painel direto em *Aulas*, *Chat*, *Resumo* e *Pontos principais*.

### Tokens

Nomes semânticos, nunca pela cor (`TextSecondaryBrush`, não `Gray500Brush`), para que o tema troque sem mudar as Views.

| Grupo | Tokens |
|---|---|
| Fundo | `AppBackground`, `SurfaceBackground`, `SurfaceRaisedBackground`, `VideoBackground` (quase preto nos dois temas) |
| Borda | `BorderSubtle`, `BorderStrong` |
| Texto | `TextPrimary`, `TextSecondary`, `TextDisabled`, `TextOnAccent` |
| Destaque | `Accent`, `AccentHover`, `AccentPressed`, `AccentSubtle` (seleção) |
| Estado | `Error`, `Warning`, `Success`, `Info` e variantes `*Subtle` |
| Foco | `FocusRing` |
| Tipografia (Inter) | Display 28/36, Title 20/28, Subtitle 16/24, Body 14/20, Caption 12/16 (tamanho/altura de linha) |
| Espaçamento | 4, 8, 12, 16, 24, 32, 48 (`SpaceXs` … `Space3Xl`) |
| Raio | 4 (controles pequenos), 8 (campos, cartões), 12 (painéis, diálogos) |
| Tamanhos | barra superior 48; botão 36 (compacto 32); botão de ícone 32×32 com ícone 20; ícone pequeno 16 |

Os valores finais de cada cor ficam no `DESIGN.md`, escolhidos para passar no teste de contraste (CA4).

### Notificações de erro

`GlobalExceptionHandler` passa a receber `INotificationService` e, depois de registrar no log, mostra a notificação. Dois cuidados:
- a exceção de task não observada chega na thread do coletor de lixo; a notificação é enviada para a thread de UI com `Dispatcher.UIThread.Post`;
- no máximo uma notificação de erro a cada 5 segundos, para uma falha repetida não encher a tela (o log continua registrando todas).

### Textos

`Strings.resx` com geração da classe no próprio build (`StronglyTypedLanguage` no `.csproj`), usada nas Views com `{x:Static}`. Funciona com `dotnet build` em qualquer SO, sem depender do Visual Studio.

## Decisões

| Decisão | Motivo | Alternativas descartadas |
|---|---|---|
| Tokens semânticos em `ThemeDictionaries` | Tema claro/escuro troca automaticamente com o sistema, sem código | Duas folhas de estilo separadas |
| Ícones FluentIcons.Avalonia | MIT, compatível com Avalonia 12, mesmo estilo do tema Fluent já usado | Material.Icons (estilo destoa do Fluent); Lucide e Projektanker (ainda sem suporte ao Avalonia 12) |
| Barra de título do sistema | Funciona igual nos três SOs sem tratar arrastar, botões e snap à mão | Barra de título própria (mais bonita, bem mais código por SO) |
| Fundo do vídeo sempre escuro | Vídeo sobre fundo claro cansa a vista e destoa | Seguir o tema |
| `WindowNotificationManager` do Avalonia | Já vem no Avalonia, sem dependência nova | Componente de notificação próprio |
| Testes de tokens e contraste automatizados | Garantem CA2–CA4 a cada PR, sem depender de revisão visual | Só revisão manual |

## Mudanças em dados

Nenhuma.

## Dependências novas

| Pacote | Projeto | Licença |
|---|---|---|
| FluentIcons.Avalonia 2.1.x (ícones Fluent UI System Icons) | App | MIT (ícones: MIT) |

## Logs e erros

- Notificação de erro não substitui o log: o registro com stack trace continua igual.
- Alternar o painel e trocar de aba registram em `Debug` (útil para depurar o layout, silencioso em release).

## Riscos

- **Contraste com o violeta:** cores de destaque saturadas costumam falhar em texto pequeno no tema escuro. Mitigação: o teste de contraste define os tons finais; o `Accent` como texto só é usado em tamanho grande ou ícone.
- **Divisória + recolhimento:** o `GridSplitter` grava larguras fixas nas colunas, o que conflita com recolher/reabrir. Mitigação: a largura do painel fica no ViewModel e a View só aplica.
- **Fontes na CI:** a Inter já vem embutida (`Avalonia.Fonts.Inter`), então capturas e medidas são iguais nos três SOs.

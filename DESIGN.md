# DESIGN.md — Design system do VibePlayer

Padrão visual do app. Toda tela segue este documento; os valores vivem em
[`src/VibePlayer.App/Styles/Tokens.axaml`](src/VibePlayer.App/Styles/Tokens.axaml) (tokens) e
[`Styles/Controls.axaml`](src/VibePlayer.App/Styles/Controls.axaml) (estilos por classe). Mudou um valor aqui, mude lá, e vice-versa.

## Princípios

1. **O vídeo é o centro.** A área do vídeo ocupa o maior espaço e fica sempre escura; o resto da interface é apoio e não disputa atenção com ele.
2. **Calmo e legível.** Poucas cores, muito espaço, hierarquia feita com tamanho e peso de fonte, não com cor.
3. **Uma cor de destaque.** O violeta marca o que é interativo ou selecionado. Não use para decoração.
4. **Acessível por padrão.** Contraste WCAG AA nos dois temas, tudo alcançável pelo teclado, todo controle com nome acessível.
5. **Tokens, nunca valores soltos.** Views não contêm cores, tamanhos de fonte nem espaçamentos literais (verificado por `ViewConventionsTests`).

## Layout

```
┌──────────────────────────── Barra superior (48) ─────────────────────────────┐
│ ▣ VibePlayer                                                   [◨] [⚙]      │
├──────────────────────────────────────────────────┬┬──────────────────────────┤
│ Área do vídeo (sempre escura, ocupa o restante)  ││ Aulas│Chat│Resumo│Pontos │
│                                                  ││ Painel lateral           │
│                                                  ││ 380 (320–560)            │
├──────────────────────────────────────────────────┤│                          │
│ ▶ 00:00 ━━━━━━━━━━━━━━━━━━━━━━ 00:00  🔊  1x  ⛶ ││                          │
└──────────────────────────────────────────────────┴┴──────────────────────────┘
                                         divisória (4) ↑         notificações ↘
```

| Região | Regra |
|---|---|
| Janela | Mínimo 960 × 600; abre com 1280 × 800. Barra de título do sistema. |
| Player | Largura mínima 400; ocupa o espaço que sobra. |
| Painel lateral | Padrão 380, de 320 a 560, nunca deixando o player abaixo de 400. Recolhe por completo (Ctrl/⌘+B); ao reabrir, volta à última largura. |
| Abas do painel | *Aulas*, *Chat*, *Resumo*, *Pontos principais*, nesta ordem; Ctrl/⌘+1…4 abre direto em cada uma. |
| Notificações | Canto inferior direito, no máximo 3 ao mesmo tempo. |

## Cores

Os nomes descrevem o **papel**, não a cor. Nas Views, sempre `{DynamicResource NomeBrush}` (Dynamic, para trocar junto com o tema do sistema).

| Token | Claro | Escuro | Uso |
|---|---|---|---|
| `AppBackgroundBrush` | `#F4F5F8` | `#0F1115` | Fundo da janela |
| `SurfaceBackgroundBrush` | `#FFFFFF` | `#16181D` | Barra superior, painel lateral, barra do player |
| `SurfaceRaisedBackgroundBrush` | `#FFFFFF` | `#1F222A` | Cartões, menus, diálogos |
| `BorderSubtleBrush` | `#E3E5EA` | `#2A2E37` | Divisórias e bordas decorativas |
| `BorderStrongBrush` | `#7E8494` | `#6B7180` | Borda de campos e controles (precisa de 3:1) |
| `TextPrimaryBrush` | `#16181D` | `#ECEDF1` | Texto principal |
| `TextSecondaryBrush` | `#555B6A` | `#A3A8B5` | Descrições, rótulos, tempos |
| `TextDisabledBrush` | `#A0A5B1` | `#5C6270` | Texto e ícones desabilitados (sem exigência de contraste) |
| `TextOnAccentBrush` | `#FFFFFF` | `#FFFFFF` | Texto sobre o destaque |
| `AccentBrush` | `#6D4AFF` | `#6D4AFF` | Fundo de botão primário, indicadores, slider |
| `AccentHoverBrush` | `#5E3BF0` | `#5E3BF0` | Hover do primário |
| `AccentPressedBrush` | `#4F2DDB` | `#4F2DDB` | Pressionado do primário |
| `AccentSubtleBrush` | `#EEEAFF` | `#2A2350` | Fundo de item selecionado, toggle ligado |
| `AccentTextBrush` | `#5436E0` | `#A894FF` | Destaque usado como texto ou ícone pequeno |
| `ErrorBrush` / `ErrorSubtleBrush` | `#B42318` / `#FDECEA` | `#FF8A8A` / `#3A1D20` | Erros |
| `WarningBrush` / `WarningSubtleBrush` | `#8A5100` / `#FFF4E0` | `#F5C04E` / `#3A2E12` | Avisos |
| `SuccessBrush` / `SuccessSubtleBrush` | `#18794E` / `#E6F6EE` | `#4ADE9B` / `#123326` | Sucesso |
| `InfoBrush` / `InfoSubtleBrush` | `#1D5FC2` / `#E8F1FD` | `#7AB8FF` / `#142A45` | Informação |
| `FocusRingBrush` | `#6D4AFF` | `#A894FF` | Indicador de foco do teclado |
| `VideoBackgroundBrush` | `#050506` | `#050506` | Área do vídeo |
| `VideoTextBrush` / `VideoTextSecondaryBrush` | `#ECEDF1` / `#A3A8B5` | iguais | Texto sobre a área do vídeo |

**Vídeo sempre escuro:** vídeo sobre fundo claro cansa a vista e as bordas do quadro "brilham". Por isso a área do vídeo e seus textos têm os mesmos valores nos dois temas.

**Destaque como texto:** o `AccentBrush` não tem contraste para texto pequeno sobre as superfícies; para texto ou ícone pequeno em destaque use `AccentTextBrush`.

**Cor de destaque do Fluent:** o `FluentTheme` usa o mesmo violeta (`VibePlayerApp.axaml`), para que controles nativos (abas, slider, foco) não peguem a cor de destaque do sistema do usuário.

### Contraste

Todos os pares abaixo passam nos dois temas; `DesignTokenTests` repete esta tabela e falha se um tom mudar para pior.

| Primeiro plano | Fundo | Mínimo |
|---|---|---|
| `TextPrimary`, `TextSecondary`, `AccentText` | `AppBackground`, `SurfaceBackground`, `SurfaceRaisedBackground` | 4,5:1 |
| `Accent`, `FocusRing`, `BorderStrong` | `AppBackground`, `SurfaceBackground`, `SurfaceRaisedBackground` | 3:1 |
| `TextOnAccent` | `Accent`, `AccentHover`, `AccentPressed` | 4,5:1 |
| `TextPrimary`, `TextSecondary` | `AccentSubtle` | 4,5:1 |
| `Error`, `Warning`, `Success`, `Info` | a própria variante `*Subtle` e `SurfaceBackground` | 4,5:1 |
| `TextPrimary` | cada variante `*Subtle` | 4,5:1 |
| `VideoText`, `VideoTextSecondary` | `VideoBackground` | 4,5:1 |

Novo par de cores na interface → acrescente aqui e em `DesignTokenTests.Pairs`.

## Tipografia

Fonte **Inter** (embutida pelo `Avalonia.Fonts.Inter`; mesma aparência nos três SOs).

| Classe (`TextBlock`) | Tamanho / linha | Peso | Uso |
|---|---|---|---|
| `display` | 28 / 36 | SemiBold | Títulos de tela inteira (raro) |
| `title` | 20 / 28 | SemiBold | Título principal de uma região (ex.: "Selecione uma aula") |
| `subtitle` | 16 / 24 | SemiBold | Título de estado vazio, nome do app na barra |
| `body` (padrão) | 14 / 20 | Regular | Texto corrido |
| `caption` | 12 / 16 | Regular | Tempos, metadados |

Modificadores: `secondary` (cor `TextSecondary`), `tabular` (algarismos de largura fixa, para tempos que contam sem "pular").

O tamanho e a cor padrão vêm da janela por herança. Não crie estilo global para `TextBlock`: ele atingiria os textos de dentro dos templates do Fluent e quebraria as cores de hover e de desabilitado.

## Espaçamento

Grade de 4 px.

| Token | Valor | Uso típico |
|---|---|---|
| `SpaceXs` | 4 | Entre botões de ícone vizinhos |
| `SpaceSm` | 8 | Ícone e texto num botão; campo e botão lado a lado |
| `SpaceMd` | 12 | Itens de um estado vazio |
| `SpaceLg` | 16 | Respiro interno de painéis (`PaddingPanel`) |
| `SpaceXl` | 24 | Margem de estados vazios (`PaddingEmptyState`) |
| `Space2Xl` | 32 | Separação entre seções |
| `Space3Xl` | 48 | Respiro de telas inteiras |

`Spacing` usa os doubles; `Margin`/`Padding` usam as `Thickness` nomeadas (`PaddingPanel`, `PaddingEmptyState`, `PaddingBar`, `PaddingButton`, `PaddingTab`, `BorderTop`, `BorderBottom`).

## Raios e elevação

| Token | Valor | Uso |
|---|---|---|
| `RadiusSm` | 4 | Botões de ícone, botões compactos |
| `RadiusMd` | 8 | Botões, campos, cartões |
| `RadiusLg` | 12 | Painéis flutuantes, diálogos |

Elevação é feita com **superfície** (`SurfaceRaised`) e borda sutil, não com sombras: sombras somem no tema escuro e custam desempenho de renderização.

## Ícones

[Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons) via `FluentIcons.Avalonia` (`<ic:SymbolIcon Symbol="Play" />`), no estilo *Regular*. Tamanhos: `IconSize` 20 (padrão), `IconSizeSmall` 16, `IconSizeEmptyState` 40. Ícone sem texto ao lado sempre tem `AutomationProperties.Name` e, quando útil, `ToolTip.Tip`.

## Componentes

| Componente | Classe | Medidas | Observações |
|---|---|---|---|
| Barra superior | — | altura 48 (`TopBarHeight`) | Superfície com borda inferior sutil |
| Barra do player | — | altura 56 (`PlayerControlsHeight`) | Superfície com borda superior sutil |
| Botão primário | `Button.primary` | altura 36 | Uma ação principal por região |
| Botão padrão | `Button` | altura 36, raio 8 | Estilo do Fluent |
| Botão de ícone | `Button.icon` / `ToggleButton.icon` | 32 × 32, ícone 20 | Sem fundo; toggle ligado usa `AccentSubtle` |
| Botão compacto de texto | `Button.ghost` | altura 32 | Ex.: velocidade "1x" |
| Campo de texto | `TextBox` | altura 36, raio 8 | Placeholder diz o que digitar |
| Abas | `TabItem` | altura 40, texto 14 SemiBold | Quatro abas cabem em 320 |
| Divisória | `Thumb.splitter` | largura 4 | `BorderSubtle`; `Accent` no hover |
| Estado vazio | `StackPanel.emptyState` | largura máx. 320 | Ícone 40 em `AccentText`, título `subtitle`, explicação `body secondary`, ação opcional |
| Notificação | `WindowNotificationManager` | — | Erro: "Algo deu errado" + "Os detalhes foram registrados no log." |
| Faixa de erro | `Border.errorBanner` | borda esquerda 3 | Erro esperado dentro de um painel (ex.: pasta inválida): fundo `ErrorSubtle`, texto `TextPrimary`, ícone `Error`, botão de fechar. Some na próxima ação |
| Barra de ações de painel | `Border.toolbar` | — | Botões de ícone alinhados à direita, borda inferior sutil |
| Árvore da biblioteca | `TreeView` + `StackPanel.libraryItem` | — | Título `body` e, quando houver, segunda linha `caption secondary` (contagem de aulas, "Indisponível", "Pasta não encontrada") |
| Item indisponível | `TextBlock.unavailable` | — | Título em `TextDisabled`; continua com foco e menu (para remover ou localizar), mas não vira a aula atual |
| Ações de uma linha | `Button.rowActions` ("⋯") + menu | 32 × 32, à direita da linha | Sempre visível em itens com ações, para quem não conhece o clique direito. O clique direito e a tecla Menu abrem o mesmo menu em qualquer ponto da linha. Itens de menu com ícone (`Delete` para remover, `FolderSearch` para localizar) |
| Barra do player | `Border.playerControls` | altura 56 | Anterior, reproduzir, próxima, tempo, posição, duração, mudo, volume, velocidade, tela cheia. Abaixo de 560 de largura fica compacta (sem anterior/próxima e sem o controle de volume). Em tela cheia fica sobre o vídeo e some após 3 s com o ponteiro parado (tocando) |
| Tempo sob o ponteiro | `Border.seekPreview` | texto `caption` | Acima da barra, centralizado no ponteiro e não no ponto atual do vídeo; acompanha o mouse e o arraste |
| Pôster | `Image.poster` + `Button.bigPlay` | botão 64 | Miniatura escurecida, título, curso e botão de reproduzir, enquanto a aula não começou a tocar |
| Contagem da próxima aula | `Border.countdown` | raio 12 | Canto inferior direito do vídeo, com "Ir agora" e "Cancelar" |
| Miniatura | `Border.thumbnail` | 64 × 36, raio 4 | À esquerda da aula e do curso (capa); ícone `Video` enquanto não existe |
| Progresso da aula | `SymbolIcon.completed` / `.inProgress` | ícone 16 | `CheckmarkCircle` em `Success`; `CircleHalfFill` em `AccentText`; sempre com o texto ao lado ("Concluída", "Em andamento") |
| Link de ação | `Button.link` | sem fundo nem borda | Texto `AccentText`, sublinhado no hover. Para a saída óbvia de um estado (ex.: "Localizar pasta…" numa pasta não encontrada) |
| Selo | `Border.badge` | — | Texto `caption` sobre `AccentSubtle` (ex.: "sugerido") |
| Caixa de prévia | `Border.previewBox` | raio 8 | Fundo `AppBackground` com borda sutil, dentro de diálogos |
| Diálogo | `Window` modal | largura 440–520, altura pelo conteúdo | Título `subtitle`, ações à direita (Cancelar, depois a principal). Confirmações destrutivas têm Cancelar como padrão do Enter |

## Estados

| Estado | Regra |
|---|---|
| Hover | Escurece/realça o fundo (`AccentHover` no primário; fundo sutil do Fluent nos demais) |
| Pressionado | `AccentPressed` no primário |
| Foco (teclado) | Anel de foco do Fluent na cor de destaque; nunca remova |
| Desabilitado | Texto/ícone `TextDisabled`. Botões sem fundo continuam sem fundo. Função ainda não disponível → desabilitado com dica "Disponível em breve" |
| Selecionado | Indicador ou fundo `AccentSubtle`, texto `TextPrimary`/`AccentText` |
| Vazio | Toda região sem conteúdo mostra um estado vazio que diz o que fazer |
| Erro | Notificação de erro; detalhes só no log |

## Movimento

Use as transições padrão do Fluent (hover, abas). Nada de animação decorativa; transições novas com no máximo 150 ms e sem movimento quando a região é o vídeo.

## Acessibilidade

- Contraste AA nos dois temas (tabela acima, verificada por teste).
- Todo elemento interativo alcançável por Tab, com foco visível, e com nome acessível: `AutomationProperties.Name` em ícones e controles sem texto (verificado por `AccessibilityTests`).
- Atalhos usam o modificador do sistema (Ctrl no Windows/Linux, ⌘ no macOS).
- Não comunicar estado só por cor: o toggle ligado também muda o fundo, o erro também tem título.

## Textos

Todos os textos da interface ficam em [`Resources/Strings.resx`](src/VibePlayer.App/Resources/Strings.resx) (pt-BR), usados nas Views com `{x:Static res:Strings.Chave}`. Chaves no formato `Area_Elemento`. Tom: direto, em segunda pessoa implícita, verbos no imperativo ("Selecione uma aula", "Adicione a pasta…"); erros sem jargão técnico.

## Exemplo

```xml
<StackPanel Classes="emptyState" Margin="{StaticResource PaddingEmptyState}">
    <ic:SymbolIcon Symbol="Library" />
    <TextBlock Classes="subtitle" Text="{x:Static res:Strings.Library_EmptyTitle}" />
    <TextBlock Classes="body secondary" Text="{x:Static res:Strings.Library_EmptyDescription}" />
    <Button Classes="primary" Content="{x:Static res:Strings.Library_AddFolder}" />
</StackPanel>
```

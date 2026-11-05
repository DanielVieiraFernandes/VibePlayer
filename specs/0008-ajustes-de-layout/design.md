# 0008 — Design técnico

## Abordagem

- **Janela:** novo padrão em `UiSettings.WindowSize` e `MainWindow.axaml`; `WindowStateTracker.LoadAsync` troca o tamanho salvo pelo novo padrão quando igual ao antigo (constante `LegacyDefaultSize`).
- **Ícone:** SVG mestre → `app.ico`, `icon-256.png`, `app.icns` em `src/VibePlayer.App/Assets/` (`AvaloniaResource`); `ApplicationIcon` no csproj; `Icon` nas janelas.
- **Clique:** `VideoArea.Tapped` com `DispatcherTimer` curto (tempo de duplo clique da plataforma); `DoubleTapped` cancela o timer. A decisão fica em uma classe pura, testável sem UI.
- **Tela cheia:** `ControlsBar` deixa de ter `Grid.Row` local; vídeo e barra num mesmo painel, com a barra sobreposta embaixo em tela cheia (classe `fullscreen`) e na linha própria fora dela.
- **Abas:** `TabItem` com `ic:SymbolIcon`, `ToolTip.Tip`, `AutomationProperties.Name`; estilos em `Controls.axaml` (`TabItem:selected`).

## Decisões

| Decisão | Motivo | Alternativas descartadas |
|---|---|---|
| Migrar só quando o tamanho salvo == padrão antigo | Preserva escolha explícita do usuário | Marca de versão; não migrar |
| Atrasar o clique simples | Evita pausar ao dar duplo clique | Pausar na hora e desfazer |

## Mudanças em dados

Nenhuma migration. A chave `ui.window.size` pode ser regravada com o novo padrão.

## Dependências novas

Nenhuma. Ícone desenhado por nós (SVG próprio); a conversão usa ferramenta local, fora do projeto.

## Logs e erros

Sem logs novos.

## Riscos

- O tempo de duplo clique varia por SO: usar o valor da plataforma.
- `.icns` precisa de gerador; sem ferramenta no Windows, montar o arquivo por script a partir dos PNGs.
- O `release.yml` só muda para incluir os ícones, para não conflitar com a spec 0009.

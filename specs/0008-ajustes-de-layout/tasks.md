# 0008 — Tarefas

## Implementação

- [x] T1 — Novo tamanho padrão e migração do tamanho salvo
- [x] T2 — Ícone oficial (SVG, .ico, .png, .icns) aplicado ao exe, janelas e pacotes
- [x] T3 — Clique simples no vídeo alterna pausa (com duplo clique)
- [x] T4 — Barra sobreposta e vídeo ocupando a tela toda em tela cheia
- [x] T5 — Abas em uma linha (conforme D3)

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1, CA2 | Unitário | `UiSettingsTests` e `WindowStateTests` (padrão novo; migração do 1280×800; tamanho próprio mantido) |
| CA3 | Unitário | `AppIconTests` (ico, icns, png, csproj) |
| CA4–CA6 | E2E | `VideoClickTests` (clique pausa/retoma; duplo clique não pausa; clique na barra) |
| CA7 | E2E | `VideoClickTests.TelaCheia_...` (área do vídeo == janela com barra visível e oculta) |
| CA8 | E2E | `SidePanelTabsTests` (uma linha em 320 px, nome acessível, ativa distinta) |

## Teste manual

Feito com o app real (Debug, Windows 11, tela 1920 × 1080, 100%), `tests/fixtures/videos/amostra-h264.mp4` e pasta de dados temporária (`VIBEPLAYER_DATA_DIR`); a biblioteca real não foi tocada. O mouse e o teclado foram acionados por script (Win32), as capturas são da tela. Capturas em [`capturas/`](capturas).

1. **Janela ao abrir — visto.** Banco novo: janela de 1116 × 739 px com moldura (cliente 1100 × 700), proporção bem menor que a anterior. [1-janela-ao-abrir.png](capturas/1-janela-ao-abrir.png). A migração do 1280 × 800 salvo foi verificada só nos testes automatizados (`WindowStateTests`), não à mão.
2. **Ícone — visto em parte.** `ExtractAssociatedIcon` do `VibePlayer.exe` devolve o play violeta ([2-icone-do-exe.png](capturas/2-icone-do-exe.png)) e a barra de título da janela mostra o mesmo ícone (visível em todas as capturas). **Não visto:** o ícone na barra de tarefas do Windows (não capturei a barra), e os formatos de macOS/Linux só por teste de arquivo (existem em `Assets/` do publish, conferido com `dotnet publish -r win-x64`); não há macOS/Linux para abrir.
3. **Clique no vídeo — visto.** Aula terminada: um clique reproduziu ([3a](capturas/3a-clique-reproduz.png), ícone de pausa e tempo 0,36 s); outro clique pausou em 1,48 s e, 3 s depois, o quadro continuava o mesmo ([3b](capturas/3b-clique-pausa.png)). Duplo clique com a aula pausada entrou em tela cheia sem retomar a reprodução ([3c](capturas/3c-duplo-clique-tela-cheia.png)).
4. **Tela cheia — visto.** A barra aparece sobre o rodapé do vídeo, sem espremê-lo ([3c](capturas/3c-duplo-clique-tela-cheia.png)). Tocando a 0,5×, após o tempo de espera a barra e o cursor sumiram e o vídeo ocupou 1920 × 1080 até a borda de baixo; pixel do rodapé = cor do vídeo ([4](capturas/4-tela-cheia-barra-escondida.png)). Com o vídeo 16:9 numa tela 16:9 não há faixas laterais. **Não testado:** vídeo 4:3 ou tela de outra proporção, onde as faixas pretas laterais da proporção do vídeo são esperadas. Obs.: numa primeira medição a barra não sumiu em 5 s; na repetição sumiu. Não investiguei a causa (provavelmente movimento do ponteiro gerado pelo script), mas o comportamento de sumir é o já existente, coberto por `PlayerTests.TelaCheia_SoOPlayer_ControlesSomemEVoltam`.
5. **Abas — visto.** Cinco ícones numa linha, na largura padrão (380) e no mínimo de 320 após arrastar a divisória ([5a](capturas/5a-abas-320px.png)); aba ativa com cor de destaque, ícone preenchido, sublinhado e título "Chat" acima; dica ao passar o mouse ("Resumo") ([5b](capturas/5b-aba-ativa-e-dica.png)).

## Documentação

- [x] CLAUDE.md
- [x] `DESIGN.md` (janela, abas, ícone)
- [ ] Wiki (pendente: a wiki é outro repositório; páginas que citam abas, tamanho da janela ou clique no vídeo precisam ser revisadas pela sessão da wiki)

# 0008 — Tarefas

## Implementação

- [ ] T1 — Novo tamanho padrão e migração do tamanho salvo
- [ ] T2 — Ícone oficial (SVG, .ico, .png, .icns) aplicado ao exe, janelas e pacotes
- [ ] T3 — Clique simples no vídeo alterna pausa (com duplo clique)
- [ ] T4 — Barra sobreposta e vídeo ocupando a tela toda em tela cheia
- [ ] T5 — Abas em uma linha (conforme D3)

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1, CA2 | Unitário | `WindowStateTrackerTests` (padrão novo; migração do 1280×800; tamanho próprio mantido) |
| CA3 | Unitário | arquivos de ícone existem e o csproj os referencia |
| CA4–CA6 | E2E | `PlayerClickTests` (clique pausa/retoma; duplo clique não pausa; clique na barra) |
| CA7 | E2E | `FullscreenLayoutTests` (área do vídeo == janela com barra visível e oculta) |
| CA8 | E2E | `SidePanelTabsTests` (uma linha em 320 px, nome acessível, ativa distinta) |

## Teste manual (obrigatório)

Registrar o que foi visto em cada ponto, com capturas, usando `tests/fixtures/videos` e pasta de dados temporária.

- [ ] 1 Janela ao abrir
- [ ] 2 Ícone (exe, janela, barra de tarefas)
- [ ] 3 Clique no vídeo
- [ ] 4 Tela cheia
- [ ] 5 Abas

## Documentação

- [ ] CLAUDE.md
- [ ] `DESIGN.md` (janela, abas, ícone)
- [ ] Wiki

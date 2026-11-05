# 0008 — Ajustes de layout

**Status:** Implementada
**Branch:** `feature/0008-ajustes-de-layout`

## Problema

Cinco pontos relatados pelo mantenedor depois de usar o app:

1. A janela abre grande demais (1280 × 800).
2. O executável não tem ícone (nem janela, barra de tarefas ou pacotes).
3. Clicar no vídeo não pausa.
4. Em tela cheia, quando a barra de controles some sobra um espaço e o vídeo não ocupa a tela toda.
5. As abas do painel lateral (Aulas, Transcrição, Chat, Resumo, Pontos principais) quebram linha em 320 px.

## Objetivo

Os cinco pontos corrigidos, sem mudar outras partes do app (melhorias gerais de fluidez ficam fora).

## Escopo

**Inclui:** os cinco pontos, testes e a atualização de `DESIGN.md`/`CLAUDE.md`/wiki.

**Não inclui:** instalador (spec 0009, outra sessão) e outras melhorias de UI.

## Decisões a tomar (escolha do mantenedor)

### D1 — Tamanho inicial (ponto 1)

Hoje: 1280 × 800, mínimo 960 × 600. Proposta: **1100 × 700** (limitado à área útil da tela, como já é feito). Alternativa: 1024 × 680.

**Quem já abriu o app:** o `WindowStateTracker` grava o tamanho ao fechar, então quem já fechou o app tem 1280 × 800 salvo e continuaria vendo o tamanho antigo. Opções:
- **(Recomendada)** Se o tamanho salvo for exatamente o antigo padrão (1280 × 800), tratamos como "nunca redimensionou" e usamos o novo. Quem escolheu outro tamanho mantém o seu. Custo: quem quer mesmo 1280 × 800 reajusta uma vez.
- Ignorar o tamanho salvo uma única vez (marca de versão do layout): afeta todos.
- Não migrar: só instalações novas veem a mudança.

### D2 — Ícone oficial (ponto 2)

Três propostas em [`opcoes/icone-opcoes.html`](opcoes/icone-opcoes.html) (SVGs na mesma pasta), com o violeta de destaque `#6D4AFF`:
- **A — Play simples:** quadrado arredondado violeta com triângulo branco. O mais legível em 16 px.
- **B — Play + chat:** tela com play e balão de fala, lembrando o chat com a aula.
- **C — Escuro + faísca:** fundo escuro, play violeta e faísca branca (IA). Destaca-se em barras claras e escuras.

O ícone escolhido vira SVG mestre em `src/VibePlayer.App/Assets/` e é exportado para `app.ico` (16–256 px; executável Windows e janela), `icon-256.png` (janela/barra no Linux) e `app.icns` (macOS). Aplicação: `<ApplicationIcon>` no csproj, `Icon` das janelas e inclusão dos arquivos nos pacotes (no `release.yml` só o necessário, sem conflitar com a spec 0009). O instalador da 0009 reutilizará `app.ico`/`app.icns`.

### D3 — Abas do painel lateral (ponto 5)

Todas as opções cabem em uma linha em 320 px:
- **(Recomendada) Só ícones** com dica (ToolTip) e `AutomationProperties.Name`; aba ativa com ícone preenchido, cor de destaque e sublinhado; o título da aba ativa aparece como cabeçalho do painel ("Transcrição"). Ícones: Aulas (lista de vídeos), Transcrição (texto), Chat (balão), Resumo (documento), Pontos principais (marcador).
- **Ícone + texto só na aba ativa:** inativas são ícones; a ativa mostra o nome ao lado. Mais descobrível, mas a barra muda de largura ao trocar.
- **Menu "…":** Aulas, Chat e Resumo em texto e o resto em um menu. Esconde funções; não recomendo.

## Correções (sem escolha visual)

### Ponto 3 — Clique no vídeo
Um clique simples na área do vídeo alterna pausar/reproduzir. Para conviver com o duplo clique (tela cheia), o clique só age depois do intervalo de duplo clique da plataforma; se vier o segundo clique, cancela e alterna a tela cheia sem mexer na reprodução. Cliques na barra de controles, no botão grande de play do pôster e na contagem para a próxima aula não passam pelo vídeo. Sem aula carregada, o clique não faz nada.

### Ponto 4 — Tela cheia
Causa provável (a confirmar no teste manual): `Grid.Row="1"` está escrito direto na barra, e valor local vence o estilo `fullscreen` que a moveria para cima do vídeo; a linha da barra continua reservada. Correção: vídeo e barra no mesmo painel, com a barra sobreposta embaixo em tela cheia; fora dela a barra continua abaixo do vídeo, sem cobrir a imagem. Se ainda houver faixas laterais com a janela realmente cobrindo a tela, são as barras pretas da proporção do vídeo (fundo sempre escuro), e isso será registrado no teste manual.

## Critérios de aceite

- [x] CA1 — Dado um banco sem tamanho salvo, quando o app abre, então a janela tem o novo tamanho padrão.
- [x] CA2 — Dado tamanho salvo igual ao antigo padrão, quando o app abre, então usa o novo padrão; com outro tamanho salvo, mantém-no.
- [x] CA3 — O executável, a janela e a barra de tarefas mostram o ícone oficial; os pacotes de macOS e Linux contêm os formatos necessários (`.icns`, PNG).
- [x] CA4 — Dado vídeo tocando, quando clico uma vez no vídeo, então pausa; clicando de novo, reproduz.
- [x] CA5 — Dado clique duplo no vídeo, então alterna tela cheia e o estado de reprodução não muda.
- [x] CA6 — Clicar nos botões da barra não alterna a reprodução além da ação do próprio botão.
- [x] CA7 — Em tela cheia a barra fica sobre o vídeo; a área do vídeo tem o mesmo tamanho (a janela inteira) com a barra visível ou escondida.
- [x] CA8 — As cinco abas cabem em uma linha em 320 px, a ativa é distinguível, cada uma tem dica e nome acessível.
- [x] CA9 — Teste manual dos cinco pontos registrado em `tasks.md` (ou dito claramente quando não foi possível).

## Impacto para o usuário

Janela menor ao abrir; ícone novo; clique pausa; tela cheia completa; abas novas. Nenhum dado muda, além do tamanho de janela salvo (D1). Wiki: atualizar o que citar abas ou clique no vídeo.

## Questões em aberto

D1 (tamanho e migração), D2 (A, B ou C), D3 (abas). Os testes manuais usarão uma pasta de dados temporária se o app permitir sobrescrevê-la; senão aviso antes de rodar.

## Decisões do mantenedor

Aprovado com "Recomendados": D1 = 1100 × 700 com migração do 1280 × 800; D3 = abas só com ícones. D2: o mantenedor não indicou letra; foi usada a **A (play simples)**, a recomendada por legibilidade em 16 px. Trocar é só mudar o desenho em `gerar-icones.py`/`icon.svg`.

Adição fora do texto aprovado (pequena, para viabilizar o teste manual sem tocar na biblioteca real): variável `VIBEPLAYER_DATA_DIR` troca a pasta de dados.

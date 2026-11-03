# 0006 — Player de vídeo, progresso e miniaturas das aulas

**Status:** Aprovada
**Branch:** `feature/0006-player`

## Problema

A biblioteca lista as aulas, mas não toca nenhuma: a área do vídeo só mostra o título, e os controles estão desabilitados. O app também não sabe o que o usuário já assistiu. Cada abertura começa do zero, sem "continuar de onde parei" e sem como ver quanto falta de um curso.

## Objetivo

Selecionar uma aula toca o vídeo. Os controles (reproduzir, posição, volume, velocidade, tela cheia) funcionam por mouse e teclado. O app lembra a posição de cada aula, marca as concluídas, mostra o andamento na biblioteca e reabre na última aula assistida.

## Escopo

**Inclui:**
- **Reprodução com LibVLC** (já definido no CLAUDE.md) dos formatos que a biblioteca aceita, com decodificação por hardware quando disponível.
- **Controles da barra:**
  - reproduzir/pausar;
  - barra de posição arrastável, com o tempo do ponto sob o ponteiro;
  - tempo atual e duração;
  - volume e mudo;
  - velocidade (0,5×, 0,75×, 1×, 1,25×, 1,5×, 1,75×, 2×);
  - tela cheia.
- **Atalhos de teclado** (quando o foco não está num campo de texto):

  | Tecla | Ação |
  |---|---|
  | Espaço / K | Reproduzir ou pausar |
  | ← / → | Voltar ou avançar 5 s |
  | J / L | Voltar ou avançar 10 s |
  | ↑ / ↓ | Volume ±5% |
  | M | Mudo |
  | F, duplo clique no vídeo | Tela cheia |
  | Esc | Sair da tela cheia |
  | Shift+. / Shift+, | Velocidade mais rápida ou mais lenta |
  | Ctrl+→ / Ctrl+← (⌘ no macOS) | Próxima ou aula anterior |

- **Tela cheia:** só o vídeo. Os controles aparecem ao mexer o mouse e somem depois de 3 s parado. O cursor some junto.
- **Progresso por aula:**
  - a posição é salva durante a reprodução (a cada 5 s), ao pausar, ao trocar de aula e ao fechar o app;
  - **continuar de onde parei:** ao abrir uma aula com posição salva, o vídeo começa nela (ver questão 2);
  - **aula concluída** ao chegar a 95% da duração ou ao terminar. O menu "⋯" da aula também permite marcar ou desmarcar como concluída à mão.
- **Fim da aula:** contagem "Próxima aula em 5 s", com botão para cancelar, e a próxima aula do curso começa (ver questão 1).
- **Na biblioteca:**
  - cada aula mostra a duração e um ícone de concluída ou em andamento;
  - cada curso mostra o andamento ("8 de 20 aulas · 3 h 40 min");
  - as durações são lidas dos arquivos em segundo plano, sem travar a UI.
- **Miniaturas das aulas:**
  - um quadro de cada vídeo, tirado a 10% da duração, para fugir da tela preta ou da vinheta do início;
  - aparecem à esquerda do título na lista de aulas; o curso usa a miniatura da primeira aula como capa;
  - na área do vídeo, servem de pôster da aula selecionada que ainda não começou a tocar (ex.: ao reabrir o app);
  - são geradas em segundo plano, uma de cada vez, começando pelos cursos abertos na árvore, e ficam em cache em disco;
  - vídeo que mudou ganha miniatura nova;
  - enquanto não existe a miniatura, aparece um ícone de vídeo no lugar.
- **Lembrar entre aberturas:**
  - volume, mudo e velocidade;
  - a última aula assistida, que já vem selecionada e pausada na posição salva ao abrir o app (ver questão 4).
- **Erros sem derrubar o app:**
  - arquivo sumiu, está corrompido ou tem codec não suportado → mensagem na área do vídeo, com detalhes no log;
  - no Linux, sem o VLC instalado → a área do vídeo explica como instalar, e o resto do app funciona (ver questão 3).
- **Licenças:**
  - o VLC é distribuído como bibliotecas separadas (LGPL, linkagem dinâmica);
  - os plugins com licença GPL ficam **fora** dos pacotes;
  - os avisos ficam em `THIRD-PARTY-NOTICES.md`.

**Não inclui** (specs futuras):
- Prévia do quadro ao passar o ponteiro pela barra de posição (exige gerar quadros sob demanda; fica para depois).
- Legendas, faixas de áudio e capítulos. As legendas `.srt`/`.vtt` entram com a transcrição.
- Timestamps clicáveis dos pontos principais (entram com a spec do chat e do resumo, que vai usar o "ir para posição" desta).
- Picture-in-picture, playlists entre cursos e configurações avançadas de vídeo (deinterlace, equalizador).
- Estatísticas de tempo assistido.

## Critérios de aceite

- [ ] CA1 — Selecionar uma aula disponível toca o vídeo; os controles habilitam e refletem o estado (tocando/pausado, tempo, duração) (E2E com player falso + verificação manual com o VLC real nos três SOs).
- [ ] CA2 — Arrastar a barra, os atalhos de tempo e o clique na barra levam o vídeo à posição certa; a velocidade e o volume mudam e ficam salvos entre aberturas (unitário + E2E).
- [ ] CA3 — Todos os atalhos da tabela funcionam e nenhum dispara enquanto o usuário digita na caixa do chat (E2E).
- [ ] CA4 — A tela cheia mostra só o vídeo; os controles aparecem ao mexer o mouse e somem 3 s depois; Esc ou F saem da tela cheia (E2E + manual).
- [ ] CA5 — A posição é salva periodicamente, ao pausar, ao trocar de aula e ao fechar; reabrir a aula retoma a posição salva conforme a regra da questão 2 (unitário + E2E).
- [ ] CA6 — Chegar a 95% ou ao fim marca a aula como concluída; marcar e desmarcar pelo menu funciona; a biblioteca mostra os ícones e o andamento do curso (unitário + E2E).
- [ ] CA7 — No fim da aula aparece a contagem para a próxima; cancelar mantém a aula atual; sem cancelar, a próxima aula disponível do curso começa; na última aula do curso nada acontece (unitário + E2E).
- [ ] CA8 — As durações aparecem na biblioteca sem travar a UI, inclusive numa pasta com 2.000 vídeos (E2E).
- [ ] CA9 — Ao abrir o app, a última aula assistida já vem selecionada, pausada na posição salva (E2E).
- [ ] CA10 — Arquivo indisponível, corrompido ou com codec não suportado mostra a mensagem de erro na área do vídeo, registra no log e o app segue funcionando (unitário + E2E + manual).
- [ ] CA11 — No Linux sem `libvlc`, a área do vídeo mostra como instalar o VLC e a biblioteca segue funcionando (E2E simulado + manual).
- [ ] CA12 — Os pacotes de release não contêm plugins do VLC com licença GPL, e o app toca MP4 (H.264/AAC), MKV (H.265), WebM (VP9/Opus) e AVI nos três SOs (verificação automatizada da lista de plugins + manual).
- [ ] CA13 — Os controles novos têm nome acessível e as mudanças de estado (tocando, pausado) são anunciadas (E2E).
- [ ] CA14 — As miniaturas aparecem nas aulas e na capa do curso, sem travar a UI; enquanto não existem, aparece o ícone de vídeo; vídeo alterado ganha miniatura nova; abrir a aula pausada mostra a miniatura como pôster (unitário + E2E com gerador falso + integração com o VLC real).

## Impacto para o usuário

- O app passa a tocar os cursos, lembrar onde o usuário parou, mostrar o progresso e exibir miniaturas das aulas.
- Surge a pasta `thumbnails/` nos dados do app (alguns KB por aula). Ela fica fora dos backups, porque pode ser gerada de novo.
- Os pacotes ficam maiores, porque o VLC vai junto no Windows e no macOS. Estimativa, a confirmar na implementação: cerca de 60 a 80 MB a mais depois de tirar os plugins desnecessários.
- No Linux, é preciso ter o VLC instalado.

Wiki: "Assistindo às aulas" (atalhos, progresso, tela cheia) e, na instalação, a dependência do VLC no Linux.

## Questões resolvidas

1. **Próxima aula:** passa sozinha, com a contagem "Próxima aula em 5 s" e o botão para cancelar.
2. **Continuar de onde parei:** retoma quando a posição salva está entre 10 s e 95%; antes de 10 s, ou se a aula já foi concluída, recomeça do início.
3. **Linux:** usa o VLC instalado no sistema, com orientação clara quando faltar.
4. **Reabertura do app:** a última aula volta selecionada e pausada na posição salva.
5. **Miniaturas:** entram nesta spec (lista de aulas, capa do curso e pôster no player).

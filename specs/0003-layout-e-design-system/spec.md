# 0003 — Layout principal e design system

**Status:** Aprovada
**Branch:** `feature/0003-layout-e-design-system`

## Problema

A janela do app está vazia e não existe padrão visual. Cada tela futura (biblioteca, player, chat, configurações) inventaria suas próprias cores, tamanhos e espaçamentos, e o app ficaria inconsistente e difícil de manter, principalmente com tema claro e escuro. Também ficou pendente da spec 0001 avisar o usuário na tela quando um erro é tratado — hoje ele só vai para o log.

## Objetivo

Existe um `DESIGN.md` que define o padrão visual do app, implementado como recursos centralizados do Avalonia, e a janela principal tem seu layout definitivo: player à esquerda e, à direita, um painel lateral com a biblioteca e o assistente de IA, com estados vazios claros, tema claro e escuro, navegação por teclado e aviso visual de erros. Nenhuma funcionalidade real ainda — só a estrutura que as próximas specs vão preencher.

## Escopo

**Inclui:**
- `DESIGN.md`: princípios, tipografia, cores (claro e escuro), espaçamento, raios, elevação, ícones, tamanhos de componentes, estados, movimento e acessibilidade.
- Tokens e estilos do Avalonia que implementam o `DESIGN.md` (nenhuma View usa cor ou tamanho literal).
- Janela principal com:
  - **barra superior**: nome do app, botão para mostrar/ocultar o painel lateral, botão de configurações (sem ação ainda);
  - **player** (esquerda, ocupa o espaço restante): área do vídeo com estado vazio "Selecione uma aula" e barra de controles (reproduzir, tempo, progresso, volume, velocidade, tela cheia) desabilitada;
  - **painel lateral** (direita) com as abas *Aulas*, *Chat*, *Resumo* e *Pontos principais*:
    - *Aulas* (biblioteca): estado vazio "Nenhum curso ainda" com botão "Adicionar pasta de cursos" (sem ação ainda);
    - *Chat*, *Resumo* e *Pontos principais* (assistente): estado vazio; no *Chat*, caixa de mensagem desabilitada;
  - divisória arrastável entre o player e o painel lateral, com largura mínima e máxima;
  - atalhos para mostrar/ocultar o painel lateral e para ir direto a cada aba.
- Tema segue o claro/escuro do sistema operacional.
- Notificação não bloqueante (canto inferior direito) quando o `GlobalExceptionHandler` trata um erro: "Algo deu errado. Os detalhes foram registrados no log."
- Textos da interface em arquivo de recursos (pt-BR), sem strings soltas nas Views.

**Não inclui** (specs futuras):
- Ações reais: adicionar pasta, listar cursos, tocar vídeo, chat, resumo.
- Tela de configurações e escolha manual de tema (precisa da persistência para lembrar a escolha).
- Lembrar tamanhos dos painéis entre aberturas do app (idem).
- Ícone/logo do app.
- Outros idiomas além de pt-BR (a estrutura de recursos já permite adicioná-los).

## Critérios de aceite

- [ ] CA1 — `DESIGN.md` existe na raiz com todas as seções do escopo e exemplos de uso dos tokens.
- [ ] CA2 — Nenhum arquivo `.axaml` em `Views/` contém cor literal (`#RRGGBB`, nomes de cor) nem tamanho de fonte numérico; tudo vem de tokens (verificado por teste).
- [ ] CA3 — Cada token de cor existe nas variantes clara e escura (verificado por teste).
- [ ] CA4 — Todos os pares de texto/fundo definidos no `DESIGN.md` têm contraste mínimo de 4,5:1 (texto normal) e 3:1 (texto grande e ícones) nos dois temas — WCAG AA (verificado por teste).
- [ ] CA5 — Ao abrir, a janela mostra a barra superior, o player à esquerda e o painel lateral à direita na aba *Aulas*, com seus estados vazios; cada aba mostra seu estado vazio ao ser selecionada (E2E).
- [ ] CA6 — O botão da barra superior e o atalho de teclado mostram e ocultam o painel lateral, e o player ocupa o espaço liberado; os atalhos de aba abrem o painel na aba pedida (E2E).
- [ ] CA7 — No tamanho mínimo da janela (960 × 600), com o painel lateral aberto, nada fica cortado nem com rolagem horizontal e o player mantém sua largura mínima (E2E).
- [ ] CA8 — Uma exceção tratada pelo `GlobalExceptionHandler` mostra a notificação de erro e continua registrada no log; o app segue funcionando (E2E).
- [ ] CA9 — Todo elemento interativo é alcançável por Tab, com indicador de foco visível, e tem nome acessível (verificado por teste para o nome; manual para o foco).
- [ ] CA10 — Capturas de tela da janela nos temas claro e escuro anexadas ao PR para revisão visual.

## Impacto para o usuário

O app passa a ter a cara definitiva, ainda sem funções. Próxima release mostra o layout vazio. Wiki ainda não precisa ser atualizada.

## Questões resolvidas

1. **Cor de destaque:** violeta (tons finais no `DESIGN.md`, ajustados pelo teste de contraste).
2. **Tema padrão:** segue o claro/escuro do sistema operacional.
3. **Idiomas:** só pt-BR por enquanto; a estrutura de recursos já permite outros idiomas.
4. **Disposição:** player à esquerda; biblioteca e assistente juntos num painel lateral à direita, em abas.

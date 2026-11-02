# 0003 — Layout principal e design system

**Status:** Aprovada
**Branch:** `feature/0003-layout-e-design-system`

## Problema

A janela do app está vazia e não existe padrão visual. Cada tela futura (biblioteca, player, chat, configurações) inventaria suas próprias cores, tamanhos e espaçamentos, e o app ficaria inconsistente e difícil de manter, principalmente com tema claro e escuro. Também ficou pendente da spec 0001 avisar o usuário na tela quando um erro é tratado — hoje ele só vai para o log.

## Objetivo

Existe um `DESIGN.md` que define o padrão visual do app, implementado como recursos centralizados do Avalonia, e a janela principal tem seu layout definitivo: biblioteca à esquerda, player no centro e assistente de IA à direita, com estados vazios claros, tema claro e escuro, navegação por teclado e aviso visual de erros. Nenhuma funcionalidade real ainda — só a estrutura que as próximas specs vão preencher.

## Escopo

**Inclui:**
- `DESIGN.md`: princípios, tipografia, cores (claro e escuro), espaçamento, raios, elevação, ícones, tamanhos de componentes, estados, movimento e acessibilidade.
- Tokens e estilos do Avalonia que implementam o `DESIGN.md` (nenhuma View usa cor ou tamanho literal).
- Janela principal com:
  - **barra superior**: nome do app, botões para mostrar/ocultar biblioteca e assistente, botão de configurações (sem ação ainda);
  - **biblioteca** (esquerda): estado vazio "Nenhum curso ainda" com botão "Adicionar pasta de cursos" (sem ação ainda);
  - **player** (centro): área do vídeo com estado vazio "Selecione uma aula" e barra de controles (reproduzir, tempo, progresso, volume, velocidade, tela cheia) desabilitada;
  - **assistente** (direita): abas *Chat*, *Resumo* e *Pontos principais*, com estado vazio e caixa de mensagem desabilitada;
  - divisórias arrastáveis para redimensionar biblioteca e assistente, com larguras mínimas e máximas;
  - atalhos para mostrar/ocultar os painéis.
- Comportamento em janelas estreitas: abaixo de 1200 px de largura o assistente começa recolhido.
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
- [ ] CA5 — Ao abrir, a janela mostra barra superior, biblioteca, player e assistente com seus estados vazios (E2E).
- [ ] CA6 — Botões da barra superior e atalhos de teclado mostram e ocultam biblioteca e assistente; o player ocupa o espaço liberado (E2E).
- [ ] CA7 — Com a janela abaixo de 1200 px de largura ao abrir, o assistente começa recolhido; no tamanho mínimo (960 × 600) nada fica cortado nem com rolagem horizontal (E2E).
- [ ] CA8 — Uma exceção tratada pelo `GlobalExceptionHandler` mostra a notificação de erro e continua registrada no log; o app segue funcionando (E2E).
- [ ] CA9 — Todo elemento interativo é alcançável por Tab, com indicador de foco visível, e tem nome acessível (verificado por teste para o nome; manual para o foco).
- [ ] CA10 — Capturas de tela da janela nos temas claro e escuro anexadas ao PR para revisão visual.

## Impacto para o usuário

O app passa a ter a cara definitiva, ainda sem funções. Próxima release mostra o layout vazio. Wiki ainda não precisa ser atualizada.

## Questões resolvidas

1. **Cor de destaque:** violeta (tons finais no `DESIGN.md`, ajustados pelo teste de contraste).
2. **Tema padrão:** segue o claro/escuro do sistema operacional.
3. **Idiomas:** só pt-BR por enquanto; a estrutura de recursos já permite outros idiomas.
4. **Disposição:** a do mockup (biblioteca à esquerda, player no centro, assistente à direita).

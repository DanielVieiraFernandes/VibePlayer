# 0004 — Persistência local (SQLite), backups e preferências

**Status:** Implementada
**Branch:** `feature/0004-persistencia`

## Problema

O app não guarda nada entre uma abertura e outra. As próximas specs dependem disso:
- a biblioteca precisa lembrar os cursos;
- o player precisa lembrar o progresso de cada aula;
- a transcrição precisa guardar o resultado para não refazer;
- o chat precisa guardar o histórico.

Hoje, até o painel lateral volta sempre ao tamanho padrão. A spec 0003 deixou isso pendente justamente por falta de persistência.

Além disso, o banco vai mudar de formato a cada versão. Ele precisa ser atualizado sozinho na máquina do usuário, sem perder dados, e uma falha nessa atualização não pode deixar o app num estado quebrado.

Fora das atualizações, o banco também pode se perder: arquivo corrompido por queda de energia, disco com defeito, um bug que apague dados. O progresso e o histórico do chat custam horas do usuário para refazer; transcrições custam minutos de processamento por aula.

## Objetivo

O app tem um banco SQLite em `<pasta de dados>/vibePlayer.db`, criado e atualizado automaticamente (migrations do EF Core) na inicialização, com backups automáticos e verificados, e um serviço de preferências que as próximas specs usam para guardar configurações. Como primeiro uso, o app passa a lembrar o estado da janela e do painel lateral entre aberturas.

## Escopo

**Inclui:**
- `DbContext` do EF Core com SQLite em `Infrastructure`, na pasta de dados do usuário (`vibePlayer.db`).
- Migrations aplicadas automaticamente na inicialização, antes de a janela abrir:
  - backup do banco antes de aplicar migrations pendentes;
  - se a atualização falhar, o banco volta ao backup, o usuário vê uma mensagem clara e o app fecha sem perder dados.
- **Rotina de backups** em `<pasta de dados>/backups/`:
  - backup automático uma vez por dia, na primeira abertura do dia, em segundo plano (o app abre sem esperar);
  - backups antes de cada atualização do banco, separados dos diários;
  - cada backup é conferido (verificação de integridade do SQLite) antes de contar como válido; um backup com defeito é descartado e nunca apaga os bons;
  - retenção: os 7 diários mais recentes e os 3 de atualização mais recentes;
  - backup feito com o banco aberto, sem travar a UI e sem risco de copiar o banco pela metade.
- Primeira migration com a tabela de preferências.
- `ISettingsStore` em `Core`: guardar e ler preferências tipadas (chave + valor com padrão), usado pelos ViewModels sem saberem que existe um banco.
- Lembrar entre aberturas:
  - painel lateral aberto ou fechado, largura e aba selecionada;
  - tamanho da janela e se estava maximizada (a posição não, porque monitores mudam; a janela continua abrindo centralizada).
- Gravações que não travam a UI e não martelam o disco: o arraste da divisória gera dezenas de mudanças por segundo, mas só a última é gravada.
- Ferramenta `dotnet-ef` fixada no repositório (manifesto de ferramentas local), para que qualquer máquina gere migrations com a mesma versão.

**Não inclui** (specs futuras):
- Tabelas de cursos, aulas, progresso, transcrições, resumos e chat: cada spec cria as suas, na sua própria migration.
- Tela de configurações e escolha manual de tema (spec de configurações).
- Chaves de API e `ISecretStore` (ver questão 1).
- Criptografia do banco (decisão do mantenedor: sem criptografia).
- Impedir duas instâncias do app abertas ao mesmo tempo.
- Restaurar um backup pela interface e escolher outra pasta de backup (ver questão 4); até lá, a restauração é manual e documentada na wiki.

## Critérios de aceite

- [x] CA1 — Na primeira abertura, `vibePlayer.db` é criado na pasta de dados com todas as migrations aplicadas (E2E).
- [x] CA2 — Todas as mudanças no modelo do EF Core têm migration correspondente; um modelo alterado sem migration faz um teste falhar (unitário).
- [x] CA3 — Com um banco de uma versão anterior (migrations pendentes), a abertura cria um backup de atualização, aplica as migrations e preserva os dados existentes (E2E).
- [x] CA4 — Se aplicar uma migration falhar, o banco volta ao conteúdo de antes, o erro é registrado no log e o usuário vê "Não foi possível atualizar seus dados" com o caminho do backup; o app encerra sem exceção não tratada (E2E).
- [x] CA10 — Na primeira abertura do dia é criado um backup diário válido (abre e passa na verificação de integridade); na segunda abertura do mesmo dia, nenhum (unitário com relógio controlado).
- [x] CA11 — O backup diário roda com o app aberto e gravando, e a cópia contém os dados gravados até o momento do backup (E2E).
- [x] CA12 — Com 8 backups diários e 4 de atualização, a limpeza mantém os 7 e os 3 mais recentes; backups com defeito não entram na contagem e são removidos (unitário).
- [x] CA13 — Se o backup falhar (disco cheio, pasta sem permissão, banco corrompido), o erro vai para o log, o app segue normalmente e os backups anteriores continuam intactos (unitário).
- [x] CA5 — `ISettingsStore` devolve o valor padrão quando a chave nunca foi gravada e o último valor gravado depois de salvo, para tipos simples e compostos (unitário, SQLite em memória).
- [x] CA6 — Fechar e reabrir o app restaura: painel aberto/fechado, largura, aba selecionada, tamanho da janela e estado maximizado (E2E com duas "aberturas" sobre o mesmo banco em pasta temporária).
- [x] CA7 — Uma sequência rápida de mudanças (ex.: arrastar a divisória) resulta em uma única gravação, e fechar o app logo depois de uma mudança não a perde (unitário + E2E).
- [x] CA8 — Um valor salvo inválido ou corrompido (ex.: largura negativa, JSON quebrado) é ignorado com aviso no log e o padrão é usado; o app abre normalmente (unitário).
- [ ] CA9 — Os pacotes de release dos quatro RIDs incluem a biblioteca nativa do SQLite e o app abre neles (manual, na próxima release).

## Impacto para o usuário

- O app passa a lembrar o tamanho da janela e o painel lateral.
- Surge o arquivo `vibePlayer.db` na pasta de dados (`%APPDATA%\VibePlayer` no Windows, `~/.config/VibePlayer` no Linux e no macOS), ao lado de `logs/`.
- Surge a pasta `backups/` ao lado do banco, com até 10 arquivos (o banco é pequeno: hoje alguns KB; com transcrições, poucos MB).

Wiki: acrescentar na FAQ onde ficam os dados, como "zerar" o app (apagar a pasta) e como restaurar um backup (fechar o app e copiar o arquivo do backup por cima de `vibePlayer.db`).

## Questões resolvidas

1. **Chaves de API:** ficam para a spec de provedores de LLM.
2. **O que lembrar:** painel lateral (aberto, largura, aba) e janela (tamanho e maximizada); a posição não.
3. **Falha ao atualizar o banco:** restaura o backup, avisa e fecha.
4. **Backups:** um diário (mantém 7) e um antes de cada atualização (mantém 3); restaurar pela interface e escolher outra pasta ficam para a spec de configurações.

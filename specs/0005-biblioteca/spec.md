# 0005 — Biblioteca de cursos

**Status:** Implementada
**Branch:** `feature/0005-biblioteca`

## Problema

A aba *Aulas* só mostra "Nenhum curso ainda": não existe como dizer ao app onde estão os cursos. Os cursos vêm como pastas de vídeos, com convenções variadas:
- módulos em subpastas, ou todos os vídeos soltos;
- numeração com e sem zero à esquerda ("2 - …" e "10 - …");
- arquivos de sistema misturados (`.DS_Store`, `Thumbs.db`, `@eaDir`).

A ordenação alfabética comum põe "10 - Conclusão" antes de "2 - Introdução". As specs seguintes (player, progresso, transcrição, chat) precisam de uma aula com identidade estável no banco para guardar seus dados.

## Objetivo

O usuário adiciona pastas de cursos ou vídeos avulsos e vê, na aba *Aulas*, seus cursos com módulos e aulas na ordem certa. A biblioteca fica salva, é atualizada quando os arquivos mudam e nunca perde o vínculo de uma aula com seus dados por causa de um arquivo que sumiu temporariamente (HD externo desconectado, pasta renomeada).

## Escopo

**Inclui:**
- **Adicionar pasta** pelo botão "Adicionar pasta de cursos" (seletor de pastas do sistema).
  - O app **sempre pergunta** a interpretação: "Um curso" ou "Vários cursos". A sugestão dele vem pré-selecionada.
  - Uma prévia mostra o que cada opção gera ("3 cursos: A, B, C" ou "1 curso com 4 módulos e 32 aulas") antes de confirmar.
- **Adicionar vídeos** (um ou vários arquivos, pelo seletor de arquivos). A prévia pergunta o destino:
  - um **curso novo**, com nome editável (sugestão: o nome da pasta em comum dos vídeos);
  - ou o grupo **"Vídeos avulsos"**, um curso fixo da biblioteca criado na primeira vez que for usado.
  - Os vídeos ficam em ordem natural pelo nome.
- **Estrutura:**
  - curso → módulos (subpastas) → aulas (vídeos);
  - vídeos soltos na pasta do curso são aulas sem módulo, listadas antes dos módulos;
  - subpastas dentro de módulos viram módulos com nome composto ("Módulo 1 / Parte A"), porque o painel lateral não comporta árvore profunda.
- **Ordenação natural** em cursos, módulos e aulas ("2 - …" antes de "10 - …"), sem diferenciar maiúsculas e acentos.
- **Formatos de vídeo:** `.mp4`, `.m4v`, `.mkv`, `.webm`, `.mov`, `.avi`, `.wmv`, `.flv`, `.mpg`, `.mpeg`, `.ts`.
- **Arquivos ignorados:**
  - arquivos e pastas ocultos ou de sistema (`.`-prefixados, `Thumbs.db`, `desktop.ini`, `@eaDir`, `$RECYCLE.BIN`);
  - atalhos e links simbólicos não são seguidos, para não entrar em ciclos.
- **Atualização da biblioteca** em segundo plano, sem travar a UI e com indicação de progresso:
  - ao abrir o app;
  - pelo botão "Atualizar".
- **Mudanças nos arquivos:**
  - aulas e módulos novos aparecem;
  - aulas cujo arquivo sumiu ficam marcadas como **indisponíveis**, em vez de apagadas, para não perder progresso, transcrição e chat quando o arquivo voltar;
  - pasta inteira inacessível (ex.: HD externo desconectado) → os cursos dela aparecem como indisponíveis, com o caminho esperado.
- **Remover da biblioteca** (menu do curso), com confirmação. Apaga só os registros do app, nunca os arquivos do usuário. Em cursos montados com vídeos avulsos, também dá para remover uma aula sozinha.
- **Localizar pasta** (menu de um curso indisponível): o usuário aponta onde a pasta está agora.
  - As aulas são reconectadas pelo caminho relativo, mantendo os mesmos registros (Id) e, portanto, os dados ligados a elas.
  - Se nenhuma aula conhecida for encontrada no novo lugar, o app avisa e pede confirmação antes de trocar.
  - Vale para a pasta adicionada inteira (todos os cursos dela). Vídeo avulso que sumiu continua indisponível; o usuário pode removê-lo e adicioná-lo de novo.
- **Aba *Aulas*:**
  - lista de cursos com módulos e aulas expansíveis;
  - contagem de aulas por curso;
  - aula indisponível em estilo desabilitado;
  - estado vazio atual mantido quando não há pastas.
- **Selecionar uma aula** a torna a "aula atual": a área do vídeo mostra o título dela e o curso. A reprodução vem na spec do player.
- **Validação** (FluentValidation, mensagens em pt-BR). Vídeos avulsos precisam existir, ter formato de vídeo e não estar já na biblioteca. A pasta precisa:
  - existir e ser legível;
  - não estar já na biblioteca, nem dentro ou acima de uma pasta já adicionada;
  - não ser a pasta de dados do app;
  - ter pelo menos um vídeo.

**Não inclui** (specs futuras):
- Reproduzir vídeos, progresso e "continuar de onde parei" (spec do player).
- Duração das aulas e miniaturas (precisam ler o vídeo; spec do player).
- Hash dos arquivos para o cache de transcrição (spec de transcrição).
- Legendas `.srt`/`.vtt` ao lado dos vídeos (spec de transcrição).
- Observar a pasta em tempo real (FileSystemWatcher); a atualização é na abertura e pelo botão.
- Arrastar pasta para a janela, busca na biblioteca, renomear cursos no app.

## Critérios de aceite

- [x] CA1 — Ao adicionar uma pasta, o app sempre pergunta a interpretação, com a sugestão pré-selecionada: com 3 subpastas de curso a sugestão é "3 cursos", com módulos e vídeos é "1 curso"; a prévia muda ao trocar a opção, e só a confirmação grava (unitário + E2E).
- [x] CA12 — Adicionar vídeos avulsos: a prévia pergunta "curso novo" (com nome editável) ou "Vídeos avulsos"; os vídeos entram em ordem natural; repetir um vídeo já presente é recusado; uma aula avulsa pode ser removida sozinha (unitário + E2E).
- [x] CA13 — Localizar pasta: depois de mover a pasta de um curso, apontar o novo lugar reconecta as aulas com os mesmos Ids e elas voltam a ficar disponíveis; um lugar sem nenhuma aula conhecida pede confirmação antes de trocar (E2E).
- [x] CA2 — "2 - b.mp4", "10 - a.mp4" e "1.1 - c.mp4" ficam na ordem 1.1, 2, 10; maiúsculas e acentos não alteram a ordem (unitário).
- [x] CA3 — Arquivos que não são vídeo, ocultos, de sistema e links simbólicos não entram na biblioteca (unitário).
- [x] CA4 — Pasta inexistente, já adicionada, dentro ou acima de outra já adicionada, a pasta de dados do app ou sem vídeos é recusada com mensagem clara, sem criar registros (unitário).
- [x] CA5 — A biblioteca aparece igual depois de fechar e reabrir o app (E2E).
- [x] CA6 — Ao atualizar: vídeo novo aparece; vídeo removido fica indisponível com o mesmo registro (mesmo Id); se o vídeo volta, fica disponível de novo com o mesmo Id (E2E).
- [x] CA7 — Com a pasta raiz inacessível, os cursos dela aparecem como indisponíveis e o resto da biblioteca funciona (E2E).
- [x] CA8 — Remover um curso apaga seus registros e não toca em nenhum arquivo da pasta (E2E).
- [x] CA9 — A atualização roda em segundo plano: a UI segue respondendo durante a leitura de uma pasta grande (ex.: 2.000 vídeos gerados no teste) e mostra o progresso; erros de leitura de uma subpasta (sem permissão) são registrados e não interrompem o resto (E2E).
- [x] CA10 — Selecionar uma aula mostra o título dela e o nome do curso na área do vídeo; aula indisponível não pode ser selecionada (E2E).
- [x] CA11 — Todos os controles novos têm nome acessível e são navegáveis por teclado (setas na lista, Enter para selecionar) (E2E).

## Impacto para o usuário

- Primeira funcionalidade de verdade: adicionar cursos e vídeos e navegar pelas aulas.
- O botão "Adicionar pasta de cursos" deixa de estar desabilitado.
- Os dados da biblioteca vão para o banco e entram nos backups.

Wiki: página "Adicionando cursos", com as convenções de pasta que o app entende, vídeos avulsos, o que significa "indisponível" e como usar "Localizar pasta".

## Questões resolvidas

1. **Curso ou pasta de cursos:** o app sempre pergunta, com a sugestão pré-selecionada e uma prévia. Também é possível adicionar vídeos avulsos, e a prévia pergunta se eles formam um curso novo ou vão para "Vídeos avulsos".
2. **Aulas cujo arquivo sumiu:** ficam indisponíveis, sem limpeza automática; só saem ao remover o curso (ou a aula avulsa).
3. **Pasta que mudou de lugar:** "Localizar pasta" entra nesta spec.
4. **Atualização da biblioteca:** ao abrir o app e pelo botão "Atualizar", sem observar a pasta em tempo real. A ideia de atualização automática do próprio app ficou registrada em `docs/backlog.md`.

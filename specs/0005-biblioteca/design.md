# 0005 — Design técnico

## Abordagem

```
Core/Library/
  NaturalStringComparer     Ordenação natural ("2" < "10"), sem caixa/acentos; pura, sem I/O
  IFileSystemReader         Lista entradas de uma pasta (nome, pasta?, tamanho, data, oculto/sistema/link)
  LibraryScanner            Lê a árvore via IFileSystemReader e devolve cursos → módulos → aulas já ordenados
  LibraryDetection          Sugere "um curso" ou "vários cursos" para uma pasta (regra da questão 1)
  LibraryFolderValidator    FluentValidation: existe, sem sobreposição, não é a pasta de dados, tem vídeo
  ICourseLibrary            Preview / Add (pasta ou vídeos) / Remove / Relocate / GetCourses / Refresh
  Modelos de leitura        CourseNode, ModuleNode, LessonNode (imutáveis, para a UI)
Infrastructure/
  FileSystem/PhysicalFileSystemReader   System.IO com EnumerationOptions (sem seguir links)
  Library/CourseLibrary                 ICourseLibrary sobre o DbContext; sincronização scan ↔ banco
  Persistence/                          Entidades LibraryRoot, Course, Module, Lesson + migration
App/
  Services/IFilePicker                  Seletores de pasta e de vídeos (StorageProvider) atrás de interface, para os E2E
  ViewModels/LibraryViewModel           Árvore, comandos Adicionar/Atualizar/Remover, progresso
  ViewModels/AddLibraryFolderViewModel  Prévia da detecção e troca de interpretação
  Views/Library/LibraryView             TreeView com cursos, módulos e aulas
  Views/Library/AddLibraryFolderDialog  Janela modal da prévia de pasta (interpretação)
  Views/Library/AddVideosDialog         Janela modal da prévia de vídeos (curso novo ou "Vídeos avulsos")
  Views/Shell/ConfirmDialog             Confirmação genérica (usada em "Remover")
  PlayerViewModel.CurrentLesson         Título da aula e do curso na área do vídeo
```

`LibraryScanner` e `LibraryDetection` ficam no `Core` e só enxergam `IFileSystemReader`. Assim a lógica que mais tem casos (ordenação, filtros, detecção, módulos aninhados) é testada com uma árvore falsa em memória, sem disco. O disco de verdade entra só nos E2E.

### Fluxo de adicionar

```
"Adicionar pasta" → IFilePicker.PickFolder → LibraryFolderValidator
   ├─ inválida → mensagem no próprio painel (faixa de erro), nada é gravado
   └─ válida  → ICourseLibrary.PreviewFolderAsync(pasta)  (scan em segundo plano; calcula as duas interpretações)
                 → AddLibraryFolderDialog:  (•) Vários cursos — 3 cursos: A, B, C
                                            ( ) Um curso — 1 curso com 4 módulos e 32 aulas
                 → Confirmar → ICourseLibrary.AddFolderAsync(pasta, tipo) → árvore recarrega

"Adicionar vídeos" → IFilePicker.PickVideos (vários) → validação (existe, formato, não repetido)
   └─ AddVideosDialog:  (•) Curso novo: [nome sugerido]    ( ) Vídeos avulsos
      → Confirmar → ICourseLibrary.AddVideosAsync(arquivos, destino) → árvore recarrega
```

### Detecção (questão 1)

Seja F a pasta escolhida:
- **Vários cursos:** alguma subpasta direta de F tem subpastas com vídeos (formato `F/Curso/Módulo/aula.mp4`).
  - Cada subpasta direta com vídeos vira um curso.
  - Vídeos soltos direto em F não pertencem a nenhum curso. A prévia avisa, e eles ficam de fora.
- **Senão, um curso:** F é o curso, e suas subpastas são os módulos.
- A prévia sempre permite trocar a interpretação. A escolhida fica salva na raiz (`Kind`) e vale nas próximas atualizações.

### Vídeos avulsos

- Curso de arquivos (`Kind = Files`) não tem raiz: cada aula guarda o **caminho absoluto** do vídeo, e não há módulos.
- "Vídeos avulsos" é um curso de arquivos fixo (marcado por `IsLooseVideos`), criado na primeira vez que for usado e listado por último na biblioteca.
- **Nome sugerido para um curso novo:** a pasta em comum dos vídeos, ou o nome do primeiro vídeo se não houver pasta em comum.
- **Na atualização:** cada arquivo é conferido (existe? tamanho/data mudaram?). Arquivo que sumiu → aula indisponível, como nas pastas.
- Aula de curso de arquivos pode ser removida sozinha. Removendo a última de um curso novo, o curso sai junto; "Vídeos avulsos" vazio deixa de aparecer.

### Localizar pasta

- Disponível no menu de um curso cuja raiz está indisponível. Age sobre a **raiz inteira**: todos os cursos daquela pasta adicionada.
- O usuário escolhe a nova pasta e passa pela mesma validação (exceto "já adicionada", porque é a própria raiz).
- Um scan da nova pasta conta quantas aulas conhecidas existem lá pelo caminho relativo:
  - **Nenhuma** → confirmação "Nenhuma aula conhecida foi encontrada nesta pasta. Usar mesmo assim?" Confirmar faz as aulas antigas ficarem indisponíveis e as encontradas entrarem como novas.
  - **Uma ou mais** → troca o `Path` da raiz e roda a sincronização normal. Aulas encontradas voltam a ficar disponíveis com o mesmo Id.
- A interpretação (`Kind`) da raiz é mantida.

### Estrutura dentro de um curso

- Vídeos direto na pasta do curso são aulas sem módulo e aparecem primeiro.
- O app sempre pergunta; a regra acima só decide qual opção vem pré-selecionada.
- Cada subpasta com vídeos (em qualquer profundidade) é um módulo. Aninhamento vira nome composto ("Módulo 1 / Parte A"), na ordem natural do caminho.
- Pastas sem vídeos não aparecem.
- Título da aula é o nome do arquivo sem extensão. Título de curso e módulo é o nome da pasta. Nenhuma "limpeza" de numeração: o número costuma ser a informação que o usuário procura.

### Ordenação natural

Compara por blocos: sequências de dígitos como número (`2 < 10`, `1.1 < 2`, zeros à esquerda ignorados) e o resto como texto. A comparação de texto usa `CompareOptions.IgnoreCase | IgnoreNonSpace` (sem diferenciar maiúsculas e acentos) com a cultura invariante, para a ordem não mudar com o idioma do sistema. Empate → comparação ordinal, para a ordem ser total e estável.

### Sincronização (atualizar)

Para cada raiz:
1. **Raiz inacessível** → cursos, módulos e aulas dela ficam `IsAvailable = false`; nada é apagado nem lido.
2. **Raiz acessível** → scan completo. Junta com o banco pelo **caminho relativo**:
   - curso pelo caminho relativo à raiz;
   - módulo e aula pelo caminho relativo ao curso.
3. Novo → insere com Id novo (`Guid.CreateVersion7`).
4. Existente → atualiza título, posição, tamanho e data, e volta a ficar disponível.
5. Ausente → `IsAvailable = false`, com o mesmo Id. Progresso, transcrição e chat das próximas specs apontam para esse Id e voltam a valer quando o arquivo reaparecer.
6. Tudo de uma raiz numa transação: a UI nunca vê a biblioteca pela metade.

Comparação de caminhos:
- **Caixa:** sem diferenciar maiúsculas no Windows e no macOS (sistemas de arquivos que não diferenciam), diferenciando no Linux. Renomear só a caixa no Windows não cria uma aula nova.
- **Unicode:** normalizado para NFC antes de comparar e gravar. O macOS devolve nomes em NFD, e "Aula 1 - Introdução" viraria outra aula ao copiar a pasta entre sistemas.

### Execução em segundo plano

- `RefreshAsync(IProgress<LibraryScanProgress>, CancellationToken)`: roda fora da thread de UI (`Task.Run` aguardado dentro do serviço). A UI chama via `FireAndForget` na abertura e aguarda pelo botão "Atualizar".
- Uma atualização por vez (`SemaphoreSlim`). Pedir outra durante uma em andamento não enfileira uma segunda; espera a atual.
- O progresso é reportado a cada 100 arquivos ("Atualizando… 1.200 aulas"), não a cada arquivo, para não inundar a UI.
- Fechar o app cancela a atualização. A transação da raiz em andamento é descartada; as raízes já concluídas ficam.
- Subpasta sem permissão → `Warning` no log com a raiz e a contagem de pastas puladas; o resto segue.

### UI

- Aba *Aulas* com barra de ações no topo: "Adicionar pasta" e "Atualizar" (botões de ícone com nome acessível), e uma barra de progresso fina durante a atualização.
- `TreeView` com três níveis:
  - **Curso:** título e "12 aulas"; se indisponível, "Pasta não encontrada" em `TextSecondary`. Menu de contexto (também pela tecla Menu ou Shift+F10) com "Remover da biblioteca".
  - **Módulo:** título.
  - **Aula:** título, ou estilo desabilitado se indisponível.
- Teclado: setas navegam, Enter seleciona a aula. Estado expandido/recolhido só na sessão.
- Aula selecionada → `PlayerViewModel.CurrentLesson = (Título, Curso)`. O estado vazio do player troca para o título da aula e a frase "A reprodução chega numa próxima versão".
- Erros de validação aparecem numa faixa de erro no topo do painel (cores `ErrorSubtle`/`Error` do DESIGN.md), com botão de fechar.

## Decisões

| Decisão | Motivo | Alternativas descartadas |
|---|---|---|
| Scanner e detecção no `Core` sobre `IFileSystemReader` | Testes unitários sem disco cobrem os casos difíceis | System.IO.Abstractions (dependência a mais para usar 3 métodos); testar só em E2E (lento, pouco preciso) |
| Identidade da aula = caminho relativo ao curso, Id estável | Sobrevive a arquivo sumido e voltando; base para progresso e cache | Caminho absoluto (quebra ao mover a raiz); hash do arquivo (caro de calcular a cada scan; entra na spec de transcrição) |
| Aula ausente fica indisponível, não é apagada | Não perde dados ligados à aula | Apagar (perde progresso/chat se o HD estiver desconectado) |
| Posição calculada no scan e gravada | SQLite não ordena "naturalmente"; a UI só lê na ordem | Ordenar na UI a cada carga (mais trabalho e a ordem fica espalhada) |
| Atualizar na abertura + botão | Simples e confiável em HD externo e rede | `FileSystemWatcher` (eventos perdidos ou duplicados em rede e USB; fica para depois, se fizer falta) |
| Sempre perguntar a interpretação, com sugestão e prévia | Curso com módulos e pasta de cursos têm o mesmo formato; o usuário decide vendo o resultado | Só adivinhar (erra calado); sem sugestão (mais atrito) |
| Vídeos avulsos com caminho absoluto, sem raiz | Os arquivos podem vir de pastas diferentes; não há pasta para relativizar | Criar uma "raiz" falsa por vídeo (complica validação e "Localizar pasta") |
| "Localizar pasta" por raiz, reconectando pelo caminho relativo | Mover a pasta inteira é o caso comum (outro disco); preserva os Ids e os dados | Reconectar por hash do arquivo (caro; entra com a transcrição); por aula (trabalhoso para o usuário) |
| `TreeView` do Avalonia | Árvore curta (cursos recolhidos por padrão), teclado e acessibilidade prontos | Lista "achatada" própria (mais código para virtualizar o que hoje não precisa) |

## Mudanças em dados

Migration `Biblioteca`:

| Tabela | Colunas | Índices / relações |
|---|---|---|
| `LibraryRoots` | `Id` (Guid), `Path`, `Kind` (`Course`/`Collection`), `AddedAt`, `LastScanAt`, `IsAvailable` | `Path` único |
| `Courses` | `Id`, `RootId` (nulo em curso de arquivos), `Kind` (`Folder`/`Files`), `IsLooseVideos`, `RelativePath` (vazio quando a raiz é o curso; nulo em curso de arquivos), `Title`, `Position`, `IsAvailable`, `AddedAt` | (`RootId`, `RelativePath`) único; FK → raiz, `CASCADE` |
| `Modules` | `Id`, `CourseId`, `RelativePath`, `Title`, `Position`, `IsAvailable` | (`CourseId`, `RelativePath`) único; FK → curso, `CASCADE` |
| `Lessons` | `Id`, `CourseId`, `ModuleId` (nulo = sem módulo), `Path` (relativo ao curso em curso de pasta; absoluto em curso de arquivos), `Title`, `Position`, `FileSize`, `LastWriteUtc`, `IsAvailable`, `FirstSeenAt` | (`CourseId`, `Path`) único; FK → curso `CASCADE`; FK → módulo `SET NULL` |

- Remover um curso apaga módulos e aulas em cascata. Remover a última raiz-curso de uma raiz apaga a raiz.
- Bancos da v0.2 só ganham tabelas novas; a tabela `Settings` não muda.
- O backup de atualização (spec 0004) roda antes desta migration.

## Dependências novas

| Pacote | Projeto | Licença |
|---|---|---|
| FluentValidation 12.2.0 | Core | Apache 2.0 |

Já previsto no CLAUDE.md; entra em `THIRD-PARTY-NOTICES.md`.

## Logs e erros

- `Information`: "Pasta relocalizada: {RootId} ({Matched} de {Known} aulas encontradas)"; "Vídeos adicionados: {Count} em {CourseId}"; "Biblioteca atualizada: {Roots} pastas, {Courses} cursos, {Lessons} aulas ({New} novas, {Missing} indisponíveis) em {Elapsed}"; "Pasta adicionada à biblioteca: {RootId} ({Kind}, {Courses} cursos)".
- `Warning`: raiz inacessível ({RootId}); subpastas puladas por permissão ({RootId}, {Count}).
- `Debug`: caminhos completos (nomes de pastas podem ser pessoais; ficam fora do log em release).
- Falhas inesperadas na atualização → `Error`, e a faixa de erro do painel mostra "Não foi possível atualizar a biblioteca"; a biblioteca anterior continua visível.

## Riscos

- **Pastas enormes ou de rede lentas:** scan em segundo plano, com progresso e cancelamento ao fechar. CA9 mede com 2.000 vídeos gerados no teste.
- **Interpretação errada salva:** mitigada pela prévia. Corrigir depois exige remover e adicionar de novo (aceitável nesta versão).
- **Nomes de arquivo estranhos** (emoji, nomes muito longos, caminhos acima de 260 caracteres no Windows): o .NET moderno suporta caminhos longos. O `app.manifest` já declara `longPathAware`? A conferir na implementação e incluir se faltar.
- **Muitas aulas na árvore:** sem virtualização no `TreeView`, uma biblioteca com milhares de aulas expandidas pode pesar. Cursos começam recolhidos; se aparecer lentidão, vira spec própria.

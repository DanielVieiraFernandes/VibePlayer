# 0004 — Design técnico

## Abordagem

```
Core/
  Settings/
    ISettingsStore        GetAsync<T>(SettingKey<T>) / SetAsync<T>(SettingKey<T>, T)
    SettingKey<T>         Nome + valor padrão + validação opcional (ex.: largura > 0)
  Persistence/
    IDatabaseInitializer  Garante o banco atualizado; resultado = ok | falhou (com caminho do backup)
    IBackupService        CreateAsync(BackupKind) / RunDailyIfDueAsync() / Prune
    BackupKind            Daily | PreMigration
Infrastructure/
  Persistence/
    VibePlayerDbContext   DbSet<SettingEntry>; uma pasta Configurations/ com IEntityTypeConfiguration por entidade
    SettingEntry          Key (PK, texto), Value (JSON), UpdatedAt
    DatabaseInitializer   Backup de atualização → Migrate → restauração em caso de falha
    SqliteBackupService   API de backup online do SQLite + integrity_check + retenção
    Migrations/           Geradas pelo dotnet-ef; nunca editadas à mão depois de publicadas
  Settings/
    SqliteSettingsStore   Implementa ISettingsStore sobre o DbContext (System.Text.Json)
App/
  Program                 Roda o IDatabaseInitializer antes do Avalonia; falha → janela de erro e encerra
  State/
    WindowStateTracker    Lê o estado salvo ao abrir; grava com atraso ao mudar e na hora ao fechar
```

### Inicialização

```
Program.Main
  ├─ Serilog, AppPaths, BuildServices            (como hoje)
  ├─ IDatabaseInitializer.Initialize()           síncrono: ainda não há UI nem SynchronizationContext
  │    ├─ sem migrations pendentes → segue
  │    ├─ pendentes → backup de atualização → Migrate()
  │    └─ Migrate falhou → fecha conexões → restaura o backup → resultado "falhou"
  ├─ falhou  → Avalonia só com a janela de erro (texto + caminho do backup + botão Fechar) → exit code 2
  └─ ok      → carrega o estado salvo da janela → StartWithClassicDesktopLifetime
               └─ janela aberta → IBackupService.RunDailyIfDueAsync().FireAndForget(...)
```

- O banco é preparado **antes** da janela abrir. Uma janela que aparece e depois descobre que não tem dados fica num estado que cada tela teria de tratar. O banco é pequeno e a migration costuma levar milissegundos.
- O backup de atualização é feito só quando há migrations pendentes e guarda o "último estado bom" antes de cada atualização. Se ele falhar, a migration não roda: atualizar sem rede de segurança é exatamente o que a rotina existe para evitar.
- **Banco de uma versão mais nova:** acontece quando o usuário volta para uma versão anterior do app. O EF ignora migrations desconhecidas já aplicadas. O app registra um aviso no log e segue, e as tabelas novas simplesmente não são usadas.

### DbContext

- `AddDbContextFactory<VibePlayerDbContext>` (singleton). Cada operação cria um contexto curto (`await using var db = await factory.CreateDbContextAsync()`).
  - Num app desktop o processo vive horas, e um contexto longo acumularia entidades rastreadas e conflitos.
  - A factory também é segura para uso de várias threads (transcrição em segundo plano, UI).
- Conexão `Data Source=<pasta>/vibePlayer.db`. Na abertura:
  - `PRAGMA journal_mode=WAL`, para que leituras da UI não esperem gravações em segundo plano;
  - `foreign_keys=ON`, para valer nas tabelas das próximas specs.
- Todo acesso é `async`. Nenhuma chamada ao banco na thread de UI de forma síncrona, exceto a inicialização no `Program`, que roda antes da UI existir.
- `dotnet ef` precisa construir o contexto sem o app rodando: `VibePlayerDesignTimeFactory : IDesignTimeDbContextFactory` aponta para um arquivo temporário. Assim o startup project do comando passa a ser o próprio `Infrastructure`.

### Backups

```
<pasta de dados>/backups/
  vibePlayer-diario-20261102-091512.db
  vibePlayer-atualizacao-20261102-091510-v0.3.0.db     (versão do app que fez a atualização)
```

- **Como copiar:** com a API de backup online do SQLite (`SqliteConnection.BackupDatabase`), não com cópia de arquivo.
  - Ela lê páginas consistentes mesmo com o app gravando (WAL), sem fechar conexões e sem o risco de copiar o `.db` sem o que ainda está no `-wal`.
  - Grava primeiro em `*.tmp`; só depois da verificação o arquivo é renomeado para `.db`. Um backup interrompido no meio nunca parece válido.
- **Verificação:** `PRAGMA integrity_check` no arquivo novo, aberto somente leitura. Falhou → apaga o `.tmp`, registra `Error`, e a limpeza não roda nesse ciclo (os backups antigos são justamente o que se quer preservar quando algo vai mal).
- **Quando:**
  - Diário: depois que a janela abre, em segundo plano via `FireAndForget`, se o backup diário válido mais recente tiver mais de 24 h. A data vem do nome do arquivo, não do relógio do sistema de arquivos (que muda ao copiar a pasta para outro computador).
  - Antes de atualizar: no `DatabaseInitializer`, de forma síncrona (a UI ainda não existe).
- **Retenção:** depois de cada backup válido, mantém os 7 diários e os 3 de atualização mais recentes, ordenados pelo nome. Arquivos que não seguem o padrão de nome ficam intocados: se o usuário guardou uma cópia ali, o app não apaga.
- **Relógio:** `TimeProvider` injetado, como no limite de notificações, para os testes controlarem "hoje" e "ontem".
- **Restaurar** fica manual nesta spec (fechar o app, copiar o backup sobre `vibePlayer.db`) e documentado na wiki. Restaurar pela interface precisa de uma tela que confirme a troca e reinicie o app; isso fica na spec de configurações.

### Preferências

```csharp
// Core
public sealed record SettingKey<T>(string Name, T Default, Func<T, bool>? IsValid = null);

public static class UiSettings
{
    public static readonly SettingKey<bool> SidePanelOpen = new("ui.sidePanel.open", true);
    public static readonly SettingKey<double> SidePanelWidth = new("ui.sidePanel.width", 380, w => w > 0);
    // ...
}
```

- O valor é gravado como JSON (System.Text.Json). Um tipo novo não precisa de coluna nem de migration, e a tabela serve para todas as specs.
- Na leitura, JSON inválido ou um valor que falha no `IsValid` vira o padrão, com `Warning` no log (só a chave, nunca o valor).
- **Chaves com prefixo por área** (`ui.`, `player.`, `llm.`): os nomes são permanentes, porque mudar um nome "esquece" a preferência dos usuários.
- Limites de domínio continuam no ViewModel. Uma largura salva de 900 px vira 560 pelo `ClampWidth` já existente, e o store só rejeita o absurdo (≤ 0).

### Estado da janela

- `WindowStateTracker` (App) lê as preferências no `Program`, antes da janela existir, e aplica ao `MainWindowViewModel` e à `MainWindow`: tamanho e maximizada.
- A gravação acontece depois de 500 ms sem novas mudanças.
  - Implementada com `DispatcherTimer`, que roda na thread de UI e não cria thread própria, seguindo a regra de exceções.
  - Ao fechar a janela, grava na hora o que estiver pendente (`Closing` aguarda a gravação, com limite de 2 s para nunca travar o fechamento).
- Tamanho salvo = tamanho "restaurado" (não o maximizado). Assim, desmaximizar volta ao tamanho que o usuário escolheu. Tamanho maior que a tela atual é reduzido para caber.

## Decisões

| Decisão | Motivo | Alternativas descartadas |
|---|---|---|
| EF Core + SQLite com migrations | Já definido no CLAUDE.md; migrations versionam o schema entre versões instaladas | Dapper com scripts SQL próprios (mais código para o mesmo resultado); LiteDB (sem SQL, ferramentas piores) |
| `IDbContextFactory` com contextos curtos | Processo de longa duração e acesso de várias threads | Contexto singleton (não é thread-safe); scoped (app desktop não tem "requisição") |
| Migrar antes da UI, com backup e restauração | Falha no upgrade nunca destrói dados; UI nunca vê banco pela metade | Migrar em segundo plano com a UI aberta; banco novo em caso de falha (perde dados) |
| Backup pela API online do SQLite + `integrity_check` | Cópia consistente com o app aberto; backup ruim nunca substitui um bom | Cópia de arquivo (exige fechar conexões e pode pegar o banco pela metade); `VACUUM INTO` (também consistente, mas sem progresso e reescreve o banco inteiro) |
| Backup diário na abertura, em segundo plano | Cobre o uso real (o app é aberto quase todo dia) sem atrasar a abertura | Backup ao fechar (atrasa o fechamento e não roda se o app travar); timer periódico (mais complexo para pouco ganho) |
| Preferências chave/valor em JSON | Uma tabela para todas as specs; tipo novo sem migration | Uma coluna por preferência (migration a cada configuração nova) |
| WAL | UI lê enquanto a transcrição grava, sem `database is locked` | Modo padrão (rollback journal) |
| Gravação com atraso de 500 ms + gravação ao fechar | Arraste da divisória não vira centenas de escritas; nada se perde ao fechar | Gravar a cada mudança; gravar só ao fechar (perde tudo num travamento) |
| `dotnet-ef` no manifesto local (`.config/dotnet-tools.json`) | Mesma versão do EF para todo mundo e na CI; `dotnet tool restore` instala | Ferramenta global (versão varia por máquina) |

## Mudanças em dados

Migration `InicialPreferencias`:

| Tabela | Coluna | Tipo | Observação |
|---|---|---|---|
| `Settings` | `Key` | TEXT, PK | Ex.: `ui.sidePanel.width` |
| | `Value` | TEXT, não nulo | JSON |
| | `UpdatedAt` | TEXT (ISO 8601 UTC) | Diagnóstico |

É a primeira migration, então não há bancos anteriores a preservar. O teste de CA3 usa um banco criado só com esta migration para validar o mecanismo para as próximas.

## Dependências novas

| Pacote | Projeto | Licença |
|---|---|---|
| Microsoft.EntityFrameworkCore.Sqlite 10.0.14 | Infrastructure | MIT |
| ↳ SQLitePCLRaw.bundle_e_sqlite3 2.1.x (traz a biblioteca nativa `e_sqlite3` por RID) | Infrastructure (transitiva) | Apache 2.0 |
| ↳ SQLite (código nativo) | — | Domínio público |
| Microsoft.EntityFrameworkCore.Design 10.0.14 | Infrastructure, só build (`PrivateAssets=all`, não vai no pacote) | MIT |
| dotnet-ef 10.0.14 | Ferramenta local de desenvolvimento | MIT |

Os três primeiros entram em `THIRD-PARTY-NOTICES.md`.

## Logs e erros

- `Information`:
  - "Banco atualizado: {Count} migrations aplicadas em {Elapsed}", com os nomes das migrations em `Debug`;
  - "Backup {Kind} criado em {Path} ({Size} bytes, {Elapsed})";
  - "Backups antigos removidos: {Count}".
- `Warning`:
  - banco de versão mais nova que o app;
  - preferência inválida ignorada ({Key});
  - gravação do estado ao fechar excedeu o limite.
- `Error`:
  - falha ao gravar preferência (a UI segue; a preferência só não é lembrada);
  - falha ao criar ou verificar um backup diário (a UI segue; tenta de novo na próxima abertura).
- `Fatal`: falha ao migrar, com a exceção completa e a confirmação de que o backup foi restaurado.
- Logs do próprio EF Core ficam em `Warning` ou acima (o override de `Microsoft` já existe). SQL gerado nunca vai para o log em release.
- Janela de erro de atualização (textos em `Strings.resx`): "Não foi possível atualizar seus dados", seguido da explicação "Seus dados foram preservados em {caminho}. Os detalhes estão no log." e do botão "Fechar".

## Riscos

- **Biblioteca nativa no macOS/Linux:** o `e_sqlite3` precisa ir em cada pacote self-contained. O bundle traz `runtimes/<rid>/native`; CA9 confere nos quatro pacotes na próxima release.
- **Restauração com WAL:** restaurar o arquivo sem limpar o `-wal`/`-shm` antigos misturaria estados. Mitigação: fechar as conexões (`SqliteConnection.ClearAllPools()`) e apagar `-wal`/`-shm` antes de copiar o backup de volta.
- **Banco já corrompido:** o backup diário de um banco corrompido falha na verificação e não é contado. Os 7 anteriores ficam, e o log aponta o problema. Detectar e avisar o usuário na UI fica para quando houver dados que valham o aviso (biblioteca).
- **Espaço em disco:** com transcrições o banco pode chegar a dezenas de MB; 10 backups multiplicam isso. Aceitável para um app desktop; a tela de configurações poderá ajustar a quantidade.
- **Duas instâncias abertas:** o SQLite aguenta, mas a segunda instância pode tentar migrar enquanto a primeira usa o banco. Mitigação: `busy_timeout` de 5 s; o bloqueio de instância única fica para outra spec.
- **Antivírus ou arquivo bloqueado no Windows** ao copiar ou restaurar: tratado como falha de migração (mensagem e encerramento), nunca como banco novo.
- **Migrations geradas em SO diferente:** o EF gera o mesmo código em qualquer SO; o teste de CA2 roda nos três na CI.

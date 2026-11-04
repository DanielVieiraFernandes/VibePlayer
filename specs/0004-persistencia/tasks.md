# 0004 — Tarefas

Marque cada tarefa ao concluir.

## Implementação

- [x] T1 — Pacotes do EF Core (Sqlite e Design) em `Directory.Packages.props` e `Infrastructure`; `dotnet-ef` no manifesto de ferramentas local; licenças em `THIRD-PARTY-NOTICES.md`.
- [x] T2 — `VibePlayerDbContext`, `SettingEntry` e configuração; `VibePlayerDesignTimeFactory`; registro com `AddDbContextFactory` (WAL, `foreign_keys`, `busy_timeout`) em `AddInfrastructure`.
- [x] T3 — Migration `InicialPreferencias`.
- [x] T4 — `IBackupService` (Core) e `SqliteBackupService` (Infrastructure): backup online, verificação de integridade, nomes por tipo, retenção.
- [x] T4a — `IDatabaseInitializer` (Core) e `DatabaseInitializer` (Infrastructure): backup de atualização, migrate, restauração em falha, aviso de banco mais novo.
- [x] T4b — Backup diário em segundo plano depois que a janela abre.
- [x] T5 — `SettingKey<T>`, `ISettingsStore` (Core) e `SqliteSettingsStore` (Infrastructure), com padrão para valor ausente/inválido.
- [x] T6 — `Program`: inicializar o banco antes da UI; janela de erro de atualização e código de saída 2.
- [x] T7 — `UiSettings` e `WindowStateTracker`: restaurar painel e janela ao abrir; gravação com atraso de 500 ms e ao fechar.
- [x] T8 — Textos novos em `Strings.resx`.
- [x] T9 — Atualizar CLAUDE.md (Persistência, Comandos com `dotnet tool restore` e o comando de migration atualizado).

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1 | E2E | `DatabaseTests.PrimeiraAbertura_CriaBancoComMigrationsAplicadas` |
| CA2 | Unitário | `MigrationTests.Modelo_NaoTemMudancasSemMigration` |
| CA3 | E2E | `DatabaseTests.BancoAntigo_CriaBackupEAplicaMigrationsPreservandoDados` |
| CA4 | E2E | `DatabaseTests.FalhaNaMigration_RestauraBackupEMostraErro` e `JanelaDeErro_MostraOndeEstaOBackup` |
| CA5 | Unitário | `SqliteSettingsStoreTests.ChaveAusente_DevolvePadrao` e `ValorGravado_EhLidoDeVolta` |
| CA6 | E2E | `WindowStateTests.Reabertura_RestauraPainelEJanela` e `Reabertura_RestauraJanelaMaximizadaSemPerderOTamanhoNormal` |
| CA7 | E2E | `WindowStateTests.MudancasRapidas_GravamUmaVez` e `FecharLogoAposMudar_NaoPerdeAMudanca` |
| CA8 | Unitário | `SqliteSettingsStoreTests.ValorInvalidoOuCorrompido_DevolvePadraoERegistraAviso` |
| CA9 | Manual | `dotnet publish` local confirma `e_sqlite3` em `win-x64`, `osx-arm64` e `linux-x64`; **falta** abrir os quatro pacotes da próxima release |
| CA10 | Unitário | `SqliteBackupServiceTests.PrimeiraAberturaDoDia_CriaBackupValido` e `SegundaAberturaDoDia_NaoCriaBackup` |
| CA11 | E2E | `BackupTests.BackupComAppGravando_ContemDadosAteOMomento` |
| CA12 | Unitário | `SqliteBackupServiceTests.Limpeza_MantemOsMaisRecentesPorTipo` |
| CA13 | Unitário | `SqliteBackupServiceTests.FalhaNoBackup_RegistraErroEPreservaBackupsAnteriores` |

Testes unitários usam SQLite **em memória** (conexão aberta durante o teste), sem tocar no disco. Os de backup são exceção: o produto deles é um arquivo, então usam pasta temporária própria, apagada ao fim. Os E2E usam arquivo real em pasta temporária.

## Documentação

- [x] CLAUDE.md (T9)
- [x] `DESIGN.md` — não se aplica (só a janela de erro, com os componentes existentes)
- [ ] Wiki — FAQ: onde ficam os dados, como zerar o app e como restaurar um backup (**pendente**: a wiki ainda não existe; criar junto com a primeira página de uso)

## Desvios em relação ao design

- **Manifesto do `dotnet-ef` na raiz** (`dotnet-tools.json`), onde o SDK atual o cria, e não em `.config/`.
- **Startup project das migrations é o próprio `Infrastructure`** (via `VibePlayerDesignTimeFactory`); o comando no CLAUDE.md foi atualizado.
- **CA7 só em E2E:** o agrupamento de gravações usa `DispatcherTimer`, que precisa do dispatcher do Avalonia; o teste roda headless com o atraso encurtado (`WindowStateTracker.SaveDelay`).
- **Backup diário pela data local** ("primeira abertura do dia"), como na spec, e não "mais de 24 h", como estava no design.
- **Backups em modo `DELETE`:** a cópia herda o WAL do banco e deixaria `-wal`/`-shm` órfãos; o backup é convertido para um arquivo único antes da verificação. Achado rodando o app real; `SqliteBackupServiceTests` agora usa banco em WAL.
- **`DatabaseInitializer` tem um construtor com o passo de migrate injetável**, só para o teste simular uma migration que falha no meio.
- **`WindowStateTracker` aplica a cada janela o estado lido na inicialização**, não o da janela anterior, para janelas criadas em sequência (testes) nascerem iguais.
- **`TimeProvider` passou a ser registrado em `AddInfrastructure`** (antes ficava no App), porque backups e preferências também usam.

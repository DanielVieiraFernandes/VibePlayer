# 0004 — Tarefas

Marque cada tarefa ao concluir.

## Implementação

- [ ] T1 — Pacotes do EF Core (Sqlite e Design) em `Directory.Packages.props` e `Infrastructure`; `dotnet-ef` no manifesto de ferramentas local; licenças em `THIRD-PARTY-NOTICES.md`.
- [ ] T2 — `VibePlayerDbContext`, `SettingEntry` e configuração; `VibePlayerDesignTimeFactory`; registro com `AddDbContextFactory` (WAL, `foreign_keys`, `busy_timeout`) em `AddInfrastructure`.
- [ ] T3 — Migration `InicialPreferencias`.
- [ ] T4 — `IBackupService` (Core) e `SqliteBackupService` (Infrastructure): backup online, verificação de integridade, nomes por tipo, retenção.
- [ ] T4a — `IDatabaseInitializer` (Core) e `DatabaseInitializer` (Infrastructure): backup de atualização, migrate, restauração em falha, aviso de banco mais novo.
- [ ] T4b — Backup diário em segundo plano depois que a janela abre.
- [ ] T5 — `SettingKey<T>`, `ISettingsStore` (Core) e `SqliteSettingsStore` (Infrastructure), com padrão para valor ausente/inválido.
- [ ] T6 — `Program`: inicializar o banco antes da UI; janela de erro de atualização e código de saída 2.
- [ ] T7 — `UiSettings` e `WindowStateTracker`: restaurar painel e janela ao abrir; gravação com atraso de 500 ms e ao fechar.
- [ ] T8 — Textos novos em `Strings.resx`.
- [ ] T9 — Atualizar CLAUDE.md (Persistência, Comandos com `dotnet tool restore` e o comando de migration atualizado).

## Testes

| Critério | Tipo | Teste |
|---|---|---|
| CA1 | E2E | `DatabaseTests.PrimeiraAbertura_CriaBancoComMigrationsAplicadas` |
| CA2 | Unitário | `MigrationTests.Modelo_NaoTemMudancasSemMigration` |
| CA3 | E2E | `DatabaseTests.BancoAntigo_CriaBackupEAplicaMigrationsPreservandoDados` |
| CA4 | E2E | `DatabaseTests.FalhaNaMigration_RestauraBackupEMostraErro` |
| CA5 | Unitário | `SqliteSettingsStoreTests.ChaveAusente_DevolvePadrao` e `ValorGravado_EhLidoDeVolta` |
| CA6 | E2E | `WindowStateTests.Reabertura_RestauraPainelEJanela` |
| CA7 | Unitário + E2E | `WindowStateTrackerTests.MudancasRapidas_GravamUmaVez`; `WindowStateTests.FecharLogoAposMudar_NaoPerdeAMudanca` |
| CA8 | Unitário | `SqliteSettingsStoreTests.ValorInvalidoOuCorrompido_DevolvePadraoERegistraAviso` |
| CA9 | Manual | Abrir os pacotes `win-x64`, `osx-arm64`, `osx-x64` e `linux-x64` da próxima release |
| CA10 | Unitário | `SqliteBackupServiceTests.PrimeiraAberturaDoDia_CriaBackupValido` e `SegundaAberturaDoDia_NaoCriaBackup` |
| CA11 | E2E | `BackupTests.BackupComAppGravando_ContemDadosAteOMomento` |
| CA12 | Unitário | `SqliteBackupServiceTests.Limpeza_MantemOsMaisRecentesPorTipo` |
| CA13 | Unitário | `SqliteBackupServiceTests.FalhaNoBackup_RegistraErroEPreservaBackupsAnteriores` |

Testes unitários usam SQLite **em memória** (conexão aberta durante o teste), sem tocar no disco. Os de backup são exceção: o produto deles é um arquivo, então usam pasta temporária própria, apagada ao fim. Os E2E usam arquivo real em pasta temporária.

## Documentação

- [ ] CLAUDE.md (T9)
- [ ] `DESIGN.md` — não se aplica (só a janela de erro, com os componentes existentes)
- [ ] Wiki — FAQ: onde ficam os dados, como zerar o app e como restaurar um backup

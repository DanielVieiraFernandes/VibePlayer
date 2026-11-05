# 0009 — Design técnico

## Abordagem

Os instaladores empacotam a mesma saída de `dotnet publish` que já vai nos `.zip` e `.tar.gz`, sem recompilar nada e sem tocar em `Core`, `Infrastructure` ou `App`. Tudo fica em arquivos novos de empacotamento e na pipeline:

```
packaging/
  windows/VibePlayer.iss        Script do Inno Setup
  macos/Info.plist              Modelo, com a versão preenchida no build
  macos/build-dmg.sh            Monta o .app, assina ad-hoc, gera o .dmg
  linux/control                 Modelo do pacote, com a versão preenchida no build
  linux/vibeplayer.desktop      Entrada do menu de aplicativos
  linux/build-deb.sh            Monta a árvore e gera o .deb
.github/scripts/
  install-inno-setup.ps1        Instala a versão fixada, conferindo o SHA-256
  package-version.sh            Lê a versão do MinVer e confere com a tag
  smoke-test-windows.ps1        Instala, abre, transcreve, atualiza, remove
  smoke-test-macos.sh           Monta o .dmg, copia, transcreve, remove
  smoke-test-linux.sh           apt install, transcreve, apt remove
.github/workflows/
  build-packages.yml            Novo, reutilizável (workflow_call): publica, empacota e testa
  check-packages.yml            Novo: dispara a conferência antes do merge em main
  release.yml                   O job build passa a chamar build-packages.yml
```

### Fluxo na pipeline

```
release.yml:        prepare (tag) → build-packages.yml → publish → back-merge
check-packages.yml:                 build-packages.yml (sem publicar)

build-packages.yml, por RID:
  publish → conferir plugins → .zip/.tar.gz → instalador → teste do instalador → upload
```

**Por que a conferência precisa rodar antes do merge:** `release.yml` cria a tag antes de gerar os pacotes, então uma falha ali deixa uma tag sem release. Rodar o mesmo workflow no push de `release/*` e `hotfix/*` mostra o problema no PR, enquanto ainda dá para corrigir. A criação da branch usa o `RELEASE_TOKEN`, que dispara workflows. O teste em `release.yml` continua como última barreira.

**Gatilhos de `check-packages.yml`:**
- `push` em `release/**` e `hotfix/**`, sem filtro;
- `pull_request` para `develop` com filtro de caminhos (`packaging/**`, `.github/workflows/build-packages.yml`, os scripts acima, `src/VibePlayer.App/*.targets`, `*.csproj`, `Directory.Packages.props`).

Fica fora dos checks obrigatórios dos rulesets: um check com filtro de caminhos que não dispara deixaria o PR preso. `ci.yml` não muda, e o tempo da CI comum continua igual.

### Teste do instalador

O ponto central é rodar o binário **já instalado**, porque é onde o empacotamento quebra: VLC e Whisper procuram nativos a partir do executável. O próprio app já tem o que é preciso: `--transcription-worker` recebe um job JSON pelo stdin e emite eventos JSON por linha.

Em cada sistema, o script passa `tests/fixtures/audio/fala-jfk.m4a` e o modelo tiny (`download-whisper-test-model.sh`, com `ForceCpu`) ao executável instalado, e exige o evento `Done`. Isso prova, sem janela e sem escrever código novo no app:
- runtime .NET self-contained;
- LibVLC e plugins, incluindo `transcode` e `smem`;
- nativos do Whisper.

No Linux, o runner `ubuntu-latest` não tem VLC, e este job não o instala (o script confere isso antes de começar). Assim, se faltar algo em `Depends`, o teste falha. Isso é justamente o que o `.deb` promete.

| Verificação | Windows | macOS | Linux |
|---|---|---|---|
| Instalar em silêncio | `/VERYSILENT /SUPPRESSMSGBOXES` | Montar `.dmg`, copiar para `/Applications` | `apt-get install ./arquivo.deb` |
| Avisos de licença no disco | Sim | Sim | Sim |
| Transcrever a amostra | Sim | Sim (x64 via Rosetta; sem Rosetta no runner, só verificação estrutural) | Sim |
| Assinatura ad-hoc válida | Não se aplica | `codesign --verify --deep --strict` | Não se aplica |
| Abrir a janela | Sim: processo vivo após 15 s | Roteiro manual | Roteiro manual |
| Atualizar a partir da release anterior | Sim: baixa o instalador anterior, se existir | Roteiro manual | Sim, a partir da próxima release |
| Remover em silêncio | `unins000.exe /VERYSILENT` | Apagar o `.app` | `apt-get remove` e `purge` |
| Marcador na pasta de dados sobrevive | Sim | Trivial: nada a toca | Sim |

A primeira versão com instalador não tem de onde atualizar; nesse caso o teste de atualização registra aviso e segue. O CA4 é coberto pelo roteiro manual gerando dois instaladores.

### Windows

**Instalação por usuário:** `PrivilegesRequired=lowest`, com `DefaultDirName={autopf}\VibePlayer`, que nesse modo resolve para `%LOCALAPPDATA%\Programs\VibePlayer`. `AppendDefaultDirName=yes` garante uma subpasta própria mesmo se o usuário escolher outro lugar.

**Identidade:** `AppId` é um GUID gerado uma vez e nunca mais trocado. Ele é o que faz uma versão nova reconhecer a anterior. O app é 64 bits (`ArchitecturesAllowed=x64compatible`).

**Versão:** `iscc /DAppVersion=<MinVer> /DAppNumericVersion=<X.Y.Z> packaging\windows\VibePlayer.iss`. O script não tem valor padrão: compilar sem a versão é erro.

**Atualização:** o Inno instala por cima sem chamar o desinstalador, então a pergunta sobre os dados não aparece ao atualizar. `[InstallDelete]` apaga `{app}\libvlc` e `{app}\runtimes` antes de copiar, porque um plugin retirado da lista de permissão ficaria no disco. Apagar `{app}\*` por inteiro está descartado: é uma pasta que o usuário pode ter escolhido.

**App aberto:** `CloseApplications=yes`, que usa o Restart Manager do Windows. O app não tem mutex, então `AppMutex` não serve, e adicioná-lo seria mudar código. O processo de transcrição não tem janela, mas sai sozinho quando o stdin fecha, e a aula interrompida recomeça (comportamento da spec 0007). `/CLOSEAPPLICATIONS` cobre o modo silencioso.

**Versão mais antiga:** `InitializeSetup` lê `DisplayVersion` em `HKCU\...\Uninstall\{AppId}_is1` e compara com a versão do instalador. Se a instalada for mais nova, mostra a mensagem e sai.

**Desinstalação:** em `CurUninstallStepChanged`, no passo `usPostUninstall`:

```pascal
{ Em modo silencioso nunca apaga: um script de implantação não respondeu a nada. }
if (not UninstallSilent) and
   (MsgBox(Mensagem, mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
  DelTree(ExpandConstant('{userappdata}\VibePlayer'), True, True, True);
```

`MB_DEFBUTTON2` faz de **Não** a resposta padrão, e fechar a caixa também conta como não.

### macOS

Sem ferramenta nova: `hdiutil` e `codesign` já estão no sistema.

- **Estrutura:** `VibePlayer.app/Contents/MacOS` recebe a saída do publish inteira, como a documentação do Avalonia recomenda. Assim, os caminhos relativos do VLC e do Whisper continuam valendo. Os avisos de licença ficam em `Contents/Resources`, e `Contents/Info.plist` sai do modelo.
- **Identificador:** `io.github.danielvieirafernandes.vibeplayer`, permanente.
- **Versão:** `CFBundleShortVersionString` e `CFBundleVersion` usam `X.Y.Z`, porque o macOS não aceita sufixo de pré-lançamento.
- **Assinatura ad-hoc:** `codesign --force --deep --sign - VibePlayer.app`. Copiar `Info.plist` e reorganizar arquivos invalida o que veio do publish, e o Apple Silicon recusa código sem assinatura válida. Ela não substitui a assinatura de desenvolvedor: o Gatekeeper continua bloqueando.
- **Versão mínima:** `LSMinimumSystemVersion` segue o suporte do .NET 10, conferido ao implementar.
- **Caminho dos dados:** `AppPaths` usa `SpecialFolder.ApplicationData`, e o código diz `~/.config`, mas isso não foi conferido num Mac. A wiki só cita o caminho depois de confirmado (T15).

**LGPL:** o usuário continua podendo trocar as bibliotecas do LibVLC dentro do `.app`. Isso invalida a assinatura ad-hoc, que basta refazer com o mesmo comando, e a wiki explica.

### Linux

`dpkg-deb --root-owner-group --build`, disponível no runner. Conteúdo:

```
/opt/vibeplayer/                       Saída do publish, com licenças
/usr/bin/vibeplayer                    Link para /opt/vibeplayer/VibePlayer
/usr/share/applications/vibeplayer.desktop
```

- **Sem scripts `postinst`/`postrm`:** nada a fazer, e sem eles nem o `purge` chega perto da pasta do usuário.
- **Atualização:** o `dpkg` troca os arquivos e remove os que saíram do pacote.
- **Dependências:** o `Depends` repete o que a CI instala para o VLC e o que o .NET e o Avalonia pedem:
  - `libvlc5, vlc-plugin-base, vlc-plugin-video-output`;
  - `libc6, libgcc-s1, libstdc++6, libssl3 | libssl3t64, libicu78 | libicu76 | libicu74 | libicu72`;
  - `libx11-6, libice6, libsm6, libfontconfig1`.
- **Versão:** troca `-` por `~` (`0.2.1~alpha.0.1`), para um pré-lançamento ficar abaixo da versão final na ordenação do `dpkg`. Em release é só `X.Y.Z`.

**Licenças:** declarar dependência não é distribuir. O `.deb` não leva nenhum binário do VLC, e o `apt` do usuário baixa do repositório da distribuição, como já acontece com quem instala o VLC à mão. `vlc-plugin-base` tem partes GPL, mas o pacote do VibePlayer não as contém. É a mesma situação de hoje no Linux. O `dpkg-deb` é GPL, mas é ferramenta de build e sua saída não fica sujeita a isso.

## Decisões

### Ferramenta do Windows

| Critério | Inno Setup 7 (recomendado) | WiX 6/7 (`.msi`) | NSIS 3 | Velopack |
|---|---|---|---|---|
| Licença | Própria e permissiva; permite uso comercial e software fechado | MS-RL (recíproca; pacotes gerados em geral ficam fora) | zlib, com módulos CPL e outros | MIT |
| Custo ou condição | Pede, sem obrigar, licença comercial acima de US$ 5.000/ano | Exige patrocínio (OSMF) acima de US$ 10.000/ano; v7 exige aceitar EULA | Nenhum | Nenhum |
| Instalação por usuário | Sim, nativa | Sim, mais trabalhosa | Manual | Sim, é o único modo |
| Pergunta ao desinstalar | Poucas linhas em Pascal | Ação personalizada e diálogo próprio | Script próprio | Hook dentro do app |
| Assistente pt-BR | Tradução oficial incluída | Escrever a localização | Incluída | Sem assistente |
| Instalar por cima | Nativo, com fechamento do app | `MajorUpgrade`; regras de GUID de componente | Manual | Nativo |
| Precisa mudar o app | Não | Não | Não | Sim: `VelopackApp.Build().Run()` no início do `Main` |
| Modo silencioso | Sim | Sim (`msiexec /qn`) | Sim | Sim |
| No runner | 6.7.1 hoje, sem garantia | Só WiX 3.14; v6/v7 via .NET tool | Ausente no Windows 2025 | .NET tool |

**Recomendação: Inno Setup 7, gerando `.exe`.** Atende a tudo que a spec pede sem código no app, com o menor volume de script.
- O `.msi` compensa quando empresas implantam por GPO ou Intune, o que não é o caso de um player pessoal. Ele traz custo real de complexidade e a OSMF.
- O NSIS é viável, mas exige escrever à mão o que o Inno já traz.
- O Velopack é forte candidato para a futura atualização automática, porém mistura as duas coisas agora: exige mudar `Program.cs` e não tem assistente, tela de licença nem pergunta sobre dados fora do app.
- MSIX está descartado: exige assinatura.

**Compatibilidade com a atualização automática:** a spec futura pode baixar o instalador novo e rodar `/VERYSILENT /CLOSEAPPLICATIONS`, sem trocar de ferramenta. Se escolher o Velopack, precisará migrar quem instalou pelo Inno, e por isso `AppId` e pasta ficam documentados.

### Demais decisões

| Decisão | Motivo | Alternativas descartadas |
|---|---|---|
| Fixar o Inno em 7.1.1 com SHA-256 (`install-inno-setup.ps1`) | O runner traz 6.7.1, que pode mudar ou sair da imagem; a mesma versão gera o mesmo instalador. Mesmo critério do modelo do Whisper | Usar o da imagem; `choco`/`winget`, que não fixam o conteúdo |
| Instalação só por usuário | Os dados são por usuário: o desinstalador só alcança a pasta de quem o rodou. Sem administrador, há um aviso a menos. Evita duas cópias convivendo | Oferecer os dois modos: duas instalações paralelas e perguntas sobre dados que não alcançam outros usuários |
| Silencioso nunca apaga dados | Apagar exige uma resposta humana | Parâmetro `/DELETEDATA`: ninguém pediu, e é um risco a mais |
| `.dmg` por arquitetura | Já há um pacote por RID, e um `.app` universal dobraria o tamanho do download | `.app` universal; `.pkg`, que exige assinatura para ser prático e não traz vantagem para um app sem serviços |
| `.deb` como formato do Linux | O app depende do VLC do sistema, e só um pacote nativo declara isso. O `apt` resolve sozinho, e Debian e Ubuntu são a maior base | AppImage: não declara dependência, exigiria embutir o VLC (com plugins sob outra lista de permissão) e pede FUSE 2, ausente por padrão no Ubuntu 24.04. Flatpak e Snap: sandbox atrapalha o acesso a pastas de cursos; exigem manifesto e publicação. `.rpm`: fica para quando houver demanda |
| Manter `.zip` e `.tar.gz` | Pedido da spec; atendem instalação portátil e outras distribuições | Substituir pelos instaladores |
| Instalador só em pt-BR | É o único idioma do app hoje | Vários idiomas, sem uso |
| Textos do instalador no `.iss` (`[CustomMessages]`) | O instalador não carrega o `Strings.resx`, e não há app rodando | Gerar a partir do `.resx`: complexidade sem ganho com um idioma só |
| Workflow reutilizável | Release e conferência precisam gerar exatamente o mesmo pacote | Duplicar passos em dois arquivos |
| Testar com o worker, sem flag nova | Já existe e prova VLC e Whisper instalados; `--version` provaria só o .NET | Criar `--smoke-test` no app: código de produção só para a CI |

## Mudanças em dados

Nenhuma: sem tabelas, sem migrations e sem chaves novas. O instalador só pode apagar a pasta de dados com confirmação explícita (CA7). As chaves de API ainda não existem. Quando a spec de provedores criar o `ISecretStore`, precisa decidir se o desinstalador limpa também o cofre do sistema: fica anotado aqui para ela.

## Dependências novas

Nenhum pacote NuGet e nada que vá dentro do app, exceto o que o Inno embute no instalador e no desinstalador.

| Ferramenta | Versão | Licença | Uso | Distribuída? |
|---|---|---|---|---|
| Inno Setup | 7.1.1 | Licença própria (permite uso comercial e software fechado, mantendo os avisos de copyright) | Gerar o `.exe` | O código do instalador e `unins000.exe`: **entra em `THIRD-PARTY-NOTICES.md`** |
| `dpkg-deb` | do runner | GPL-2.0+ | Gerar o `.deb` | Não; só ferramenta de build |
| `hdiutil`, `codesign` | do runner | Ferramentas da Apple | Gerar o `.dmg` e assinar | Não |

A licença do Inno foi lida em `https://jrsoftware.org/files/is/license.txt` em novembro de 2026. A cobrança comercial é um pedido à parte, descrito pelo próprio site como "not strictly required" (ver Q1). O texto vale para a versão fixada, e a tarefa T2 confere de novo antes de fixar o hash.

## Logs e erros

- O Inno grava log com `/LOG=<arquivo>` nos testes da CI, publicado como artefato em caso de falha. Nenhum arquivo do app é afetado.
- Falhas de instalação aparecem no assistente, ou como código de saída diferente de zero no modo silencioso.
- A recusa de versão antiga (CA5) é uma mensagem do assistente e um código de saída diferente de zero no modo silencioso.
- Os scripts da CI usam `::error::` com o motivo, como `check-vlc-plugins.sh`. O texto das aulas nunca é impresso: o teste só olha o tipo do evento, nunca os segmentos.

## Riscos

| Risco | Mitigação |
|---|---|
| Antivírus marca instalador sem assinatura como suspeito | Aviso na wiki; é um argumento para a futura spec de assinatura |
| Smart App Control bloqueia sem opção de continuar | Documentado; depende de assinatura, que está fora do escopo |
| `codesign --deep` recusa arquivos que não são código em `Contents/MacOS` | Validar com `--verify --strict` no teste; se recusar, assinar cada `.dylib` e o executável antes do bundle |
| Sem Rosetta no runner, o `.dmg` x64 não executa na CI | Só verificação estrutural nele, com o roteiro manual em Mac Intel, se houver |
| `Depends` varia entre distribuições (`libicu`, `libssl`) | Alternativas com `\|`; o teste cobre o Ubuntu do runner, e o roteiro manual inclui Debian |
| `LICENSE` em UTF-8 sem BOM aparece com acentos errados no assistente | Conferir na implementação; se preciso, o script passa uma cópia com BOM ao Inno |
| Tag criada e instalador falhando em `release.yml` | A conferência roda antes do merge; se escapar, "Re-run failed jobs" ou hotfix |
| Fechar o app no meio de uma transcrição | A aula recomeça; é o comportamento conhecido da spec 0007 |
| Trabalho em paralelo da spec 0008 | Esta spec não toca em Views nem em Styles |

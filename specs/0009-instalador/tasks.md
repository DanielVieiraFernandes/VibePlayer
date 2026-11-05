# 0009 — Tarefas

Marque cada tarefa ao concluir.

## Implementação

Base:
- [ ] T1 — `package-version.sh`: ler `MinVerVersion`, `Major`, `Minor` e `Patch` com `dotnet msbuild -t:MinVer -getProperty:...` e falhar se divergir da tag numa release.
- [ ] T2 — `install-inno-setup.ps1`: instalar o Inno Setup 7.1.1 com SHA-256 fixado, depois de reler a licença dessa versão.

Windows:
- [ ] T3 — `packaging/windows/VibePlayer.iss`: identidade (`AppId` gerado uma vez), pasta, instalação por usuário, pt-BR, licença, atalhos, `[InstallDelete]`, `CloseApplications`, opção de abrir o app ao concluir.
- [ ] T4 — `[Code]`: recusa de versão antiga e pergunta sobre os dados na desinstalação.
- [ ] T5 — Conferir no assistente real os acentos de `LICENSE` (a documentação do Inno aceita UTF-8 sem BOM).

macOS:
- [ ] T6 — `packaging/macos/Info.plist` e `build-dmg.sh`: `.app`, avisos de licença, assinatura ad-hoc e `.dmg` com atalho para Aplicativos.

Linux:
- [ ] T7 — `packaging/linux/control`, `vibeplayer.desktop` e `build-deb.sh`.

Pipeline:
- [ ] T8 — `build-packages.yml` (reutilizável): mover os passos de `build` de `release.yml`, sem mudar `.zip` e `.tar.gz`, e acrescentar instalador e teste por RID.
- [ ] T9 — `smoke-transcription.ps1`, `smoke-test-windows.ps1`, `smoke-test-macos.sh` e `smoke-test-linux.sh`, com a matriz do `design.md`.
- [ ] T10 — `check-packages.yml` com os gatilhos do `design.md`.
- [ ] T11 — `release.yml`: chamar o workflow reutilizável e atualizar as notas da release (qual arquivo baixar, avisos de segurança).
- [ ] T12 — Acompanhar `check-packages.yml` no PR desta branch e corrigir o que aparecer nos três sistemas.

Conferências finais:
- [ ] T13 — Executar o roteiro manual e anotar no PR o que não pôde ser testado.
- [ ] T14 — Conferir que nada em `src/`, `Views` ou `Styles` mudou e que `dotnet test VibePlayer.slnx` continua passando.
- [ ] T15 — Confirmar num Mac o caminho real da pasta de dados, antes de citá-lo na wiki.

## Testes

Esta spec não muda código C#, então não há novos testes unitários nem E2E. A prova são os testes dos instaladores instalados (CI) e o roteiro manual.

| Critério | Tipo | Teste |
|---|---|---|
| CA1 | CI + manual | `smoke-test-windows.ps1` (instalação silenciosa e transcrição) + roteiro W1 |
| CA2 | Manual | Roteiro W1 |
| CA3 | Manual | Roteiro W2 |
| CA4 | CI + manual | `smoke-test-windows.ps1` (atualização sobre 0.0.1) + roteiro W3 |
| CA5 | CI + manual | `smoke-test-windows.ps1` (instalador 0.0.1 sobre o novo, código de saída) + roteiro W4 |
| CA6, CA7 | Manual | Roteiro W5 e W6 (precisam da pergunta na tela) |
| CA8 | CI | `smoke-test-windows.ps1` (marcador sobrevive) |
| CA9 | CI + manual | `smoke-test-macos.sh` + roteiro M1 |
| CA10 | Manual | Roteiro M2 |
| CA11 | CI + manual | `smoke-test-linux.sh` (runner sem VLC) + roteiro L1 |
| CA12 | CI + manual | `smoke-test-linux.sh` (atualização sobre 0.0.1; marcador sobrevive ao `purge`) + roteiro L2 |
| CA13 | Manual | Release real, ou pré-visualização dos artefatos de `check-packages.yml` |
| CA14 | CI | `package-version.sh`; `iscc` falha sem `AppVersion` |
| CA15 | CI | Os três `smoke-test-*` (arquivos no disco) + `check-vlc-plugins.sh` no publish |
| CA16 | CI | `check-packages.yml` |
| CA17 | CI | `smoke-test-windows.ps1` |
| CA18 | Manual | Revisão da wiki |
| CA19 | Manual | Anotações no PR |

## Roteiro de teste manual

Use uma máquina ou VM limpa, com o instalador baixado pelo navegador: é o que aciona o SmartScreen e o Gatekeeper. Tenha um curso pequeno e um modelo do Whisper já baixado ou conexão.

**Windows 10 ou 11:**
- W1 — Rode o instalador e passe pelo SmartScreen. Confira: nenhum pedido de administrador, pt-BR, licença, troca de pasta, atalho na área de trabalho desmarcado. Abra pelo menu Iniciar, adicione o curso, assista, transcreva.
- W2 — Em "Aplicativos instalados", confira nome, versão e editor.
- W3 — Gere um instalador com versão maior. Com o app aberto e tocando vídeo, rode-o: o app fecha, a pasta é mantida, a versão muda, a biblioteca e o progresso continuam lá.
- W4 — Rode o instalador antigo sobre o novo: recusa com mensagem clara, nada muda.
- W5 — Desinstale e responda **Não**: programa removido, pasta `%APPDATA%\VibePlayer` intacta. Instale de novo: a biblioteca volta.
- W6 — Desinstale e responda **Sim**: a pasta de dados some, os vídeos do curso continuam onde estavam.

**macOS (Apple Silicon e, se houver, Intel):**
- M1 — Abra o `.dmg`, arraste para Aplicativos, abra: bloqueio. Libere em **Privacidade e Segurança → Abrir Mesmo Assim**. Adicione um curso, assista, transcreva.
- M2 — Troque o app pela versão nova arrastando de novo e, depois, mova-o para a Lixeira: a pasta de dados continua lá.

**Linux (Ubuntu 24.04 e Debian 13, sem VLC):**
- L1 — Rode `sudo apt install ./VibePlayer-X.Y.Z-linux-x64.deb`: o VLC vem junto. Abra pelo menu de aplicativos e por `vibeplayer`. Adicione um curso, assista, transcreva.
- L2 — Instale um `.deb` maior, depois rode `sudo apt purge vibeplayer`: arquivos do app removidos, pasta de dados intacta.

## Documentação

- [ ] `CLAUDE.md`: seção de empacotamento, pacotes publicados, estrutura (`packaging/`), `AppId` e identificador do macOS como permanentes
- [ ] `docs/backlog.md`: tirar "Instaladores" das specs previstas, manter assinatura, notarização e atualização automática, registrar `.rpm`
- [ ] `THIRD-PARTY-NOTICES.md`: incluir o Inno Setup
- [ ] `README.md`: seção de download e status, que hoje dizem não haver versões
- [ ] `DESIGN.md`: não se aplica (a UI do app não muda)
- [ ] Wiki: instalar, atualizar e remover por sistema; SmartScreen, Smart App Control e Gatekeeper; onde ficam os dados e como apagá-los; migração a partir do `.zip`

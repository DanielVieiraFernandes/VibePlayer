# 0009 — Instaladores

**Status:** Implementada
**Branch:** `feature/0009-instalador`

## Problema

Hoje o VibePlayer sai só como `.zip` (Windows) e `.tar.gz` (macOS e Linux). Quem baixa precisa extrair a pasta, escolher onde guardá-la, abrir o executável lá de dentro e criar o próprio atalho. Para atualizar, precisa trocar a pasta à mão. Não há entrada em "Aplicativos instalados" no Windows, nem ícone no menu de aplicativos do Linux, nem um `.app` no macOS. No Linux, o usuário ainda descobre sozinho que precisa do VLC.

## Objetivo

Em cada sistema, instalar, atualizar e remover o VibePlayer funciona do jeito que aquele sistema costuma fazer, sem terminal (salvo no Linux, onde é o normal) e sem perder dados. Os instaladores saem da pipeline de release, junto dos pacotes atuais, com a versão do MinVer, e cada um é instalado e testado de verdade antes de ser publicado.

| Sistema | Formato | Como instala | Como remove |
|---|---|---|---|
| Windows | `.exe` (Inno Setup) | Assistente | "Aplicativos instalados", com pergunta sobre os dados |
| macOS | `.dmg` com `VibePlayer.app` | Arrastar para Aplicativos | Arrastar para a Lixeira |
| Linux (Debian, Ubuntu e derivados) | `.deb` | `sudo apt install ./arquivo.deb`, que também instala o VLC | `sudo apt remove vibeplayer` |
| Outras distribuições Linux | `.tar.gz` atual | Sem mudança | Sem mudança |

## Escopo

**Inclui:**

Windows:
- instalador `VibePlayer-X.Y.Z-win-x64-setup.exe`, em português do Brasil, com a licença na primeira tela;
- instalação só para o usuário atual, sem pedir administrador, em `%LOCALAPPDATA%\Programs\VibePlayer` (pasta alterável na primeira instalação);
- atalho no menu Iniciar sempre, e na área de trabalho se o usuário marcar;
- entrada em "Aplicativos instalados", com nome, versão e editor;
- atualização por cima, mantendo a pasta escolhida e fechando o app se estiver aberto;
- recusa de instalar versão mais antiga por cima de uma mais nova;
- desinstalação que pergunta se apaga a pasta de dados, com **Não** como resposta padrão.

macOS:
- `VibePlayer-X.Y.Z-osx-arm64.dmg` e `VibePlayer-X.Y.Z-osx-x64.dmg`, cada um com `VibePlayer.app` e um atalho para Aplicativos;
- assinatura ad-hoc do pacote `.app` (não identifica o autor e não evita o Gatekeeper, mas mantém o app íntegro no Apple Silicon).

Linux:
- `VibePlayer-X.Y.Z-linux-x64.deb`, que instala em `/opt/vibeplayer`, cria o comando `vibeplayer` e a entrada no menu de aplicativos;
- VLC e demais bibliotecas declarados como dependências do pacote, para o `apt` instalá-los.

Pipeline e documentação:
- instaladores gerados pela pipeline de release e publicados junto dos `.zip` e `.tar.gz`, que continuam existindo;
- versão vinda do MinVer, sem número escrito em arquivo;
- `LICENSE`, `THIRD-PARTY-NOTICES.md` e `licenses/LGPL-2.1.txt` entregues em todos os formatos;
- conferência automática que instala, abre, transcreve uma amostra e remove, em cada formato;
- texto de "Sobre os avisos de segurança" nas notas da release e na wiki (SmartScreen e Gatekeeper).

**Não inclui** (fica para outra spec):
- **Atualização automática:** continua em `docs/backlog.md`. Atualizar é baixar o instalador novo.
- **Assinatura de código e notarização:** continuam no backlog. Os avisos do SmartScreen e do Gatekeeper permanecem e são explicados na wiki.
- **Ícone do app:** continua no backlog. Sem ele, instalador, atalhos e `.app` usam o ícone padrão (Q4).
- **`.rpm`, AppImage, Flatpak e Snap:** ver `design.md`. Outras distribuições seguem com o `.tar.gz`.
- **Publicação em lojas e repositórios** (Microsoft Store, Mac App Store, Homebrew, winget, repositório `apt`).
- **Associação de arquivos de vídeo** com o VibePlayer.
- **Arquivo de checksums** na release: entra com a atualização automática, que precisa dele.
- **Migração automática** de quem usa o `.zip`: a wiki explica que basta apagar a pasta antiga, porque os dados ficam fora dela.
- **Mudanças em código C#, Views e Styles:** esta spec só adiciona empacotamento, pipeline e documentação.

## Critérios de aceite

Instalador do Windows:
- [ ] CA1 — Dado o Windows sem o app, quando o usuário roda o instalador com as opções padrão, então o app fica em `%LOCALAPPDATA%\Programs\VibePlayer` sem nenhum pedido de administrador, e o atalho do menu Iniciar abre o app.
- [ ] CA2 — Dado o assistente de instalação, então ele está em português do Brasil, mostra `LICENSE` antes de instalar, deixa trocar a pasta e traz desmarcado o atalho na área de trabalho.
- [ ] CA3 — Dado o app instalado, quando o usuário abre "Aplicativos instalados", então o VibePlayer aparece com nome, versão correta e editor, e pode ser desinstalado por lá.
- [ ] CA4 — Dado o app instalado em versão anterior, com o app aberto e uma pasta personalizada, quando o usuário roda o instalador novo, então o app é fechado, a pasta é mantida, a versão muda, `libvlc` e `runtimes` não guardam arquivos da versão antiga, e os dados do usuário continuam intactos.
- [ ] CA5 — Dado o app instalado em versão mais nova, quando o usuário roda um instalador mais antigo, então a instalação é recusada com uma mensagem clara, e nada muda (o banco de uma versão nova não abre com segurança numa antiga).
- [ ] CA6 — Dado o desinstalador em modo normal, quando o usuário responde **Não** ou fecha a pergunta, então a pasta de programa some e `ApplicationData/VibePlayer` fica intacta. **Não** é a resposta padrão.
- [ ] CA7 — Dado o desinstalador em modo normal, quando o usuário responde **Sim**, então `ApplicationData/VibePlayer` é apagada. A pergunta mostra o caminho e diz o que se perde (biblioteca, progresso, transcrições, backups, modelos baixados), e que os vídeos não são tocados.
- [ ] CA8 — Dado o desinstalador em modo silencioso, então a pasta de dados **nunca** é apagada.

macOS e Linux:
- [ ] CA9 — Dado o `.dmg` correto para o processador, quando o usuário arrasta o app para Aplicativos e o libera seguindo a wiki, então ele abre, reproduz vídeo e transcreve.
- [ ] CA10 — Dado o app no macOS, quando ele é arrastado para a Lixeira, ou trocado pelo de uma versão nova, então a pasta de dados fica intacta.
- [ ] CA11 — Dado Ubuntu ou Debian sem VLC, quando o usuário roda `sudo apt install ./VibePlayer-X.Y.Z-linux-x64.deb`, então o VLC é instalado junto, o app aparece no menu de aplicativos, abre pelo comando `vibeplayer`, reproduz vídeo e transcreve.
- [ ] CA12 — Dado o `.deb` instalado, quando o usuário instala um `.deb` mais novo, então a versão muda sem resíduos da anterior. Quando roda `apt remove` ou `apt purge`, então os arquivos do app somem e a pasta de dados na pasta do usuário fica intacta.

Pipeline:
- [ ] CA13 — Dada uma release, quando a pipeline termina, então o GitHub Release traz os quatro pacotes atuais e mais `-win-x64-setup.exe`, dois `.dmg` e um `.deb`, todos com a versão da tag, e a falha de qualquer instalador impede a publicação.
- [ ] CA14 — Dado qualquer arquivo de `packaging/`, então nenhum contém número de versão do app. Toda versão vem do MinVer, e a pipeline falha se ela divergir da tag.
- [ ] CA15 — Dado qualquer instalador, quando instalado, então `LICENSE`, `THIRD-PARTY-NOTICES.md` e `licenses/LGPL-2.1.txt` estão no disco, e a lista de plugins do VLC continua conferida (nenhum plugin fora da lista de permissão).
- [ ] CA16 — Dado um push em `release/*` ou `hotfix/*`, ou um PR que mexa em empacotamento, quando a conferência roda, então cada instalador é gerado, instalado em silêncio, transcreve a amostra com o binário instalado (provando VLC e Whisper no lugar final) e é removido em silêncio, tudo antes do merge em `main`.
- [ ] CA17 — Dada a conferência no Windows, então ela também prova que a janela abre, que a atualização sobre uma versão anterior funciona, que a versão anterior é recusada por cima da nova e que um arquivo marcador na pasta de dados sobrevive à desinstalação silenciosa.

Documentação:
- [ ] CA18 — Dada a wiki, então ela explica, por sistema, como instalar, atualizar e remover, como passar pelo aviso do SmartScreen e do Gatekeeper, onde ficam os dados e como apagá-los no macOS e no Linux.
- [ ] CA19 — Dado o roteiro de teste manual de `tasks.md`, então ele foi executado numa máquina real de cada sistema disponível, e o que não pôde ser testado está anotado no PR.

## Impacto para o usuário

- **Dados salvos:** nada muda. Banco, backups, modelos e miniaturas continuam na pasta de dados, e quem vem do `.zip` encontra tudo igual. Só o desinstalador do Windows pode apagá-los, e só se o usuário confirmar.
- **Avisos de segurança:** o instalador do Windows mostra "O Windows protegeu o computador" (**Mais informações → Executar assim mesmo**). No macOS, o app é bloqueado na primeira abertura e precisa ser liberado em **Ajustes do Sistema → Privacidade e Segurança → Abrir Mesmo Assim**. Sem assinatura, os avisos voltam a cada versão.
- **Smart App Control:** no Windows 11 com esse recurso ligado, programas sem assinatura são bloqueados sem opção de continuar, tanto no `.exe` quanto no `.zip`. A única saída é desligar o recurso, e a wiki deixa isso claro.
- **Wiki:** sim, página de instalação reescrita (CA18). O `README.md` ganha a seção de download, que hoje diz que não há versões disponíveis.

## Decisões do mantenedor

Aprovadas com a spec, conforme as recomendações do `design.md`:

- **Q1 — Windows:** `.exe` com Inno Setup. Com o app gratuito, a licença comercial que o Inno pede não se aplica hoje.
- **Q2 — Para quem instalar:** só o usuário atual, sem administrador.
- **Q3 — macOS e Linux:** entram nesta spec, junto do Windows.
- **Q4 — Ícone:** publicar com o ícone padrão; o ícone próprio fica para a spec dele.
- **Q5 — Contato no `.deb`:** `Daniel Vieira Fernandes <DanielVieiraFernandes@users.noreply.github.com>`.
- **Assinatura:** continua fora, por enquanto. Windows e Linux instalam sem ela.

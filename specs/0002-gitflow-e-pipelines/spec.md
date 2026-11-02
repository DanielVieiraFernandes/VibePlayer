# 0002 — GitFlow e pipelines de release

**Status:** Implementada
**Branch:** `feature/0002-gitflow-e-pipelines`

## Problema

Hoje existe só `main` e uma branch por spec, com CI apenas em PRs para `main`. Não há um caminho definido para juntar várias features numa versão, testá-la antes de publicar, corrigir urgências em produção nem publicar binários. Tudo isso seria manual e sujeito a erro (esquecer de criar a tag, de levar uma correção de volta para o desenvolvimento, de gerar o pacote de um dos sistemas).

## Objetivo

O repositório segue o GitFlow, e as pipelines do GitHub Actions cuidam das partes repetitivas: criar branches de release e hotfix, versionar, publicar a release com os binários dos três sistemas e levar as mudanças de volta para `develop`. O mantenedor só decide **quando** iniciar uma release e **aprova** os merges.

## Escopo

**Inclui:**
- Modelo de branches GitFlow: `main`, `develop`, `feature/*`, `bugfix/*`, `release/*`, `hotfix/*`.
- Criar `develop` a partir de `main` e redirecionar o PR #1 para `develop`.
- Versão do app calculada a partir das tags git (SemVer, `vX.Y.Z`), sem editar número de versão à mão.
- CI rodando em todas as branches do fluxo.
- Pipeline **Iniciar release**: cria `release/X.Y.Z` a partir de `develop` e abre o PR para `main`.
- Pipeline **Iniciar hotfix**: cria `hotfix/X.Y.Z` a partir de `main` e abre o PR para `main`.
- Pipeline **Publicar release**: ao mergear uma release/hotfix em `main`, cria a tag, gera os binários (Windows, macOS Intel e Apple Silicon, Linux), publica a GitHub Release com notas automáticas e abre o PR de volta `main → develop`, mesclado automaticamente se não houver conflito.
- Regras de proteção em `main` e `develop`: só entra por PR com CI verde, sem force push e sem apagar a branch.
- Atualizar CLAUDE.md e `specs/README.md` com o novo fluxo.

**Não inclui** (specs futuras):
- Instaladores (MSI, DMG, AppImage), atualização automática do app.
- Assinatura de código no Windows e assinatura/notarização no macOS (os binários saem sem assinatura; o sistema mostra aviso na primeira abertura).
- Builds de pré-visualização (nightly) a partir de `develop`.
- Bloqueio de PRs de pessoas de fora.

## Critérios de aceite

- [ ] CA1 — `develop` existe, é a branch padrão do repositório, e o PR #1 aponta para ela.
- [ ] CA2 — `main` e `develop` rejeitam push direto e só aceitam PR com a CI aprovada nos três SOs.
- [ ] CA3 — A CI roda em push e PR para `develop`, `main`, `release/**` e `hotfix/**`.
- [ ] CA4 — Sem nenhuma tag, o app compila com versão de pré-lançamento (`0.0.0-alpha…`); numa tag `v0.1.0`, com versão `0.1.0`. A versão aparece na linha de inicialização do log.
- [ ] CA5 — Rodando **Iniciar release** com `0.1.0`, surgem a branch `release/0.1.0` (a partir de `develop`) e um PR `release/0.1.0 → main` com a CI rodando. Versão inválida ou já existente é rejeitada com mensagem clara.
- [ ] CA6 — Rodando **Iniciar hotfix** com `0.1.1`, surgem `hotfix/0.1.1` (a partir de `main`) e um PR rascunho para `main`.
- [ ] CA7 — Ao mergear o PR da release em `main`: a tag `v0.1.0` é criada; a GitHub Release `v0.1.0` é publicada com notas geradas dos PRs e quatro pacotes (`win-x64.zip`, `osx-arm64.tar.gz`, `osx-x64.tar.gz`, `linux-x64.tar.gz`), cada um com `LICENSE` e `THIRD-PARTY-NOTICES.md`.
- [ ] CA8 — Após a publicação, um PR `main → develop` é aberto e mesclado automaticamente quando a CI passa; se houver conflito, ele fica aberto para resolução manual.
- [ ] CA9 — O pacote `win-x64` baixado da release abre o app e grava o log com a versão `0.1.0`.

## Impacto para o usuário

Primeira versão para download (`v0.1.0`) passa a ser possível, ainda só com a janela vazia. A wiki ganha uma página de instalação quando houver uma release útil, não nesta spec.

## Questões em aberto

1. ~~**Token para as pipelines.**~~ Resolvido: o mantenedor cria um *fine-grained personal access token* restrito a este repositório (Contents, Pull requests e Workflows como leitura e escrita) e o salva como secret `RELEASE_TOKEN`. Necessário só para a validação de ponta a ponta (T9); o restante pode ser implementado antes.
2. ~~**Merge da release em `main`.**~~ Resolvido: o mantenedor clica em "Merge" depois da CI passar; a pipeline nunca mescla em `main` sozinha.
3. ~~**Tipos de merge.**~~ Resolvido: *squash* em `feature/*` e `bugfix/*` → `develop`; *merge commit* em `release/*` e `hotfix/*` → `main` e no retorno `main → develop`.
4. ~~**Binários sem assinatura.**~~ Resolvido: publicar sem assinatura por enquanto; assinatura e notarização entram numa spec própria antes de divulgar o app ao público.

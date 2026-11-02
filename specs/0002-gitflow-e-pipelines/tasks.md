# 0002 — Tarefas

## Implementação

- [x] T1 — Criar `develop` a partir de `main`, definir como branch padrão e redirecionar o PR #1 para `develop`.
- [x] T2 — Configurar o repositório: auto-merge, apagar branch após merge, squash e merge commit permitidos (rebase desativado), permitir que o Actions crie PRs.
- [x] T3 — Adicionar MinVer (`Directory.Packages.props` e `Directory.Build.props`, prefixo de tag `v`) e remover o `<Version>` fixo.
- [x] T4 — `ci.yml`: novos gatilhos e `fetch-depth: 0`.
- [x] T5 — `start-release.yml` e `start-hotfix.yml`, com validação de versão e verificação do `RELEASE_TOKEN`.
- [x] T6 — `release.yml`: tag, publish por RID nos runners de cada SO, pacotes com licenças, GitHub Release com notas, PR `main → develop` com auto-merge.
- [x] T7 — Rulesets de `main` e `develop` (depois que os nomes dos checks da CI existirem).
- [x] T8 — Atualizar CLAUDE.md (seção Git e GitHub) e `specs/README.md` (nome das branches por spec).
- [x] T9 — Validar o fluxo de ponta a ponta publicando a `v0.1.0`.

## Testes

| Critério | Tipo | Verificação |
|---|---|---|
| CA1 | Manual | `develop` é a branch padrão (conferido); PR #1 já estava mesclado em `main` (ver desvios) |
| CA2 | Manual | Push direto em `develop` é recusado (conferido: GH013, ruleset ativo); PR sem CI verde não pode ser mesclado |
| CA3 | CI | Conferido: CI em PR para `develop` (#2, #4), PR para `main` (#3) e push em `release/0.1.0` |
| CA4 | Unitário | `AppVersionTests.VersaoDoAssembly_VemDoMinVer_SegueSemVer`; build local conferido: sem tag → `0.0.0-alpha.0.1`, tag temporária `v9.9.9` → `9.9.9` |
| CA5 | Manual | Conferido: `0.1.0` criou `release/0.1.0` e o PR #3 com CI; `abc` e `0.1.0` repetido falharam com mensagem clara |
| CA6 | Manual | **Não executado:** validar no primeiro hotfix real (ver desvios) |
| CA7 | Manual | Conferido: tag `v0.1.0` no merge commit, release publicada com os quatro pacotes e notas geradas dos PRs |
| CA8 | Manual | **Falhou na v0.1.0** (ver desvios): PR #4 criado, mas o auto-merge foi recusado; concluído manualmente após a CI. Correção a validar na próxima release |
| CA9 | Manual | Conferido: pacote baixado da release abre a janela e registra `Iniciando VibePlayer 0.1.0+e3827e3…` |

## Documentação

- [x] CLAUDE.md e `specs/README.md` (T8)
- [x] `DESIGN.md` — não se aplica
- [x] Wiki — não se aplica (página de instalação quando houver release útil)

## Desvios em relação ao design

- **PR #1 não foi redirecionado:** o mantenedor já o havia mesclado em `main` antes da implementação. `develop` foi criada a partir desse `main`, então já contém a spec 0001.
- **"Permitir que o Actions crie PRs" não foi ligado:** todos os PRs das pipelines usam o `RELEASE_TOKEN`, então a permissão (que também deixaria o Actions aprovar PRs) não é necessária.
- **Retorno `main → develop` por branch temporária** `merge/vX.Y.Z-para-develop`, e não com `main` como origem do PR: com "apagar branch após o merge" ligado, usar `main` como origem arriscaria o GitHub tentar apagá-la.
- **Validação de versão em script** (`.github/scripts/validate-version.sh`), compartilhado pelas duas pipelines de início; também recusa versões que não sejam maiores que a última publicada.
- **Tag e GitHub Release usam o `GITHUB_TOKEN`**; o `RELEASE_TOKEN` fica restrito ao que precisa disparar a CI (branches e PRs).
- **Retorno para `develop` sem auto-merge (correção após a v0.1.0):** o `RELEASE_TOKEN` é do mantenedor, que como administrador pode passar por cima das regras em PRs; para ele o GitHub considera o PR "já mesclável" e recusa ligar o auto-merge (`Pull request is in clean status`). A pipeline agora espera os checks exigidos (`gh pr checks --required --watch`) e então mescla com merge commit. Lógica de espera testada localmente contra o PR #4; o fluxo completo será confirmado na próxima release.
- **CA6 (Iniciar hotfix) não foi executado:** exigiria criar e descartar uma branch e um PR de hotfix sem necessidade real. Reaproveita o mesmo script de validação e os mesmos passos já validados no *Iniciar release*; será confirmado no primeiro hotfix.

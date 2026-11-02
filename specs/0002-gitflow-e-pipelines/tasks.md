# 0002 — Tarefas

## Implementação

- [x] T1 — Criar `develop` a partir de `main`, definir como branch padrão e redirecionar o PR #1 para `develop`.
- [x] T2 — Configurar o repositório: auto-merge, apagar branch após merge, squash e merge commit permitidos (rebase desativado), permitir que o Actions crie PRs.
- [x] T3 — Adicionar MinVer (`Directory.Packages.props` e `Directory.Build.props`, prefixo de tag `v`) e remover o `<Version>` fixo.
- [x] T4 — `ci.yml`: novos gatilhos e `fetch-depth: 0`.
- [x] T5 — `start-release.yml` e `start-hotfix.yml`, com validação de versão e verificação do `RELEASE_TOKEN`.
- [x] T6 — `release.yml`: tag, publish por RID nos runners de cada SO, pacotes com licenças, GitHub Release com notas, PR `main → develop` com auto-merge.
- [ ] T7 — Rulesets de `main` e `develop` (depois que os nomes dos checks da CI existirem).
- [x] T8 — Atualizar CLAUDE.md (seção Git e GitHub) e `specs/README.md` (nome das branches por spec).
- [ ] T9 — Validar o fluxo de ponta a ponta publicando a `v0.1.0`.

## Testes

| Critério | Tipo | Verificação |
|---|---|---|
| CA1 | Manual | `gh repo view` mostra `develop` como padrão; PR #1 com base `develop` |
| CA2 | Manual | Push direto em `develop` é recusado; PR sem CI verde não pode ser mesclado |
| CA3 | CI | Execuções da CI aparecem para PR em `develop` e na branch `release/0.1.0` |
| CA4 | Unitário | `AppVersionTests.VersaoDoAssembly_VemDoMinVer_SegueSemVer`; build local conferido: sem tag → `0.0.0-alpha.0.1`, tag temporária `v9.9.9` → `9.9.9` |
| CA5 | Manual | Executar *Iniciar release* com `0.1.0`, e com `abc` e `0.1.0` repetido (devem falhar) |
| CA6 | Manual | Executar *Iniciar hotfix* com `0.1.1`, conferir branch e PR rascunho, depois descartá-los |
| CA7 | Manual | Mergear `release/0.1.0`; conferir tag, release e os quatro pacotes |
| CA8 | Manual | Conferir PR `main → develop` mesclado automaticamente |
| CA9 | Manual | Baixar `win-x64.zip`, abrir o app, conferir versão no log |

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

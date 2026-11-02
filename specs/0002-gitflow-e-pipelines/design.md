# 0002 — Design técnico

## Fluxo de branches

```
feature/NNNN-nome ──squash──▶ develop ──(Iniciar release)──▶ release/X.Y.Z ──merge──▶ main ──tag vX.Y.Z──▶ GitHub Release
bugfix/NNNN-nome  ──squash──▶ develop                              │                       │
                                 ▲                                 └─ correções de release  │
                                 └────────────── PR automático main → develop ◀────────────┘
main ──(Iniciar hotfix)──▶ hotfix/X.Y.Z ──merge──▶ main (mesmo caminho de publicação)
```

| Branch | Sai de | Volta para | Quem cria |
|---|---|---|---|
| `feature/NNNN-nome` | `develop` | `develop` (PR, squash) | Claude, uma por spec |
| `bugfix/NNNN-nome` | `develop` | `develop` (PR, squash) | Claude, uma por spec de correção |
| `release/X.Y.Z` | `develop` | `main` (PR, merge commit) | Pipeline *Iniciar release* |
| `hotfix/X.Y.Z` | `main` | `main` (PR, merge commit) | Pipeline *Iniciar hotfix* |

Correções encontradas durante a release entram por PR na própria `release/X.Y.Z` e voltam para `develop` pelo PR automático depois da publicação.

## Versionamento

**MinVer** calcula a versão a partir da tag git mais próxima no histórico, durante o build:

- commit com tag `v0.1.0` → `0.1.0`;
- commits depois dela → `0.1.1-alpha.0.N` (N = commits desde a tag);
- sem tag nenhuma → `0.0.0-alpha.0.N`.

Remove o `<Version>` fixo do `Directory.Build.props`. A tag só é criada pela pipeline de publicação, então o número nunca é editado à mão e não existe commit de "bump de versão".

Exige histórico completo no checkout da CI (`fetch-depth: 0`); sem isso o MinVer não enxerga as tags.

## Workflows

| Arquivo | Gatilho | O que faz |
|---|---|---|
| `ci.yml` | push e PR em `develop`, `main`, `release/**`, `hotfix/**` | Build + testes unitários + E2E na matriz Windows/macOS/Linux (já existe; ganha os gatilhos e `fetch-depth: 0`) |
| `start-release.yml` | manual (`workflow_dispatch`, entrada `version`) | Valida SemVer e se a tag/branch já existe; cria `release/X.Y.Z` de `develop`; abre PR para `main` |
| `start-hotfix.yml` | manual (entrada `version`) | Idem, a partir de `main`, PR em rascunho até o conserto ser enviado |
| `release.yml` | PR fechado com merge em `main` vindo de `release/*` ou `hotfix/*` | Cria tag `vX.Y.Z` → publica binários (matriz) → cria GitHub Release → abre PR `main → develop` com auto-merge |

`release.yml` faz tudo num workflow só porque uma tag criada pelo próprio Actions não dispara outro workflow.

### Pacotes da release

Publicados em runners do próprio SO de destino (Windows → `win-x64`; macOS → `osx-arm64` e `osx-x64`; Ubuntu → `linux-x64`), já prevendo as bibliotecas nativas que virão (LibVLC, Whisper), que precisam ser empacotadas por plataforma.

```
dotnet publish src/VibePlayer.App -c Release -r <rid> --self-contained true
```

Self-contained: o usuário não precisa instalar o .NET. Cada pacote leva `LICENSE` e `THIRD-PARTY-NOTICES.md`. Notas da release geradas pelo GitHub a partir dos títulos dos PRs (que já seguem Conventional Commits).

## Proteção de branches (rulesets do GitHub)

| Regra | `main` | `develop` |
|---|---|---|
| Só via PR | sim | sim |
| CI obrigatória nos 3 SOs | sim | sim |
| Bloquear force push e exclusão | sim | sim |
| Bypass | mantenedor (para emergências) | mantenedor |

Configurações do repositório: `develop` como branch padrão; permitir auto-merge; apagar a branch de origem após o merge; tipos de merge permitidos: squash e merge commit (rebase desativado).

## Decisões

| Decisão | Motivo | Alternativas descartadas |
|---|---|---|
| MinVer para a versão | Uma linha no build, a tag é a única fonte da verdade, licença Apache 2.0 | GitVersion (configuração extensa); `<Version>` manual (fácil esquecer, gera commits de bump) |
| Branch de release criada por pipeline | Valida a versão e garante a origem certa (`develop`) | Criar a branch à mão |
| Token `RELEASE_TOKEN` nas pipelines | PRs e pushes feitos com o token padrão não disparam a CI, travando PRs com CI obrigatória | `GITHUB_TOKEN` padrão |
| Publicar em runner de cada SO | Prepara para as bibliotecas nativas por plataforma | Publicar todos os RIDs a partir do Linux (funciona hoje, quebra quando entrar LibVLC/Whisper) |
| Pacotes `.zip`/`.tar.gz` | Simples e suficiente até a spec de instaladores | Instaladores agora (fora de escopo) |

## Mudanças em dados

Nenhuma.

## Dependências novas

| Pacote / ação | Uso | Licença |
|---|---|---|
| MinVer (NuGet, só em build) | Versão a partir das tags | Apache 2.0 |
| `actions/checkout`, `actions/setup-dotnet`, `actions/upload-artifact`, `actions/download-artifact` | Pipelines | MIT |

Criação de tags, PRs e releases usa a CLI `gh`, já presente nos runners.

## Logs e erros

- A linha de inicialização do log já registra a versão; com o MinVer ela passa a ser a versão real da release.
- Pipelines falham com mensagem explícita para versão inválida, tag ou branch já existente, e ausência do `RELEASE_TOKEN`.

## Riscos

- **Token expira:** fine-grained PATs têm validade; quando vencer, as pipelines de release falham com erro de autenticação. Mitigação: validade longa e um passo que verifica o token no início dos workflows com mensagem clara.
- **PR #1 já aberto como `feat/0001-…`:** fica com o nome antigo; o padrão `feature/` vale a partir da 0002.
- **Conflito no retorno `main → develop`:** acontece se `develop` mudou o mesmo trecho corrigido no hotfix. O PR fica aberto e é resolvido manualmente.
- **Teste real do fluxo** exige publicar uma release de verdade (`v0.1.0`). Proposta: usar a própria `v0.1.0` (app com janela vazia) como teste do CA5, CA7, CA8 e CA9.

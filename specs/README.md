# Specs (Spec-Driven Development)

Cada mudança no projeto — funcionalidade, correção relevante, refatoração estrutural — nasce como uma spec nesta pasta, é aprovada pelo mantenedor e só então é implementada.

## Estrutura

```
specs/
  _template/           Modelos para copiar
  0001-nome-curto/
    spec.md            O QUÊ e POR QUÊ: problema, escopo, critérios de aceite
    design.md          COMO: decisões técnicas, alternativas descartadas, impacto
    tasks.md           Passo a passo da implementação e testes que comprovam a spec
```

Numeração sequencial com 4 dígitos; nome curto em kebab-case (`0003-transcricao-whisper`). A spec vive na branch da própria mudança (`feature/0003-transcricao-whisper` ou `bugfix/...`, a partir de `develop`) e entra em `develop` junto com o código, pelo mesmo PR.

## Ciclo de vida

| Status | Significado |
|---|---|
| Rascunho | Escrita, aguardando revisão. Nenhum código é escrito. |
| Aprovada | Mantenedor aprovou. Implementação pode começar. |
| Implementada | Código, testes e documentação concluídos e mergeados. |
| Descartada | Não será feita; mantida para registro do motivo. |

Mudou algo do que foi aprovado durante a implementação? A spec volta para **Rascunho** com a alteração destacada e passa por nova aprovação.

## Mudanças pequenas

Correções triviais (typo, ajuste de texto, bump de dependência sem mudança de API) podem usar só um `spec.md` curto, mas ainda passam por aprovação antes do código.

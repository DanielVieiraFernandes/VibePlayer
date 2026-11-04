# 0007 — Transcrição das aulas

**Status:** Implementada
**Branch:** `feature/0007-transcricao`

## Problema

O chat, o resumo e os pontos principais precisam do texto da aula, com timestamps. Hoje o app só toca o vídeo: não há texto nenhum para enviar ao LLM.

Boa parte dos cursos comprados não traz legenda, e transcrever precisa rodar na máquina do usuário, sem depender de serviço externo. A transcrição é demorada e usa muito CPU e memória, então não pode travar o player, nem derrubar o app se a biblioteca nativa falhar.

## Objetivo

Para qualquer aula com áudio, o app obtém o texto com timestamps, de duas formas:
- pela legenda `.srt` ou `.vtt` ao lado do vídeo, quando existir;
- pelo Whisper local, em segundo plano, com progresso e cancelamento.

O resultado fica salvo. Não é preciso transcrever de novo se a pasta for movida ou se o mesmo vídeo aparecer em outro curso. O app continua aberto mesmo se a transcrição falhar de forma irrecuperável.

## Escopo

**Inclui:**
- Transcrição local com `Whisper.net`, em segundo plano, uma aula por vez, com fila, progresso e cancelamento.
- Áudio extraído pelo próprio VLC que o app já distribui, sem ffmpeg.
- Whisper e extração em **processo separado**, para uma falha nativa não derrubar o app.
- Download do modelo na primeira transcrição, com confirmação, progresso, retomada e verificação de integridade.
- Cache pelo hash SHA-256 do arquivo de vídeo, com os dados ligados a `LessonEntity.Id`.
- Legendas `.srt` e `.vtt` ao lado do vídeo, que substituem o Whisper.
- Nova aba **Transcrição** no painel lateral: estado, progresso, texto com tempos e clique no tempo para levar o vídeo àquele ponto.
- "Transcrever" no menu da aula e "Transcrever curso" no menu do curso.
- Aceleração por GPU quando houver (Vulkan no Windows e no Linux, Metal no macOS Apple Silicon), com volta automática para a CPU.

**Não inclui** (fica para outra spec):
- Chat, resumo, pontos principais e qualquer envio do texto a um LLM.
- Exibir legendas sobre o vídeo.
- Busca dentro da transcrição, edição, exportação, tradução e identificação de quem fala.
- Escolher a faixa de áudio: vale a primeira faixa.
- Tela para gerenciar modelos baixados, limpeza de transcrições sem aula e runtimes CUDA, CoreML ou OpenVINO.
- Retomar uma transcrição interrompida do ponto em que parou: ela recomeça do início.

## Critérios de aceite

Extração de áudio:

- [x] CA1 — Dado um vídeo MP4 (AAC), MKV (AAC), AVI (MP3) ou WebM (Opus) de `tests/fixtures`, quando o áudio é extraído, então sai PCM de 16 kHz, mono, com duração igual à do vídeo (tolerância de 100 ms), nos três SOs.
- [x] CA2 — Dado um arquivo com um trecho de 30 s sem pacotes de áudio, quando o áudio é extraído, então o trecho vira silêncio e o que vem depois mantém o tempo original (tolerância de 100 ms).
- [x] CA3 — Dado um vídeo sem faixa de áudio, quando a transcrição é pedida, então a aula fica como "Sem áudio", sem erro e sem nova tentativa.
- [x] CA4 — Dado um arquivo corrompido, quando a transcrição é pedida, então a aula fica como "Falhou", com opção de tentar de novo, e o app segue funcionando.
- [x] CA5 — Os pacotes publicados só têm os plugins do VLC da lista de permissão, agora com `libstream_out_transcode_plugin` e `libstream_out_smem_plugin` (`check-vlc-plugins.sh`).

Transcrição:

- [x] CA6 — Dado o fixture de fala, quando transcrito pelo Whisper real, então o texto contém as palavras esperadas e os segmentos têm início e fim crescentes (`WhisperIntegrationTests`, nos três SOs).
- [x] CA7 — Dada uma transcrição em andamento, quando a tela é observada, então mostra etapa e porcentagem, e os segmentos só ficam salvos ao final, de forma atômica.
- [x] CA8 — Dada uma transcrição em andamento, quando o usuário cancela, então o processo termina em até 2 s, a memória é liberada e a aula volta a "Não transcrita".
- [x] CA9 — Dada uma fila com várias aulas, quando o usuário pede a aula aberta, então ela vai para a frente da fila; só uma aula é transcrita por vez.
- [x] CA10 — Dada uma fila com aulas pendentes, quando o app é fechado e reaberto, então as aulas pendentes continuam na fila, e a que estava em andamento recomeça.
- [x] CA11 — Dado um processo de transcrição que encerra de forma anormal, quando isso acontece, então o app segue aberto, a aula fica como "Falhou", o log registra só o código de saída e a fila continua na aula seguinte.
- [x] CA12 — Dada uma falha do processo usando GPU, quando ela acontece, então a aula é tentada de novo uma vez só na CPU, e o app passa a usar a CPU dali em diante.
- [x] CA13 — Dado um trecho longo de silêncio, quando transcrito, então marcações que o Whisper inventa, como `[MÚSICA]`, não aparecem no resultado.

Modelo:

- [x] CA14 — Dado que o modelo não existe, quando a primeira transcrição é pedida, então o app pede confirmação mostrando o tamanho, baixa com progresso e cancelamento, e só então transcreve.
- [x] CA15 — Dado um download interrompido, quando pedido de novo, então continua de onde parou.
- [x] CA16 — Dado um arquivo baixado com SHA-256 diferente do esperado, quando o download termina, então o arquivo é descartado, o usuário é avisado e nada é transcrito com ele.

Cache e identidade:

- [x] CA17 — Dada uma aula transcrita, quando a pasta é movida e reconectada, então a transcrição continua disponível sem rodar o Whisper.
- [x] CA18 — Dado o mesmo vídeo em dois cursos, quando o segundo é pedido, então usa o resultado salvo, na hora.
- [x] CA19 — Dado um vídeo alterado (tamanho ou data diferentes, conteúdo novo), quando a aula é aberta, então a transcrição antiga deixa de valer para ela e a aula volta a "Não transcrita".

Legendas:

- [x] CA20 — Dado `aula.mp4` com `aula.srt`, `aula.vtt`, `aula.pt.srt` ou `aula.pt-BR.vtt` ao lado, quando a aula é aberta, então a legenda vira a transcrição, sem fila e sem Whisper, e a tela indica a origem.
- [x] CA21 — Dada uma legenda em UTF-8, UTF-8 com BOM, UTF-16 ou Windows-1252, quando lida, então os acentos ficam corretos.
- [x] CA22 — Dada uma legenda com tags (`<i>`, `{\an8}`), configurações de cue, `NOTE` e `STYLE`, quando lida, então sobra só o texto com os tempos certos.
- [x] CA23 — Dada uma legenda malformada, quando lida, então os blocos válidos são aproveitados; sem nenhum válido, a aula continua disponível para o Whisper e o log registra o aviso.

Interface:

- [x] CA24 — Dada uma transcrição pronta, quando o usuário clica num tempo da aba, então o vídeo vai para aquele ponto.
- [x] CA25 — Dado o Linux sem VLC, quando a aula tem legenda, então a legenda funciona; sem legenda, a aba explica que o VLC é necessário.
- [x] CA26 — Todos os estados da aba (sem aula, não transcrita, baixando modelo, na fila, em andamento, concluída, legenda, sem áudio, falhou, VLC ausente) seguem o `DESIGN.md` e passam em `AccessibilityTests` e `ViewConventionsTests`.

Segurança dos dados:

- [x] CA27 — Nenhum log contém texto de transcrição ou de legenda, e nenhum dado do usuário sai da máquina: a única conexão é o download do modelo.

## Impacto para o usuário

- **Aba nova** no painel lateral, e novas ações nos menus de aula e de curso.
- **Download:** cerca de 190 MB no primeiro uso (modelo padrão), depois de confirmar.
- **Tempo, medido em um Ryzen 7 5700G com aulas reais:** uma aula de 1 hora leva cerca de 6 min pela GPU integrada ou 9 min pela CPU. Máquinas mais fracas levam mais.
- **Memória durante a transcrição:** cerca de 750 MB, mais 350 MB por hora de aula, liberados ao terminar.
- **Dados salvos:** novas tabelas no banco, cobertas pelos backups.
- **Pacote:** 18 MB a mais no download do Windows, medido, e valor parecido no Linux; cerca de 3 MB no macOS (ver Q4).
- **Wiki:** nova página sobre transcrição: como usar, tempos esperados, legendas aceitas, e o VLC necessário no Linux.

## Decisões do mantenedor

Todas as recomendações foram aprovadas em 2026-11-03. O detalhe e os números estão em `design.md`.

| Questão | Decisão |
|---|---|
| Q1 — Extração de áudio | VLC com `transcode` + `smem`, sem ffmpeg. Aceito o limite do áudio que começa depois do vídeo |
| Q2 — Processo separado | Mesmo executável, com `--transcription-worker` |
| Q3 — Modelo padrão | `small` quantizado (190 MB), com `large-v3-turbo` quantizado (574 MB) como "Mais preciso". Confirmado com aulas reais (T1, ver `design.md`) |
| Q4 — Vulkan no pacote | Sim, em `win-x64` e `linux-x64` |
| Q5 — Interface | Quinta aba, "Transcrição", depois de "Aulas" |
| Q6 — Quando transcrever | Só a pedido do usuário |
| Q7 — Legenda ao lado do vídeo | Vence automaticamente, com "Transcrever com o Whisper" para quem preferir |
| Q8 — Legenda sobre o vídeo | Fica para uma spec própria |

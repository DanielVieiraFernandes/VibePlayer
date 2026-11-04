# Backlog

Ideias e specs futuras ainda sem spec escrita. Cada item vira uma spec (`specs/NNNN-…`) quando for priorizado; até lá, nada aqui está aprovado.

## Atualização automática do app

Ideia do mantenedor (registrada durante a spec 0005):
- **Verificação:** de tempos em tempos, com a rede disponível, o app procura uma versão nova (as releases ficam no GitHub Releases).
- **Download:** encontrou uma versão nova → baixa em segundo plano.
- **Aplicação:** a atualização é aplicada quando o app é reiniciado, com um botão para o usuário reiniciar na hora ("Reiniciar para atualizar").

Pontos para a spec decidir:
- **Mecanismo de atualização:**
  - biblioteca pronta (ex.: Velopack, MIT, que também gera instaladores);
  - ou código próprio sobre a API de releases do GitHub.
- **Integridade:** como verificar o download (checksum publicado na release).
- **Assinatura:**
  - sem binários assinados, o SmartScreen (Windows) e o Gatekeeper (macOS) voltam a avisar a cada versão;
  - no macOS, trocar o app exige notarização para não ser bloqueado.
- **Linux:** formato de pacote (tar.gz, AppImage, Flatpak), que muda como atualizar.
- **Canal:** só versões estáveis ou também pré-lançamentos.
- **Privacidade:** a verificação só consulta a página de releases; nenhum dado do usuário é enviado.
- **Desligar:** opção para desativar a verificação nas configurações.

## Specs previstas

- Player (LibVLC): reprodução, progresso, "continuar de onde parei", duração e miniaturas.
- Exibir legendas sobre o vídeo, a partir da transcrição (decisão da spec 0007).
- Provedores de LLM (Claude e Ollama), chaves de API no cofre do sistema (`ISecretStore`), aviso de privacidade sobre o envio da transcrição.
- Chat, resumo e pontos principais com timestamps clicáveis.
- Configurações:
  - tema manual;
  - restaurar backup pela interface e escolher a pasta de backup;
  - quantidade de backups.
- Instaladores, assinatura e notarização dos binários.
- Ícone e logo do app.
- Bloquear PRs externos no repositório.

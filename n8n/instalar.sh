#!/bin/sh
# Importa os fluxos de n8n/fluxos no n8n do compose e publica os que rodam sozinhos.
#
# Roda na VPS, na pasta do AlephBot, depois de criar a conta de dono no n8n
# (http://localhost:5678, pelo túnel). Pode rodar de novo depois de um git pull: o fluxo
# com o mesmo id é trocado pelo do repo — e o que você mudou nele pelo editor se perde.
set -eu

docker compose exec -T n8n n8n import:workflow --separate --input=/fluxos

# o de erros fica de fora: o n8n chama ele sozinho quando um dos outros falha
for id in AlephSaude000001 AlephStats000001 AlephComandos001 AlephPainel00001 AlephBackup00001; do
  docker compose exec -T n8n n8n publish:workflow --id="$id"
done

# o que a linha de comando publica só começa a rodar quando o n8n sobe de novo
docker compose restart n8n

echo "Pronto: os fluxos estão publicados. O painel fica em http://localhost:5678/webhook/aleph-painel"

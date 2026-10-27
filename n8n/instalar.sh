#!/bin/sh
# Importa os fluxos de n8n/fluxos no n8n do compose e publica os que rodam sozinhos.
#
# Roda na VPS, na pasta do AlephBot, depois de criar a conta de dono no n8n
# (http://localhost:5678, pelo túnel). Pode rodar de novo depois de um git pull: o fluxo
# com o mesmo id é trocado pelo do repo — e o que você mudou nele pelo editor se perde.
set -eu

# o de erros fica de fora: o n8n chama ele sozinho quando um dos outros falha.
# O de stats saiu de vez: quem anota os números agora é o próprio bot, de dentro, sem
# passar pela API — e segue anotando mesmo com o n8n parado, que foi o que faltou quando
# os fluxos caíram e a linha do tempo parou sem ninguém ver
SOZINHOS="AlephSaude000001 AlephComandos001 AlephPainel00001 AlephBackup00001"
ESPERADOS=4

# A credencial do Postgres que o backup e o painel usam (id AlephPostgres001). Os fluxos só
# guardam o id dela; sem isso o "Lê o banco" falha com "Credential ... does not exist". A senha
# vem do POSTGRES_PASSWORD do .env da raiz, o mesmo que o compose entrega ao Postgres, e passa
# pro contêiner por variável — não fica em arquivo nenhum na VPS
senha=$(grep '^POSTGRES_PASSWORD=' .env 2>/dev/null | tail -1 | cut -d= -f2- | sed 's/^["'\'']//; s/["'\'']$//')
senha=${senha:-${POSTGRES_PASSWORD:-}}

if [ -z "$senha" ]; then
  echo "FALHOU: sem POSTGRES_PASSWORD no .env da raiz, não dá pra criar a credencial do banco."
  exit 1
fi

docker compose exec -T -e PG_SENHA="$senha" n8n node -e '
  const credencial = [{
    id: "AlephPostgres001",
    name: "Aleph Postgres",
    type: "postgres",
    data: { host: "postgres", port: 5432, database: "aleph", user: "aleph", password: process.env.PG_SENHA, ssl: "disable" },
  }];
  require("fs").writeFileSync("/tmp/credencial.json", JSON.stringify(credencial));
'
docker compose exec -T n8n sh -c 'n8n import:credentials --input=/tmp/credencial.json; status=$?; rm -f /tmp/credencial.json; exit $status'

docker compose exec -T n8n n8n import:workflow --separate --input=/fluxos

for id in $SOZINHOS; do
  docker compose exec -T n8n n8n publish:workflow --id="$id"
done

# o que a linha de comando publica só começa a rodar quando o n8n sobe de novo
docker compose restart n8n

# Daqui pra baixo é só conferência. Publicar e não valer calou a automação inteira uma vez
# (2026-10-11): os fluxos apareciam "Published" no editor, rodavam no botão, e nada rodava
# sozinho — nenhum aviso, nenhum backup. O n8n não reclama; quem conta a verdade é esta
# linha do log dele.
printf 'Esperando o n8n voltar'

i=0
while [ $i -lt 40 ]; do
  if docker compose logs --since 2m n8n 2>/dev/null | grep -q 'published workflows'; then
    break
  fi

  printf '.'
  sleep 2
  i=$((i + 1))
done

echo

publicados=$(docker compose logs --since 2m n8n 2>/dev/null \
  | grep -o '[0-9]\{1,\} published workflows' | tail -1 | cut -d' ' -f1)
publicados=${publicados:-0}

if [ "$publicados" -lt "$ESPERADOS" ]; then
  echo
  echo "FALHOU: o n8n subiu com $publicados fluxo(s) publicado(s), e eram pra ser $ESPERADOS."
  echo "Sem isso nada roda sozinho: nem os avisos no Discord, nem o backup das 3h."
  echo "Publique pelo editor (botão Published em cada fluxo) e confira com:"
  echo "  docker compose logs --tail 20 n8n | grep -iE 'published workflows|Deregistered'"
  exit 1
fi

echo "Pronto: $publicados fluxos publicados e rodando sozinhos."
echo "O painel fica em http://localhost:5678/webhook/aleph-painel"

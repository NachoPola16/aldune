#!/usr/bin/env bash
set -Eeuo pipefail

repo_dir="${ALDUNE_REPO_DIR:-$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)}"
env_file="${ALDUNE_ENV_FILE:-$repo_dir/.env}"
compose_file="${ALDUNE_COMPOSE_FILE:-docker-compose.sync.nginx.yml}"
branch="${ALDUNE_BRANCH:-main}"
health_url="${ALDUNE_HEALTH_URL:-http://127.0.0.1:8097/health}"

if [[ ! -d "$repo_dir/.git" ]]; then
    echo "No se ha encontrado un checkout Git en: $repo_dir" >&2
    exit 1
fi

if [[ ! -f "$env_file" ]]; then
    echo "Falta el fichero de secretos: $env_file" >&2
    echo "Crea ALDUNE_SYNC_TOKEN (o ALDUNE_SYNC_TOKENS) antes de actualizar." >&2
    exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
    echo "No se ha encontrado Docker CLI." >&2
    exit 1
fi

cd "$repo_dir"
git pull --ff-only origin "$branch"

compose_args=(
    --env-file "$env_file" \
    -f "$repo_dir/$compose_file"
)

docker compose "${compose_args[@]}" config --quiet
docker compose "${compose_args[@]}" build --pull
docker compose "${compose_args[@]}" up -d --remove-orphans

if command -v curl >/dev/null 2>&1; then
    curl --fail --silent --show-error --max-time 15 "$health_url" >/dev/null
    echo "Servidor actualizado y saludable: $health_url"
else
    echo "Servidor actualizado. Instala curl para comprobar /health automáticamente." >&2
fi
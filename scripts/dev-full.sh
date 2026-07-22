#!/usr/bin/env bash
set -Eeuo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"
state_dir="$repo_root/.mastemis-dev"
env_file="$state_dir/environment"
log_dir="$state_dir/logs"
server_log="$log_dir/server.log"
cookie_jar="$state_dir/login-test.cookies"
server_pid_file="$state_dir/server.pid"
compose_file="$repo_root/deploy/compose/compose.yaml"

server_http_port="${MASTEMIS_DEV_HTTP_PORT:-5080}"
server_https_port="${MASTEMIS_DEV_HTTPS_PORT:-5443}"
server_url="https://localhost:${server_https_port}"
no_ui=false
reset=false

usage() {
  printf '%s\n' \
    'Usage: ./scripts/dev-full.sh [--no-ui] [--reset]' \
    '' \
    '  --no-ui  Start PostgreSQL and the server without opening Uno Desktop.' \
    '  --reset  Move the existing local database aside and create a clean one.'
}

for argument in "$@"; do
  case "$argument" in
    --no-ui) no_ui=true ;;
    --reset) reset=true ;;
    -h|--help) usage; exit 0 ;;
    *) printf 'Unknown argument: %s\n' "$argument" >&2; usage >&2; exit 2 ;;
  esac
done

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    printf 'Missing required command: %s\n' "$1" >&2
    return 1
  fi
}

require_command dotnet
require_command curl
require_command openssl

if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
  compose=(docker compose)
elif command -v podman >/dev/null 2>&1 && podman compose version >/dev/null 2>&1; then
  compose=(podman compose)
else
  printf '%s\n' \
    'Docker Compose or Podman Compose is required.' \
    'Install one of them, ensure the daemon is running, then execute this script again.' >&2
  exit 3
fi

mkdir -p "$state_dir" "$log_dir"
chmod 700 "$state_dir" "$log_dir"

random_secret() {
  openssl rand -hex 18
}

if [[ ! -f "$env_file" ]]; then
  umask 077
  postgres_password="$(random_secret)"
  administrator_password="Mst-$(random_secret)-A1!"
  certificate_password="$(random_secret)"
  {
    printf 'POSTGRES_PASSWORD=%q\n' "$postgres_password"
    printf 'ADMINISTRATOR_USERNAME=%q\n' 'admin'
    printf 'ADMINISTRATOR_PASSWORD=%q\n' "$administrator_password"
    printf 'CERTIFICATE_PASSWORD=%q\n' "$certificate_password"
  } > "$env_file"
fi

# The file is generated locally with mode 600 and is excluded by .gitignore.
# shellcheck disable=SC1090
source "$env_file"
: "${POSTGRES_PASSWORD:?Missing generated PostgreSQL password}"
: "${ADMINISTRATOR_USERNAME:?Missing generated administrator username}"
: "${ADMINISTRATOR_PASSWORD:?Missing generated administrator password}"
: "${CERTIFICATE_PASSWORD:?Missing generated certificate password}"

if $reset && [[ -d "$repo_root/deploy/compose/data" ]]; then
  backup_dir="$state_dir/database-backup-$(date -u +%Y%m%dT%H%M%SZ)"
  "${compose[@]}" -f "$compose_file" down >/dev/null 2>&1 || true
  mv -- "$repo_root/deploy/compose/data" "$backup_dir"
  printf 'Existing database moved to %s\n' "$backup_dir"
fi

certificate_path="$state_dir/https.pfx"
certificate_pem="$state_dir/https.pem"
if [[ ! -f "$certificate_path" || ! -f "$certificate_pem" ]]; then
  dotnet dev-certs https -ep "$certificate_path" -p "$CERTIFICATE_PASSWORD" >/dev/null
  openssl pkcs12 -in "$certificate_path" -clcerts -nokeys -passin "pass:$CERTIFICATE_PASSWORD" -out "$certificate_pem" >/dev/null 2>&1
  chmod 600 "$certificate_path" "$certificate_pem"
fi

cleanup() {
  local exit_code=$?
  trap - EXIT INT TERM
  rm -f -- "$cookie_jar"
  if [[ -f "$server_pid_file" ]]; then
    server_pid="$(<"$server_pid_file")"
    if [[ "$server_pid" =~ ^[0-9]+$ ]] && kill -0 "$server_pid" 2>/dev/null; then
      kill "$server_pid" 2>/dev/null || true
      wait "$server_pid" 2>/dev/null || true
    fi
    rm -f -- "$server_pid_file"
  fi
  printf '\nMastemis server stopped. PostgreSQL data was retained.\n'
  printf 'Stop PostgreSQL with: %q %q -f %q down\n' "${compose[0]}" "${compose[1]:-}" "$compose_file"
  exit "$exit_code"
}
trap cleanup EXIT INT TERM

printf '[1/6] Starting PostgreSQL...\n'
MASTEMIS_POSTGRES_PASSWORD="$POSTGRES_PASSWORD" "${compose[@]}" -f "$compose_file" up -d postgres

printf '[2/6] Restoring and building Mastemis...\n'
dotnet restore "$repo_root/Mastemis.sln" --nologo
dotnet build "$repo_root/Mastemis.sln" --configuration Release --no-restore --nologo

printf '[3/6] Starting the API server...\n'
connection_string="Host=localhost;Port=5432;Database=mastemis;Username=mastemis;Password=$POSTGRES_PASSWORD"
(
  cd "$repo_root"
  ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS="http://localhost:${server_http_port};${server_url}" \
  ASPNETCORE_Kestrel__Certificates__Default__Path="$certificate_path" \
  ASPNETCORE_Kestrel__Certificates__Default__Password="$CERTIFICATE_PASSWORD" \
  ConnectionStrings__Mastemis="$connection_string" \
  Bootstrap__Administrator__Username="$ADMINISTRATOR_USERNAME" \
  Bootstrap__Administrator__Password="$ADMINISTRATOR_PASSWORD" \
  dotnet run --project src/Server --configuration Release --no-build --no-launch-profile
) >"$server_log" 2>&1 &
server_pid=$!
printf '%s\n' "$server_pid" > "$server_pid_file"

printf '[4/6] Waiting for migrations and readiness...\n'
ready=false
for _ in $(seq 1 90); do
  if ! kill -0 "$server_pid" 2>/dev/null; then
    printf 'The server exited during startup. Last log lines:\n' >&2
    tail -80 "$server_log" >&2
    exit 4
  fi
  if curl --silent --show-error --fail --cacert "$certificate_pem" "$server_url/health/ready" >/dev/null 2>&1; then
    ready=true
    break
  fi
  sleep 1
done
if ! $ready; then
  printf 'Server readiness timed out. Last log lines:\n' >&2
  tail -80 "$server_log" >&2
  exit 5
fi

printf '[5/6] Verifying administrator login...\n'
login_status="$(curl --silent --show-error --cacert "$certificate_pem" \
  --output /dev/null --write-out '%{http_code}' --cookie-jar "$cookie_jar" \
  --header 'Content-Type: application/json' \
  --data "{\"username\":\"$ADMINISTRATOR_USERNAME\",\"password\":\"$ADMINISTRATOR_PASSWORD\",\"rememberMe\":false}" \
  "$server_url/api/auth/login")"
if [[ "$login_status" != '204' ]]; then
  printf 'Administrator login verification returned HTTP %s.\n' "$login_status" >&2
  printf 'If this database predates the generated credentials, rerun with --reset.\n' >&2
  exit 6
fi
curl --silent --show-error --fail --cacert "$certificate_pem" --cookie "$cookie_jar" \
  "$server_url/api/auth/me" >/dev/null
rm -f -- "$cookie_jar"

version_json="$(curl --silent --show-error --fail --cacert "$certificate_pem" "$server_url/api/system/version")"

printf '\n%s\n' '============================================================'
printf 'MASTEMIS DEVELOPMENT ENVIRONMENT IS READY\n'
printf '%s\n' '============================================================'
printf 'Mode:              Host or Connect\n'
printf 'Server URL:        %s\n' "$server_url"
printf 'HTTP fallback:     http://localhost:%s\n' "$server_http_port"
printf 'OpenAPI:           %s/openapi/v1.json\n' "$server_url"
printf 'Liveness:          %s/health/live\n' "$server_url"
printf 'Readiness:         %s/health/ready\n' "$server_url"
printf 'Version response:  %s\n' "$version_json"
printf 'Administrator:     %s\n' "$ADMINISTRATOR_USERNAME"
printf 'Password:          %s\n' "$ADMINISTRATOR_PASSWORD"
printf 'Login verification: PASSED\n'
printf 'Server log:        %s\n' "$server_log"
printf 'Local secret file: %s (mode 600, gitignored)\n' "$env_file"
printf '%s\n' '============================================================'
printf 'Press Ctrl+C to stop the server. PostgreSQL data is retained.\n\n'

if $no_ui; then
  printf '[6/6] UI disabled by --no-ui; server remains active.\n'
  wait "$server_pid"
else
  printf '[6/6] Opening Uno Desktop...\n'
  printf 'Use server URL %s and the credentials printed above.\n' "$server_url"
  (
    cd "$repo_root"
    SSL_CERT_FILE="$certificate_pem" \
    dotnet run --project src/Client/Mastemis.Client.csproj --configuration Release \
      --framework net10.0-desktop --no-build
  )
fi

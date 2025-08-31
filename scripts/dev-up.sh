#!/usr/bin/env bash
# re-exec with bash if invoked via sh/zsh
[ -z "${BASH_VERSION:-}" ] && exec /usr/bin/env bash "$0" "$@"
set -euo pipefail

# ==== Ayarlar (ENV ile override edilebilir) ====
: "${ENABLE_OBS:=1}"    # 1 -> Jaeger vs. kalksın
: "${DO_SEED:=1}"       # 0 -> core seed atla
: "${DO_SMOKE:=1}"      # 0 -> smoke atla
: "${EF_BOOTSTRAP:=1}"  # 1 -> EF migration yoksa oluştur + DB'leri update et
: "${DB_RESET:=0}"      # 1 -> ilgili DB'de public şemasını drop+create (dev only)
: "${EF_BASELINE:=0}"   # 1 -> (Core hariç) Migrations klasörünü sıfırla ve tek "Init" migration üret
: "${CLEAN_COMPOSE:=0}" # 1 -> önceki docker compose stack'ini 'down -v --remove-orphans' ile temizle
: "${SKIP_INFRA:=0}"    # 1 -> infra (db up + EF bootstrap) adımlarını tamamen atla (kod-only redeploy)

# Basit argüman işleme: --clean / --reset => CLEAN_COMPOSE=1
for arg in "$@"; do
  case "$arg" in
    --clean|--reset) CLEAN_COMPOSE=1 ;;
  esac
done

# Repo kökü (bash/zsh uyumlu)
SOURCE="${BASH_SOURCE[0]:-$0}"
ROOT="$(cd "$(dirname "$SOURCE")/.." && pwd)"
cd "$ROOT"

say()  { echo -e "\n\033[1;36m▶ $*\033[0m"; }
fail() { echo -e "\n\033[1;31m✗ $*\033[0m"; exit 1; }

# HTTP 200 bekleme (gateway/readiness)
wait_http200() {
  local url="$1"
  local header="${2:-}"
  local tries="${3:-60}"
  for ((i=1; i<=tries; i++)); do
    if curl -fsS ${header:+-H "$header"} "$url" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  fail "timeout waiting 200: $url"
}

# Compose servis adıyla health bekleme (container id auto)
wait_healthy() {
  local svc="$1"
  say "waiting healthy: $svc"
  for _ in {1..60}; do
    cid="$(docker compose ps -q "$svc" 2>/dev/null || true)"
    if [[ -n "$cid" ]]; then
      st="$(docker inspect -f '{{.State.Health.Status}}' "$cid" 2>/dev/null || echo "starting")"
      [[ "$st" == "healthy" ]] && return 0
    fi
    sleep 1
  done
  fail "health timeout: $svc"
}

drop_schema() {
  local svc="$1" db="$2"
  say "reset schema: $db on $svc (DROP SCHEMA public CASCADE; CREATE SCHEMA public;)"
  docker compose --profile infra exec -T "$svc" bash -lc "psql -U postgres -d $db -c 'DROP SCHEMA public CASCADE; CREATE SCHEMA public;'"
}

clean_compose() {
  say "docker compose down -v --remove-orphans (full reset)"
  docker compose down -v --remove-orphans || true
}

# EF: Migration var mı yok mu kontrol et, yoksa oluştur; sonra DB update
ensure_migration_and_update() {
  local proj_path="$1"   # örn: src/Perf.Api
  local ctx="$2"         # örn: PerfDbContext
  local dbname="$3"      # örn: perf
  local port="$4"        # örn: 5433
  local svc="$5"         # örn: perf-db

  local mig_dir="$proj_path/Infrastructure/Migrations"

  if [[ "$EF_BOOTSTRAP" == "1" ]]; then
    # Core dışı projelerde (Perf/Comp) baseline modunu destekle
    local is_core=0
    [[ "$proj_path" == "src/Core.Api" ]] && is_core=1

    # (Opsiyonel) Baseline: migration klasörünü sıfırla ve tek migration üret
    if [[ "$EF_BASELINE" == "1" && "$is_core" -eq 0 ]]; then
      say "EF: baseline mode for $(basename "$proj_path") → wiping migrations and recreating 'Init'"
      rm -rf "$mig_dir"
      mkdir -p "$mig_dir"
      dotnet ef migrations add Init \
        -p "$proj_path" -s "$proj_path" --context "$ctx" \
        -o Infrastructure/Migrations
    fi

    # (Opsiyonel) DB şemasını sıfırla
    if [[ "$DB_RESET" == "1" || "$EF_BASELINE" == "1" && "$is_core" -eq 0 ]]; then
      drop_schema "$svc" "$dbname"
    fi

    # dotnet-ef hazır mı?
    dotnet tool restore >/dev/null 2>&1 || true

    # migrations klasörü mevcut değilse oluştur
    mkdir -p "$mig_dir"

    # klasörde *.cs var mı? yoksa InitialCreate oluştur
    if [[ -z "$(find "$mig_dir" -maxdepth 1 -name '*.cs' -print -quit 2>/dev/null)" ]]; then
      say "EF: creating InitialCreate for $(basename "$proj_path") ($ctx)"
      dotnet ef migrations add InitialCreate \
        -p "$proj_path" -s "$proj_path" --context "$ctx" \
        -o Infrastructure/Migrations
    else
      say "EF: migrations already present for $(basename "$proj_path")"
    fi

    say "EF: database update -> $dbname@localhost:$port"
    ConnectionStrings__Default="Host=localhost;Port=$port;Database=$dbname;Username=postgres;Password=postgres" \
      dotnet ef database update -p "$proj_path" -s "$proj_path" --context "$ctx"
  fi
}

# ---- .NET restore/build/test ----
say "dotnet restore/build/test"
dotnet --info >/dev/null
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build

# ---- Optional: Clean previous docker compose stack ----
if [[ "$CLEAN_COMPOSE" == "1" ]]; then
  say "cleaning previous docker compose stack (down -v --remove-orphans)"
  clean_compose
fi

# ---- Docker infra + EF bootstrap (opsiyonel) ----
if [[ "$SKIP_INFRA" != "1" ]]; then
  say "docker compose (infra) up"
  docker compose --profile infra up -d --build

  wait_healthy postgres
  wait_healthy perf-db
  wait_healthy comp-db

  # ---- EF Bootstrap (tabloları oluştur) ----
  say "EF bootstrap (create missing migrations + update DBs)"
  ensure_migration_and_update "src/Core.Api" "CoreDbContext" "core" 5432 "postgres"
  ensure_migration_and_update "src/Perf.Api" "PerfDbContext" "perf" 5433 "perf-db"
  ensure_migration_and_update "src/Comp.Api" "CompDbContext" "comp" 5434 "comp-db"
else
  say "SKIP_INFRA=1 → infra ve EF bootstrap adımlarını atlıyorum"
fi

# ---- Core seed ----
if [[ "$DO_SEED" == "1" ]]; then
  say "checking core schema before seeding"
  exists="$(docker compose --profile infra exec -T postgres psql -U postgres -d core -tAc "SELECT (to_regclass('\"Tenants\"') IS NOT NULL) AND (to_regclass('\"TenantDomains\"') IS NOT NULL) AND (to_regclass('\"DomainMappings\"') IS NOT NULL);")"
  if [[ "$exists" != "t" ]]; then
    say "core schema not ready → auto EF bootstrap (Core.Api)"
    ensure_migration_and_update "src/Core.Api" "CoreDbContext" "core" 5432 "postgres"
    # tekrar kontrol
    exists="$(docker compose --profile infra exec -T postgres psql -U postgres -d core -tAc "SELECT (to_regclass('\"Tenants\"') IS NOT NULL) AND (to_regclass('\"TenantDomains\"') IS NOT NULL) AND (to_regclass('\"DomainMappings\"') IS NOT NULL);")"
    [[ "$exists" != "t" ]] && fail "core schema still not ready after auto EF bootstrap."
  fi

  say "seeding core database"
  docker compose --profile infra exec -T postgres bash -lc "cat <<'SQL' | psql -v ON_ERROR_STOP=1 -U postgres -d core
BEGIN;
TRUNCATE TABLE \"DomainMappings\" RESTART IDENTITY CASCADE;
TRUNCATE TABLE \"TenantDomains\" RESTART IDENTITY CASCADE;
TRUNCATE TABLE \"Tenants\" RESTART IDENTITY CASCADE;

INSERT INTO \"Tenants\" (\"Id\",\"Name\",\"Slug\",\"Status\",\"CreatedAt\") VALUES
  ('a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac','Firm 1','firm1','active', NOW() AT TIME ZONE 'utc'),
  ('44709835-d55a-ef2a-2327-5fdca19e55d8','Firm 2','firm2','active', NOW() AT TIME ZONE 'utc');

INSERT INTO \"TenantDomains\" (\"Id\",\"TenantId\",\"Host\",\"IsDefault\") VALUES
  ('33333333-3333-3333-3333-333333333331','a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac','pys.local', TRUE),
  ('33333333-3333-3333-3333-333333333332','44709835-d55a-ef2a-2327-5fdca19e55d8','pay.local', TRUE);

INSERT INTO \"DomainMappings\" (\"Id\",\"Host\",\"Module\",\"TenantId\",\"PathMode\",\"TenantSlug\",\"IsActive\") VALUES
  ('11111111-1111-1111-1111-111111111111','pys.local','performance', NULL, 'slug', NULL, TRUE),
  ('22222222-2222-2222-2222-222222222222','pay.local','compensation','44709835-d55a-ef2a-2327-5fdca19e55d8', 'host', NULL, TRUE);
COMMIT;
SQL"
fi

# ---- API'ler + Gateway ----
say "docker compose (apis) up"
docker compose --profile core --profile perf --profile comp up -d --build

say "docker compose (gateway) up"
docker compose --profile gw up -d --build

# ---- (Opsiyonel) Observability ----
if [[ "${ENABLE_OBS}" == "1" ]]; then
  say "docker compose (obs) up"
  docker compose --profile obs up -d --build
fi

# ---- (Opsiyonel) Smoke ----
if [[ "$DO_SMOKE" == "1" ]]; then
  say "waiting gateway + apis ready (HTTP 200)"
  # Perf (slug mode)
  wait_http200 "http://localhost:8080/api/perf/firm1/me" "Host: pys.local" 90
  
  # Comp: önce host-mode (/api/comp/me), olmazsa slug-mode (/api/comp/firm2/me)
  COMP_SMOKE_URL="/api/comp/me"
  if curl -fsS -H "Host: pay.local" "http://localhost:8080${COMP_SMOKE_URL}" >/dev/null 2>&1; then
    :
  else
    wait_http200 "http://localhost:8080/api/comp/firm2/me" "Host: pay.local" 90
    COMP_SMOKE_URL="/api/comp/firm2/me"
  fi
  
  say "smoke: /api/perf/firm1/me via pys.local"
  if command -v jq >/dev/null 2>&1; then
    curl -fsS -H "Host: pys.local" http://localhost:8080/api/perf/firm1/me | jq .
  else
    curl -fsS -H "Host: pys.local" http://localhost:8080/api/perf/firm1/me
  fi
  
  say "smoke: ${COMP_SMOKE_URL} via pay.local"
  if command -v jq >/dev/null 2>&1; then
    curl -fsS -H "Host: pay.local" "http://localhost:8080${COMP_SMOKE_URL}" | jq .
  else
    curl -fsS -H "Host: pay.local" "http://localhost:8080${COMP_SMOKE_URL}"
  fi
fi

say "ALL DONE ✅"
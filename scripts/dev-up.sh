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
    local is_core=0
    [[ "$proj_path" == "src/Core.Api" ]] && is_core=1

    if [[ "$EF_BASELINE" == "1" && "$is_core" -eq 0 ]]; then
      say "EF: baseline mode for $(basename "$proj_path") → wiping migrations and recreating 'Init'"
      rm -rf "$mig_dir"
      mkdir -p "$mig_dir"
      dotnet ef migrations add Init -p "$proj_path" -s "$proj_path" --context "$ctx" -o Infrastructure/Migrations
    fi

    if [[ "$DB_RESET" == "1" || ( "$EF_BASELINE" == "1" && "$is_core" -eq 0 ) ]]; then
      drop_schema "$svc" "$dbname"
    fi

    dotnet tool restore >/dev/null 2>&1 || true
    mkdir -p "$mig_dir"

    if [[ -z "$(find "$mig_dir" -maxdepth 1 -name '*.cs' -print -quit 2>/dev/null)" ]]; then
      say "EF: creating InitialCreate for $(basename "$proj_path") ($ctx)"
      dotnet ef migrations add InitialCreate -p "$proj_path" -s "$proj_path" --context "$ctx" -o Infrastructure/Migrations
    else
      say "EF: migrations already present for $(basename "$proj_path")"
    fi

    say "EF: database update -> $dbname@localhost:$port"
    ConnectionStrings__Default="Host=localhost;Port=$port;Database=$dbname;Username=postgres;Password=postgres" \
      dotnet ef database update -p "$proj_path" -s "$proj_path" --context "$ctx"
  fi
}

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
    exists="$(docker compose --profile infra exec -T postgres psql -U postgres -d core -tAc "SELECT (to_regclass('\"Tenants\"') IS NOT NULL) AND (to_regclass('\"TenantDomains\"') IS NOT NULL) AND (to_regclass('\"DomainMappings\"') IS NOT NULL);")"
    [[ "$exists" != "t" ]] && fail "core schema still not ready after auto EF bootstrap."
  fi

  say "seeding core database"

  # SQL'i container içinde quoted heredoc ile yaz → dış kabuk $ işaretlerine hiç dokunmasın
  docker compose --profile infra exec -T postgres bash -lc '
set -e
cat >/tmp/core_seed.sql <<'\''SQL'\''
BEGIN;
-- Sıra: ilişkilerden dolayı önce çocuk tabloları boşalt
TRUNCATE TABLE "UserTenants" RESTART IDENTITY CASCADE;
TRUNCATE TABLE "Users" RESTART IDENTITY CASCADE;
TRUNCATE TABLE "Roles" RESTART IDENTITY CASCADE;
TRUNCATE TABLE "DomainMappings" RESTART IDENTITY CASCADE;
TRUNCATE TABLE "TenantDomains" RESTART IDENTITY CASCADE;
TRUNCATE TABLE "Tenants" RESTART IDENTITY CASCADE;

-- Tenants (deterministik GUID + UTC createdAt)
INSERT INTO "Tenants" ("Id","Name","Slug","Status","CreatedAt") VALUES
  ( '\''a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac'\'','\''Firm 1'\'','\''firm1'\'','\''active'\'', NOW() AT TIME ZONE '\''utc'\'' ),
  ( '\''44709835-d55a-ef2a-2327-5fdca19e55d8'\'','\''Firm 2'\'','\''firm2'\'','\''active'\'', NOW() AT TIME ZONE '\''utc'\'' );

-- TenantDomains
INSERT INTO "TenantDomains" ("Id","TenantId","Host","IsDefault") VALUES
  ( '\''33333333-3333-3333-3333-333333333331'\'','\''a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac'\'','\''pys.local'\'', TRUE ),
  ( '\''33333333-3333-3333-3333-333333333332'\'','\''44709835-d55a-ef2a-2327-5fdca19e55d8'\'','\''pay.local'\'', TRUE );

-- DomainMappings (ikisi de slug-mode)
INSERT INTO "DomainMappings" ("Id","Host","Module","TenantId","PathMode","TenantSlug","IsActive") VALUES
  ( '\''11111111-1111-1111-1111-111111111111'\'','\''pys.local'\'','\''performance'\'', NULL, '\''slug'\'', NULL, TRUE ),
  ( '\''22222222-2222-2222-2222-222222222222'\'','\''pay.local'\'','\''compensation'\'', NULL, '\''slug'\'', NULL, TRUE );

-- Roles
INSERT INTO "Roles" ("Id","Name") VALUES
  ( '\''0F000000-0000-0000-0000-0000000000A1'\'','\''admin'\'' ),
  ( '\''0F000000-0000-0000-0000-0000000000A2'\'','\''viewer'\'' );

-- Users (PasswordHash = '\''Pass123$'\'' — bcrypt)
INSERT INTO "Users" ("Id","Email","PasswordHash","IsActive","CreatedAt") VALUES
  ( '\''0E000000-0000-0000-0000-0000000000B1'\'','\''admin@firm1.local'\'','\''$2a$10$k4V0Ui0s5jJQk9S0iJYt9uYq2WmFQ7Y0yQ9bA4hQv8q1f9o8o0s3C'\'', TRUE, NOW() AT TIME ZONE '\''utc'\'' ),
  ( '\''0E000000-0000-0000-0000-0000000000B2'\'','\''viewer@firm2.local'\'','\''$2a$10$k4V0Ui0s5jJQk9S0iJYt9uYq2WmFQ7Y0yQ9bA4hQv8q1f9o8o0s3C'\'', TRUE, NOW() AT TIME ZONE '\''utc'\'' );

-- UserTenants (RBAC bağları)
INSERT INTO "UserTenants" ("UserId","TenantId","RoleId") VALUES
  ( '\''0E000000-0000-0000-0000-0000000000B1'\'','\''a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac'\'','\''0F000000-0000-0000-0000-0000000000A1'\'' ),
  ( '\''0E000000-0000-0000-0000-0000000000B2'\'','\''44709835-d55a-ef2a-2327-5fdca19e55d8'\'','\''0F000000-0000-0000-0000-0000000000A2'\'' );
COMMIT;
SQL
psql -v ON_ERROR_STOP=1 -U postgres -d core -f /tmp/core_seed.sql
'

fi  # <-- DO_SEED bloğunu kapat

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


say "ALL DONE ✅"
#!/usr/bin/env bash
set -euo pipefail

say()  { echo -e "\n\033[1;36m▶ $*\033[0m"; }
fail() { echo -e "\n\033[1;31m✗ $*\033[0m"; exit 1; }

say "checking core schema before seeding"
exists="$(docker compose --profile infra exec -T postgres psql -U postgres -d core -tAc "SELECT (to_regclass('\"Tenants\"') IS NOT NULL) AND (to_regclass('\"TenantDomains\"') IS NOT NULL) AND (to_regclass('\"DomainMappings\"') IS NOT NULL);")"
if [[ "$exists" != "t" ]]; then
  fail "core schema not ready for seeding."
fi

say "seeding core database"

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
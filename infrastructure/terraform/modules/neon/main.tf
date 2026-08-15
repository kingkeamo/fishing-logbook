# Neon PostgreSQL project for a single environment.
#
# Default database/role stay Neon's defaults (`neondb` / `neondb_owner`) — do not add a
# second database. Extra branches/roles/databases are separate resources, added later
# only if explicitly approved.
#
# Dev already has a Console-created project: import it (do not apply a create). See
# infrastructure/README.md. Destroying this resource deletes the database.
#
# Non-HashiCorp providers must declare `source` in every module that uses them,
# otherwise Terraform looks for hashicorp/<name>. Version is pinned in each
# environment's versions.tf.

terraform {
  required_providers {
    neon = {
      source = "kislerdm/neon"
    }
  }
}

locals {
  resource_prefix = "fishing-logbook-${var.environment}"
}

resource "neon_project" "this" {
  name                      = local.resource_prefix
  region_id                 = var.region == "" ? null : var.region
  org_id                    = var.org_id == "" ? null : var.org_id
  pg_version                = var.pg_version
  history_retention_seconds = var.history_retention_seconds

  lifecycle {
    prevent_destroy = true
  }
}

# Neon PostgreSQL module (skeleton).
#
# This module will define the FishingLogBook Neon project/branch/database for a single
# environment. It intentionally declares NO resources yet.
#
# Planned resources (added only when explicitly approved, subject to provider support):
#   - neon_project
#   - neon_branch
#   - neon_database
#   - neon_role
#
# Dev and Prod must use separate databases. Never point Development at Production.

locals {
  resource_prefix = "fishing-logbook-${var.environment}"
}

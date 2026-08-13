# Fly.io module (naming skeleton only).
#
# Actual Fly config lives in infrastructure/fly/ (fly.dev.toml / fly.prod.toml) and is
# applied with flyctl, not this Terraform module. See infrastructure/fly/README.md.
#
# GitHub Actions may deploy new versions to an ALREADY-EXISTING Fly app via flyctl.
# CI must never create, resize or destroy Fly infrastructure.

locals {
  resource_prefix = "fishing-logbook-${var.environment}"
}

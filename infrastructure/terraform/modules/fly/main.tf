# Fly.io module (skeleton).
#
# This module will describe the FishingLogBook API application on Fly.io for a single
# environment. It intentionally declares NO resources yet.
#
# Planned resources (added only when explicitly approved):
#   - fly_app
#   - fly_machine (start with the smallest practical VM; no autoscaling initially)
#
# GitHub Actions may deploy new versions to an ALREADY-EXISTING Fly app via flyctl.
# CI must never create, resize or destroy Fly infrastructure.

locals {
  resource_prefix = "fishing-logbook-${var.environment}"
}

# Cloudflare Pages module (skeleton).
#
# This module will describe the FishingLogBook PWA hosting project on Cloudflare Pages
# for a single environment. It intentionally declares NO resources yet.
#
# Planned resources (added only when explicitly approved):
#   - cloudflare_pages_project
#
# Prefer distinct Cloudflare Pages projects (or clearly isolated targets) per environment.
# API URLs must be supplied as environment-specific configuration, never hard-coded.

locals {
  resource_prefix = "fishing-logbook-${var.environment}"
}

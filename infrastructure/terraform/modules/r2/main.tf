# Cloudflare R2 module (skeleton).
#
# This module will describe the FishingLogBook R2 bucket used for catch photographs for
# a single environment. It intentionally declares NO resources yet.
#
# Planned resources (added only when explicitly approved):
#   - cloudflare_r2_bucket
#
# Buckets must NOT be publicly writable. R2 secret credentials must never be placed in
# the PWA. Dev and Prod buckets must be separate.

locals {
  bucket_name = "fishing-logbook-${var.environment}"
}

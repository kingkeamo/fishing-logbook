# Private R2 bucket for catch photographs (one per environment).
#
# This is NOT the Terraform state bucket. State lives in a separate, manually created
# bucket (see infrastructure/README.md). Destroying this resource deletes stored photos.
#
# The bucket is private: no public access/domain is configured. The PWA must never
# receive R2 credentials; uploads will use short-lived presigned URLs from the API.

terraform {
  required_providers {
    cloudflare = {
      source = "cloudflare/cloudflare"
    }
  }
}

locals {
  resource_prefix = "fishing-logbook-${var.environment}"
}

resource "cloudflare_r2_bucket" "photos" {
  account_id    = var.account_id
  name          = local.resource_prefix
  location      = var.location == "" ? null : var.location
  jurisdiction  = "default"
  storage_class = "Standard"

  lifecycle {
    prevent_destroy = true
  }
}

# Presigned-URL uploads/downloads happen directly from the browser, so the bucket must
# allow those origins to PUT/GET/HEAD cross-origin. Skipped when no origins are supplied.
resource "cloudflare_r2_bucket_cors" "photos" {
  count        = length(var.cors_allowed_origins) == 0 ? 0 : 1
  account_id   = var.account_id
  bucket_name  = cloudflare_r2_bucket.photos.name
  jurisdiction = "default"

  rules = [
    {
      allowed = {
        methods = var.cors_allowed_methods
        origins = var.cors_allowed_origins
        headers = var.cors_allowed_headers
      }
      expose_headers  = ["ETag"]
      max_age_seconds = 3600
    }
  ]
}

# Disposable environments (E2E) should not accumulate test photographs indefinitely.
resource "cloudflare_r2_bucket_lifecycle" "photos" {
  count        = var.object_expiration_days == null ? 0 : 1
  account_id   = var.account_id
  bucket_name  = cloudflare_r2_bucket.photos.name
  jurisdiction = "default"

  rules = [
    {
      id      = "expire-objects"
      enabled = true
      conditions = {
        prefix = ""
      }
      delete_objects_transition = {
        condition = {
          type    = "Age"
          max_age = var.object_expiration_days * 86400
        }
      }
    }
  ]
}

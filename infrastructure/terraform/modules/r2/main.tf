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

# Cloudflare Pages project for the Blazor WASM PWA (one per environment).
#
# Direct upload: omit `source` so GitHub Actions (later) can publish pre-built static
# files with wrangler. Cloudflare does not build the .NET app. Creating this resource
# does not deploy the PWA; the project will exist empty until the first upload.
#
# API URLs are baked into wwwroot/appsettings at publish time, not Cloudflare env vars.

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

resource "cloudflare_pages_project" "web" {
  account_id        = var.account_id
  name              = local.resource_prefix
  production_branch = var.production_branch

  lifecycle {
    prevent_destroy = true
  }
}

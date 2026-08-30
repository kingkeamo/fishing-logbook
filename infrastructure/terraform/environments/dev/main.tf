module "cognito" {
  source                  = "../../modules/cognito"
  environment             = var.environment
  region                  = var.aws_region
  api_resource_identifier = var.cognito_api_resource_identifier
  callback_urls           = var.cognito_callback_urls
  logout_urls             = var.cognito_logout_urls
}

module "neon" {
  source                    = "../../modules/neon"
  environment               = var.environment
  region                    = var.neon_region
  org_id                    = var.neon_org_id
  pg_version                = var.neon_pg_version
  history_retention_seconds = var.neon_history_retention_seconds
}

module "fly" {
  source      = "../../modules/fly"
  environment = var.environment
  region      = var.fly_region
  vm_size     = var.fly_vm_size
}

module "r2" {
  source               = "../../modules/r2"
  environment          = var.environment
  account_id           = var.cloudflare_account_id
  location             = var.r2_location
  cors_allowed_origins = var.r2_cors_allowed_origins
}

# Dedicated bucket for local Playwright E2E runs (tests/FishingLogBook.E2E), so
# uploaded test photographs never land in the fishing-logbook-dev or
# fishing-logbook-prod buckets. Not used by CI today - only local runs. Objects expire
# automatically after 48 hours so disposable test photos never accumulate.
module "r2_e2e" {
  source                 = "../../modules/r2"
  environment            = "e2e"
  account_id             = var.cloudflare_account_id
  location               = var.r2_location
  cors_allowed_origins   = ["http://localhost:5019"]
  object_expiration_days = 2
}

module "cloudflare_pages" {
  source            = "../../modules/cloudflare-pages"
  environment       = var.environment
  account_id        = var.cloudflare_account_id
  production_branch = var.pages_production_branch
}

module "grafana_cloud" {
  source      = "../../modules/grafana-cloud"
  count       = var.grafana_cloud_stack_slug == "" ? 0 : 1
  environment = var.environment
  stack_slug  = var.grafana_cloud_stack_slug
}

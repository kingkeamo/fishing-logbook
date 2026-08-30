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
  cors_allowed_methods = ["PUT"]
  cors_allowed_headers = ["Content-Type", "X-Correlation-Id"]
}

module "cloudflare_pages" {
  source            = "../../modules/cloudflare-pages"
  environment       = var.environment
  account_id        = var.cloudflare_account_id
  production_branch = var.pages_production_branch
  custom_domains    = [var.app_domain]
}

module "cloudflare_root_pages" {
  source            = "../../modules/cloudflare-pages"
  environment       = var.environment
  account_id        = var.cloudflare_account_id
  project_name      = "catch-but-dont-forget-root"
  production_branch = var.pages_production_branch
  custom_domains    = [var.root_domain]
}

module "grafana_cloud" {
  source      = "../../modules/grafana-cloud"
  count       = var.grafana_cloud_stack_slug == "" ? 0 : 1
  environment = var.environment
  stack_slug  = var.grafana_cloud_stack_slug
}

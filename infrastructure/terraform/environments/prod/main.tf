terraform {
  required_version = ">= 1.6.0"
}

module "cognito" {
  source        = "../../modules/cognito"
  environment   = var.environment
  callback_urls = var.cognito_callback_urls
  logout_urls   = var.cognito_logout_urls
}

module "neon" {
  source      = "../../modules/neon"
  environment = var.environment
  region      = var.neon_region
}

module "fly" {
  source      = "../../modules/fly"
  environment = var.environment
  region      = var.fly_region
  vm_size     = var.fly_vm_size
}

module "r2" {
  source      = "../../modules/r2"
  environment = var.environment
}

module "cloudflare_pages" {
  source            = "../../modules/cloudflare-pages"
  environment       = var.environment
  production_branch = var.pages_production_branch
}

output "cognito_resource_prefix" {
  description = "Naming prefix for Cognito resources."
  value       = module.cognito.resource_prefix
}

output "neon_project_name" {
  description = "Planned Neon project name."
  value       = module.neon.project_name
}

output "fly_app_name" {
  description = "Planned Fly.io application name."
  value       = module.fly.app_name
}

output "r2_bucket_name" {
  description = "Planned R2 bucket name."
  value       = module.r2.bucket_name
}

output "cloudflare_pages_project_name" {
  description = "Planned Cloudflare Pages project name."
  value       = module.cloudflare_pages.project_name
}

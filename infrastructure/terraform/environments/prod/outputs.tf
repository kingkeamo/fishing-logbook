output "cognito_resource_prefix" {
  description = "Naming prefix for Cognito resources."
  value       = module.cognito.resource_prefix
}

output "neon_project_name" {
  description = "Neon project name."
  value       = module.neon.project_name
}

output "neon_project_id" {
  description = "Neon project ID."
  value       = module.neon.project_id
}

output "neon_database_name" {
  description = "Default Neon database name."
  value       = module.neon.database_name
}

output "neon_database_host" {
  description = "Default Neon database host."
  value       = module.neon.database_host
}

output "neon_database_user" {
  description = "Default Neon database role."
  value       = module.neon.database_user
}

output "neon_connection_uri" {
  description = "Default Neon connection URI. Contains credentials — do not commit or log. Fly secrets remain the API source of truth."
  value       = module.neon.connection_uri
  sensitive   = true
}

output "fly_app_name" {
  description = "Planned Fly.io application name."
  value       = module.fly.app_name
}

output "r2_bucket_name" {
  description = "R2 bucket name for catch photographs."
  value       = module.r2.bucket_name
}

output "cloudflare_pages_project_name" {
  description = "Cloudflare Pages project name."
  value       = module.cloudflare_pages.project_name
}

output "cloudflare_pages_subdomain" {
  description = "Cloudflare Pages subdomain."
  value       = module.cloudflare_pages.subdomain
}

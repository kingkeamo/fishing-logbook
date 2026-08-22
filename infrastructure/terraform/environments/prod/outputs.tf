output "cognito_resource_prefix" {
  description = "Naming prefix for Cognito resources."
  value       = module.cognito.resource_prefix
}

output "cognito_user_pool_id" {
  description = "Cognito user pool ID. Public identifier."
  value       = module.cognito.user_pool_id
}

output "cognito_client_id" {
  description = "Public PWA app client ID. Not a secret."
  value       = module.cognito.client_id
}

output "cognito_authority" {
  description = "OIDC issuer/authority for Blazor OIDC and API JWT validation."
  value       = module.cognito.authority
}

output "cognito_hosted_ui_domain" {
  description = "Cognito hosted UI / managed login hostname (no scheme)."
  value       = module.cognito.hosted_ui_domain
}

output "cognito_api_scope" {
  description = "Resource-server scope the PWA must request for FishingLogBook API access."
  value       = module.cognito.api_scope
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

output "cloudflare_pages_domains" {
  description = "Custom domains attached to the production PWA Pages project."
  value       = module.cloudflare_pages.domains
}

output "cloudflare_root_pages_project_name" {
  description = "Cloudflare Pages project name for the small public root site."
  value       = module.cloudflare_root_pages.project_name
}

output "cloudflare_root_pages_subdomain" {
  description = "Cloudflare Pages subdomain for the small public root site."
  value       = module.cloudflare_root_pages.subdomain
}

output "cloudflare_root_pages_domains" {
  description = "Custom domains attached to the public root Pages project."
  value       = module.cloudflare_root_pages.domains
}

output "grafana_url" {
  description = "Grafana UI URL. Null until grafana_cloud_stack_slug is set."
  value       = try(module.grafana_cloud[0].grafana_url, null)
}

output "grafana_loki_push_url" {
  description = "Loki push URL for ExternalLogging__Url. Null until grafana_cloud_stack_slug is set."
  value       = try(module.grafana_cloud[0].loki_push_url, null)
}

output "grafana_loki_user" {
  description = "Loki basic-auth user for ExternalLogging__User. Null until grafana_cloud_stack_slug is set."
  value       = try(module.grafana_cloud[0].loki_user, null)
}

output "grafana_loki_write_token" {
  description = "Loki write token for ExternalLogging__ApiToken. Sensitive — copy into Fly secrets or user-secrets, never commit or log."
  value       = try(module.grafana_cloud[0].loki_write_token, null)
  sensitive   = true
}

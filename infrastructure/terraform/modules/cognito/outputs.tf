output "resource_prefix" {
  description = "Naming prefix used by Cognito resources."
  value       = local.resource_prefix
}

output "user_pool_id" {
  description = "Cognito user pool ID. Public identifier."
  value       = aws_cognito_user_pool.this.id
}

output "client_id" {
  description = "Public PWA app client ID. Not a secret."
  value       = aws_cognito_user_pool_client.pwa.id
}

output "authority" {
  description = "OIDC issuer/authority for Blazor OIDC and API JWT validation."
  value       = "https://cognito-idp.${var.region}.amazonaws.com/${aws_cognito_user_pool.this.id}"
}

output "hosted_ui_domain" {
  description = "Cognito hosted UI / managed login hostname (no scheme)."
  value       = "${aws_cognito_user_pool_domain.this.domain}.auth.${var.region}.amazoncognito.com"
}

output "api_scope" {
  description = "Resource-server scope the PWA must request for FishingLogBook API access."
  value       = "${local.api_identifier}/${local.api_scope_name}"
}

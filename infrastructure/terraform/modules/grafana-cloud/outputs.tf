output "stack_slug" {
  description = "Grafana Cloud stack slug."
  value       = data.grafana_cloud_stack.this.slug
}

output "grafana_url" {
  description = "Grafana UI URL for this stack."
  value       = data.grafana_cloud_stack.this.url
}

output "loki_push_url" {
  description = "Loki HTTP push URL for ExternalLogging__Url."
  value       = local.loki_push_url
}

output "loki_user" {
  description = "Loki basic-auth user (instance id) for ExternalLogging__User."
  value       = tostring(data.grafana_cloud_stack.this.logs_user_id)
}

output "loki_write_token" {
  description = "Loki write token for ExternalLogging__ApiToken. Sensitive — copy into Fly secrets or user-secrets, never commit or log."
  value       = grafana_cloud_access_policy_token.loki_write.token
  sensitive   = true
}

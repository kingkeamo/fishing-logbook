variable "environment" {
  type        = string
  description = "Deployment environment name (for example: dev or prod)."
}

variable "stack_slug" {
  type        = string
  description = "Existing Grafana Cloud stack slug (the subdomain of https://<slug>.grafana.net). Account-specific; supply locally, never commit."
}

variable "environment" {
  type        = string
  description = "Deployment environment name."
  default     = "dev"
}

variable "aws_region" {
  type        = string
  description = "AWS region for Cognito resources. Dev must set this in terraform.tfvars."
  default     = "us-east-1"
}

variable "cognito_api_resource_identifier" {
  type        = string
  description = "Cognito resource-server identifier and RFC 8707 resource/aud URI. Must match Auth:ApiResource."
  default     = "https://fishing-logbook-dev-api.fly.dev"
}

variable "cloudflare_account_id" {
  type        = string
  description = "Cloudflare account ID that owns the Pages project and R2 bucket. Account-specific; supply locally, never commit."
  default     = ""
}

variable "r2_location" {
  type        = string
  description = "R2 location hint (enam = Eastern North America). Honoured only on first create."
  default     = "enam"
}

variable "cognito_callback_urls" {
  type        = list(string)
  description = "Exact OAuth callback URLs for the PWA app client."
  default = [
    "https://localhost:7005/authentication/login-callback",
    "http://localhost:5019/authentication/login-callback",
    "https://fishing-logbook-dev.pages.dev/authentication/login-callback",
  ]
}

variable "cognito_logout_urls" {
  type        = list(string)
  description = "Exact sign-out redirect URLs for the PWA app client."
  default = [
    "https://localhost:7005/authentication/logout-callback",
    "https://localhost:7005/",
    "http://localhost:5019/authentication/logout-callback",
    "http://localhost:5019/",
    "https://fishing-logbook-dev.pages.dev/authentication/logout-callback",
    "https://fishing-logbook-dev.pages.dev/",
  ]
}

variable "neon_region" {
  type        = string
  description = "Neon region identifier (for example: aws-us-east-1). Must match an existing project before import."
  default     = ""
}

variable "neon_org_id" {
  type        = string
  description = "Neon organisation ID. Account-specific; supply locally, never commit."
  default     = ""
}

variable "neon_pg_version" {
  type        = number
  nullable    = true
  default     = null
  description = "Postgres major version. Must match an existing project before import."
}

variable "neon_history_retention_seconds" {
  type        = number
  description = "PITR history retention in seconds. Free plan maximum is 21600 (6 hours)."
  default     = 21600
}

variable "fly_region" {
  type        = string
  description = "Primary Fly.io region for the API."
  default     = ""
}

variable "fly_vm_size" {
  type        = string
  description = "Fly.io machine size."
  default     = "shared-cpu-1x"
}

variable "pages_production_branch" {
  type        = string
  description = "Git branch mapped to this Pages environment."
  default     = "develop"
}

variable "grafana_cloud_stack_slug" {
  type        = string
  description = "Existing Grafana Cloud stack slug (https://<slug>.grafana.net). Leave empty to skip Grafana resources. Account-specific; supply locally, never commit."
  default     = ""
}

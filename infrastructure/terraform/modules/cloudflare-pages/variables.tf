variable "environment" {
  type        = string
  description = "Deployment environment name (for example: dev or prod)."
}

variable "account_id" {
  type        = string
  description = "Cloudflare account ID. Account-specific; supply locally, never commit."
}

variable "production_branch" {
  type        = string
  description = "Git branch that maps to this Pages environment."
  default     = "main"
}

variable "project_name" {
  type        = string
  description = "Optional explicit Pages project name. Defaults to fishing-logbook-<environment>."
  default     = ""
}

variable "custom_domains" {
  type        = set(string)
  description = "Custom domains to attach to the Pages project. DNS records are managed separately."
  default     = []
}

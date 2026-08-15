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

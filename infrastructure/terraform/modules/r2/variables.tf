variable "environment" {
  type        = string
  description = "Deployment environment name (for example: dev or prod)."
}

variable "account_id" {
  type        = string
  description = "Cloudflare account ID. Account-specific; supply locally, never commit."
}

variable "location" {
  type        = string
  description = "R2 location hint (for example: enam). Honoured only on first create."
  default     = ""
}

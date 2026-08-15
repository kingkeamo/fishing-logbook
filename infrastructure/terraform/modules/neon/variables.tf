variable "environment" {
  type        = string
  description = "Deployment environment name (for example: dev or prod)."
}

variable "region" {
  type        = string
  description = "Neon region identifier (for example: aws-us-east-1). Must match an existing project before import."
  default     = ""
}

variable "org_id" {
  type        = string
  description = "Neon organisation ID. Account-specific; supply locally, never commit."
  default     = ""
}

variable "pg_version" {
  type        = number
  nullable    = true
  default     = null
  description = "Postgres major version. Must match an existing project before import."
}

variable "history_retention_seconds" {
  type        = number
  description = "PITR history retention in seconds. Free plan maximum is 21600 (6 hours); paid default is 86400."
  default     = 21600
}

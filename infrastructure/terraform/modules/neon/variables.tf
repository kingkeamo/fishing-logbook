variable "environment" {
  type        = string
  description = "Deployment environment name (for example: dev or prod)."
}

variable "region" {
  type        = string
  description = "Neon region identifier for the project."
  default     = ""
}

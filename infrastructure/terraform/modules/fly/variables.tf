variable "environment" {
  type        = string
  description = "Deployment environment name (for example: dev or prod)."
}

variable "region" {
  type        = string
  description = "Primary Fly.io region for the API application."
  default     = ""
}

variable "vm_size" {
  type        = string
  description = "Fly.io machine size. Start with the smallest practical size."
  default     = "shared-cpu-1x"
}

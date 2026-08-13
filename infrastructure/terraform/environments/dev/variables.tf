variable "environment" {
  type        = string
  description = "Deployment environment name."
  default     = "dev"
}

variable "cognito_callback_urls" {
  type        = list(string)
  description = "Allowed OAuth callback URLs for the PWA app client."
  default     = []
}

variable "cognito_logout_urls" {
  type        = list(string)
  description = "Allowed sign-out redirect URLs for the PWA app client."
  default     = []
}

variable "neon_region" {
  type        = string
  description = "Neon region identifier."
  default     = ""
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

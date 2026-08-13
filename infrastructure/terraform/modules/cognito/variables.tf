variable "environment" {
  type        = string
  description = "Deployment environment name (for example: dev or prod)."
}

variable "callback_urls" {
  type        = list(string)
  description = "Allowed OAuth callback URLs for the PWA app client."
  default     = []
}

variable "logout_urls" {
  type        = list(string)
  description = "Allowed sign-out redirect URLs for the PWA app client."
  default     = []
}

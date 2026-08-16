variable "environment" {
  type        = string
  description = "Deployment environment name (for example: dev or prod)."
}

variable "region" {
  type        = string
  description = "AWS region that hosts the user pool (used to build issuer and hosted-UI URLs)."
}

variable "api_resource_identifier" {
  type        = string
  description = "Cognito resource-server identifier and RFC 8707 resource/aud URI. Must be an https URL so custom scopes can be requested with resource binding."
}

variable "callback_urls" {
  type        = list(string)
  description = "Exact OAuth callback URLs for the PWA app client. No wildcards."

  validation {
    condition     = length(var.callback_urls) > 0
    error_message = "At least one callback URL is required when OAuth is enabled."
  }

  validation {
    condition = alltrue([
      for url in var.callback_urls : !strcontains(url, "*") && (
        startswith(url, "https://") ||
        startswith(url, "http://localhost:") ||
        startswith(url, "http://localhost/") ||
        startswith(url, "http://127.0.0.1:") ||
        startswith(url, "http://127.0.0.1/")
      )
    ])
    error_message = "Callback URLs must be exact (no wildcards). HTTPS is required except for localhost HTTP."
  }
}

variable "logout_urls" {
  type        = list(string)
  description = "Exact sign-out redirect URLs for the PWA app client. No wildcards."

  validation {
    condition     = length(var.logout_urls) > 0
    error_message = "At least one logout URL is required when OAuth is enabled."
  }

  validation {
    condition = alltrue([
      for url in var.logout_urls : !strcontains(url, "*") && (
        startswith(url, "https://") ||
        startswith(url, "http://localhost:") ||
        startswith(url, "http://localhost/") ||
        startswith(url, "http://127.0.0.1:") ||
        startswith(url, "http://127.0.0.1/")
      )
    ])
    error_message = "Logout URLs must be exact (no wildcards). HTTPS is required except for localhost HTTP."
  }
}

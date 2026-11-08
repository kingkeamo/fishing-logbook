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

variable "enable_native_client" {
  type        = bool
  description = "Whether to create the native public app client for this environment."
}

variable "native_callback_urls" {
  type        = list(string)
  description = "Exact OAuth callback URLs for the native public app client. HTTPS app/universal links or reverse-domain private-use schemes only; no wildcards, query strings, or fragments."

  validation {
    condition     = !var.enable_native_client || length(var.native_callback_urls) > 0
    error_message = "At least one native callback URL is required when the native client is enabled."
  }

  validation {
    condition = alltrue([
      for url in var.native_callback_urls :
      !strcontains(url, "*") &&
      !strcontains(url, "?") &&
      !strcontains(url, "#") &&
      (
        startswith(url, "https://") ||
        can(regex("^[a-z][a-z0-9-]*(\\.[a-z0-9-]+){2,}://[a-z0-9][a-z0-9.-]*$", lower(url)))
      )
    ])
    error_message = "Native callback URLs must be exact HTTPS URLs or reverse-domain private-use scheme URLs without wildcards, query strings, or fragments."
  }
}

variable "native_logout_urls" {
  type        = list(string)
  description = "Exact sign-out redirect URLs for the native public app client. HTTPS app/universal links or reverse-domain private-use schemes only; no wildcards, query strings, or fragments."

  validation {
    condition     = !var.enable_native_client || length(var.native_logout_urls) > 0
    error_message = "At least one native logout URL is required when the native client is enabled."
  }

  validation {
    condition = alltrue([
      for url in var.native_logout_urls :
      !strcontains(url, "*") &&
      !strcontains(url, "?") &&
      !strcontains(url, "#") &&
      (
        startswith(url, "https://") ||
        can(regex("^[a-z][a-z0-9-]*(\\.[a-z0-9-]+){2,}://[a-z0-9][a-z0-9.-]*$", lower(url)))
      )
    ])
    error_message = "Native logout URLs must be exact HTTPS URLs or reverse-domain private-use scheme URLs without wildcards, query strings, or fragments."
  }
}

variable "google_client_id" {
  type        = string
  description = "Google OAuth 2.0 web client ID. Empty disables Continue with Google. Not a secret, but account-specific: supply locally."
  default     = ""
}

variable "google_client_secret" {
  type        = string
  description = "Google OAuth 2.0 web client secret. Supply through TF_VAR_google_client_secret; never commit."
  default     = ""
  sensitive   = true

  validation {
    condition     = var.google_client_id == "" || var.google_client_secret != ""
    error_message = "google_client_secret is required when google_client_id is set."
  }
}

variable "apple_services_id" {
  type        = string
  description = "Sign in with Apple Services ID (the OAuth client ID). Empty disables Continue with Apple. Supply locally."
  default     = ""
}

variable "apple_team_id" {
  type        = string
  description = "Apple Developer Team ID. Required when apple_services_id is set."
  default     = ""
}

variable "apple_key_id" {
  type        = string
  description = "Key ID of the Sign in with Apple private key. Required when apple_services_id is set."
  default     = ""
}

variable "apple_private_key" {
  type        = string
  description = "Contents of the Sign in with Apple .p8 private key. Supply through TF_VAR_apple_private_key; never commit."
  default     = ""
  sensitive   = true

  validation {
    condition = var.apple_services_id == "" || (
      var.apple_team_id != "" && var.apple_key_id != "" && var.apple_private_key != ""
    )
    error_message = "apple_team_id, apple_key_id and apple_private_key are required when apple_services_id is set."
  }
}

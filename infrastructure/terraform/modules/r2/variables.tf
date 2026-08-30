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

variable "cors_allowed_origins" {
  type        = list(string)
  description = "Browser origins allowed to access objects directly against this bucket via presigned URLs. Empty list disables CORS."
  default     = []
}

variable "cors_allowed_methods" {
  type        = list(string)
  description = "HTTP methods allowed by the CORS rule. Only used when cors_allowed_origins is non-empty."
  default     = ["GET", "PUT", "HEAD"]
}

variable "cors_allowed_headers" {
  type        = list(string)
  description = "Request headers allowed by the CORS rule. Only used when cors_allowed_origins is non-empty."
  default     = ["*"]
}

variable "object_expiration_days" {
  type        = number
  description = "Days after which every object in the bucket is automatically deleted. Null disables expiration."
  default     = null
}

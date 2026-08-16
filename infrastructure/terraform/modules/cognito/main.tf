# Amazon Cognito user pool, resource server, public PWA app client, hosted-UI domain,
# and managed-login branding for one environment.
#
# The PWA is a public browser client: generate_secret is false. OAuth is Authorization
# Code only (no implicit, no client_credentials). PKCE S256 is enforced by the Blazor
# OIDC library against this public client.
#
# Destroying the user pool permanently deletes registered users. prevent_destroy and
# deletion_protection are both set.

locals {
  resource_prefix = "fishing-logbook-${var.environment}"
  api_identifier  = var.api_resource_identifier
  api_scope_name  = "access"
}

resource "aws_cognito_user_pool" "this" {
  name = "${local.resource_prefix}-users"

  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]
  mfa_configuration        = "OFF"
  deletion_protection      = "ACTIVE"
  user_pool_tier           = "ESSENTIALS"

  username_configuration {
    case_sensitive = false
  }

  account_recovery_setting {
    recovery_mechanism {
      name     = "verified_email"
      priority = 1
    }
  }

  admin_create_user_config {
    allow_admin_create_user_only = false
  }

  email_configuration {
    email_sending_account = "COGNITO_DEFAULT"
  }

  password_policy {
    minimum_length                   = 12
    require_lowercase                = true
    require_numbers                  = true
    require_uppercase                = true
    require_symbols                  = false
    temporary_password_validity_days = 7
  }

  user_attribute_update_settings {
    attributes_require_verification_before_update = ["email"]
  }

  verification_message_template {
    default_email_option = "CONFIRM_WITH_CODE"
  }

  tags = {
    Environment = var.environment
    Name        = "${local.resource_prefix}-users"
  }

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_cognito_resource_server" "api" {
  identifier   = local.api_identifier
  name         = "${local.resource_prefix}-api"
  user_pool_id = aws_cognito_user_pool.this.id

  scope {
    scope_name        = local.api_scope_name
    scope_description = "Access the FishingLogBook API"
  }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_cognito_user_pool_client" "pwa" {
  name         = "${local.resource_prefix}-pwa"
  user_pool_id = aws_cognito_user_pool.this.id

  generate_secret                      = false
  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_flows                  = ["code"]
  allowed_oauth_scopes = [
    "openid",
    "email",
    "profile",
    "${local.api_identifier}/${local.api_scope_name}",
  ]
  supported_identity_providers = ["COGNITO"]
  callback_urls                = var.callback_urls
  logout_urls                  = var.logout_urls

  prevent_user_existence_errors = "ENABLED"
  enable_token_revocation       = true
  auth_session_validity         = 3

  access_token_validity  = 1
  id_token_validity      = 1
  refresh_token_validity = 30

  token_validity_units {
    access_token  = "hours"
    id_token      = "hours"
    refresh_token = "days"
  }

  refresh_token_rotation {
    feature                    = "ENABLED"
    retry_grace_period_seconds = 5
  }

  explicit_auth_flows = []

  depends_on = [aws_cognito_resource_server.api]
}

resource "aws_cognito_user_pool_domain" "this" {
  domain                = local.resource_prefix
  user_pool_id          = aws_cognito_user_pool.this.id
  managed_login_version = 2
}

resource "aws_cognito_managed_login_branding" "pwa" {
  user_pool_id                = aws_cognito_user_pool.this.id
  client_id                   = aws_cognito_user_pool_client.pwa.id
  use_cognito_provided_values = true
}

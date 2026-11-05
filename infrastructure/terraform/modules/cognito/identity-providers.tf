# Federated identity providers. Cognito stays the only authentication authority: Google
# and Apple users get their own Cognito user (and therefore their own sub) and the API
# keeps mapping Cognito sub -> FishingLogBook UserId. No pre sign-up trigger links accounts
# by email, so a federated identity is never silently merged into an existing account.
#
# email_verified is mapped explicitly. The Pre Token Generation Lambda only puts an email
# on the access token when email_verified is true, and the API rejects tokens without one.
# Apple private-relay addresses are delivered as verified emails.
#
# Provider secrets are sensitive variables supplied through TF_VAR_* environment variables.

locals {
  google_enabled = var.google_client_id != ""
  apple_enabled  = var.apple_services_id != ""

  federated_identity_providers = concat(
    local.google_enabled ? ["Google"] : [],
    local.apple_enabled ? ["SignInWithApple"] : [],
  )
  supported_identity_providers = concat(["COGNITO"], local.federated_identity_providers)
}

resource "aws_cognito_identity_provider" "google" {
  count = local.google_enabled ? 1 : 0

  user_pool_id  = aws_cognito_user_pool.this.id
  provider_name = "Google"
  provider_type = "Google"

  provider_details = {
    client_id        = var.google_client_id
    client_secret    = var.google_client_secret
    authorize_scopes = "openid email profile"
  }

  attribute_mapping = {
    email          = "email"
    email_verified = "email_verified"
    username       = "sub"
  }
}

resource "aws_cognito_identity_provider" "apple" {
  count = local.apple_enabled ? 1 : 0

  user_pool_id  = aws_cognito_user_pool.this.id
  provider_name = "SignInWithApple"
  provider_type = "SignInWithApple"

  provider_details = {
    client_id        = var.apple_services_id
    team_id          = var.apple_team_id
    key_id           = var.apple_key_id
    private_key      = var.apple_private_key
    authorize_scopes = "email name"
  }

  attribute_mapping = {
    email          = "email"
    email_verified = "email_verified"
    username       = "sub"
  }
}

# Amazon Cognito module (skeleton).
#
# This module will define the FishingLogBook Cognito user pool and app client for a
# single environment. It intentionally declares NO resources yet: infrastructure is
# only created deliberately and manually (see infrastructure/README.md).
#
# Planned resources (added only when explicitly approved):
#   - aws_cognito_user_pool
#   - aws_cognito_user_pool_client (Authorization Code + PKCE, no client secret)
#   - aws_cognito_user_pool_domain
#
# The PWA must use Authorization Code flow with PKCE and must NOT use a client secret.

locals {
  resource_prefix = "fishing-logbook-${var.environment}"
}

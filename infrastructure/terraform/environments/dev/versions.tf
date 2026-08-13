terraform {
  # >= 1.10 is required for native S3 backend state locking (use_lockfile).
  required_version = ">= 1.10.0"

  required_providers {
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 5.23"
    }
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.59"
    }
    neon = {
      source  = "kislerdm/neon"
      version = "~> 0.15"
    }
  }
}

# Cloudflare Pages + R2. The API token is read from the CLOUDFLARE_API_TOKEN
# environment variable; never commit tokens or account identifiers.
provider "cloudflare" {}

# Amazon Cognito only. Credentials come from the standard AWS environment variables
# (or a named profile); never commit credentials.
provider "aws" {
  region = var.aws_region
}

# Neon PostgreSQL. The API key is read from the NEON_API_KEY environment variable.
provider "neon" {}

# NOTE: Fly.io is intentionally NOT managed by a Terraform provider. The official
# provider is archived/unmaintained and the community alternative is immature, so Fly
# apps are created and deployed with flyctl (see infrastructure/README.md). The `fly`
# module currently only computes naming and declares no provider-backed resources.

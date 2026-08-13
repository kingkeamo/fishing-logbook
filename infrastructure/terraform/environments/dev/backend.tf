terraform {
  # Terraform state is stored in Cloudflare R2 (S3-compatible) with native lockfile
  # locking (use_lockfile), so no AWS DynamoDB lock table is required.
  #
  # Account-specific values (bucket + endpoints) are NOT committed. Initialise with:
  #   terraform init -backend-config=backend.hcl
  #
  # The R2 access key id/secret are supplied as AWS credentials via environment
  # variables (AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY).
  backend "s3" {
    key          = "dev/terraform.tfstate"
    region       = "auto"
    use_lockfile = true

    # R2 is S3-compatible but not AWS; skip AWS-specific behaviours.
    skip_credentials_validation = true
    skip_region_validation      = true
    skip_requesting_account_id  = true
    skip_metadata_api_check     = true
    skip_s3_checksums           = true
  }
}

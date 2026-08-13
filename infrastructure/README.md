# FishingLogBook Infrastructure

Infrastructure for FishingLogBook is defined with **Terraform** and is applied
**manually only**. This directory currently contains a **skeleton**: module and
environment structure with variables and outputs, but **no cloud resources are defined
yet**. Resources are added deliberately, one at a time, only when explicitly approved.

## ⚠️ Cost and safety warning

```text
Terraform is intentionally manual.

Never run terraform apply without reviewing the complete plan.

Never add terraform apply or terraform destroy to GitHub Actions.

Cloud resources may incur charges immediately after creation.
```

GitHub Actions is permitted to run **validation only**:

```text
terraform fmt -check
terraform init -backend=false
terraform validate
```

GitHub Actions must **never** run:

```text
terraform apply
terraform destroy
terraform import
```

No CI/CD workflow may create, resize, modify or destroy cloud infrastructure.

## Layout

```text
infrastructure/terraform/
├── modules/
│   ├── cognito/            # Amazon Cognito user pool + app client (PKCE, no secret)
│   ├── neon/               # Neon PostgreSQL project/branch/database
│   ├── fly/                # Fly.io API application + machine
│   ├── r2/                 # Cloudflare R2 bucket for catch photographs
│   └── cloudflare-pages/   # Cloudflare Pages project for the PWA
└── environments/
    ├── dev/
    └── prod/
```

Development and Production are separate environment directories with separate state.
They must not share operational databases or object-storage buckets.

## Providers and credentials

Provider credentials (AWS, Neon, Fly.io, Cloudflare) must be supplied through
environment variables or your local provider configuration. **Never commit credentials,
connection strings, or real resource identifiers** to this repository. Each environment
provides a `terraform.tfvars.example`; copy it to `terraform.tfvars` (gitignored) and
fill in non-sensitive values locally.

Some providers require manual account setup before Terraform can manage resources (for
example: an AWS account/region, a Neon account, a Fly.io organisation, and a Cloudflare
account with R2 enabled). Document any such manual prerequisites here as resources are
added.

## Manual deployment process

Development:

```text
cd infrastructure/terraform/environments/dev

terraform init
terraform fmt
terraform validate
terraform plan -out=dev.tfplan
```

Review the **entire** plan. Only then, deliberately:

```text
terraform apply dev.tfplan
```

Production follows the equivalent process from `environments/prod` and must be performed
with extra care. There is intentionally **no** one-command script that plans and applies
without review.

## State

Terraform state files, `.terraform/` directories, and `terraform.tfvars` are gitignored
and must never be committed. For the earliest local prototype, local state is acceptable
if handled carefully. Before multiple developers manage shared or Production
infrastructure, configure an appropriate remote, encrypted state backend.

## Cost alerts

Where supported, configure billing alerts for AWS, Fly.io, Neon, and Cloudflare. Billing
alerts are preferable to relying solely on provider free tiers — a free tier does not
guarantee zero cost. Terraform must not automatically upgrade resources after thresholds
are reached.

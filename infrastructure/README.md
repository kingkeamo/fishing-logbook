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
├── adding-resources-to-terraform.md   # how to add resources safely
├── modules/
│   ├── cognito/            # Amazon Cognito user pool + app client (PKCE, no secret)
│   ├── neon/               # Neon PostgreSQL project/branch/database
│   ├── fly/                # Fly.io API app (naming only; deployed via flyctl, not TF)
│   ├── r2/                 # Cloudflare R2 bucket for catch photographs
│   └── cloudflare-pages/   # Cloudflare Pages project for the PWA
└── environments/
    ├── dev/
    │   ├── main.tf              # module calls
    │   ├── versions.tf         # required_version + pinned providers + provider blocks
    │   ├── backend.tf          # Cloudflare R2 (S3-compatible) remote state
    │   ├── variables.tf
    │   ├── outputs.tf
    │   ├── backend.hcl.example       # copy to backend.hcl (gitignored)
    │   └── terraform.tfvars.example  # copy to terraform.tfvars (gitignored)
    └── prod/                    # same layout as dev
```

Development and Production are separate environment directories with separate state.
They must not share operational databases or object-storage buckets.

## Providers and credentials

Provider versions are **pinned centrally** in each environment's `versions.tf`
(`required_providers`), and `.terraform.lock.hcl` is committed to lock exact versions and
hashes. Update providers deliberately with `terraform init -upgrade` locally (never in CI),
then review the plan.

Generate the lock file with hashes for **every platform** the repo is used on (the Linux
CI runner plus local dev machines), otherwise CI will fail with missing-hash errors. Run
this once per environment (from `environments/dev` and `environments/prod`):

```powershell
terraform providers lock `
  -platform=linux_amd64 `
  -platform=windows_amd64 `
  -platform=darwin_arm64 `
  -platform=darwin_amd64
```

Commit the resulting `.terraform.lock.hcl` in each environment directory.

| Concern | Provider | Credentials (env var) |
|---|---|---|
| Cloudflare Pages + R2 | `cloudflare/cloudflare` | `CLOUDFLARE_API_TOKEN` |
| Amazon Cognito | `hashicorp/aws` | standard AWS credentials / profile |
| Neon PostgreSQL | `kislerdm/neon` | `NEON_API_KEY` |
| Fly.io API | **flyctl, not Terraform** | `flyctl auth` / `FLY_API_TOKEN` |

**Fly.io is intentionally not managed by Terraform.** The official Fly provider is
archived/unmaintained and the community alternative is immature, so Fly apps are created
and deployed with `flyctl` (GitHub Actions may deploy to an *already-existing* app; it must
never create/resize/destroy Fly infrastructure). The `fly` module only computes naming.

Provider credentials must be supplied through environment variables or your local provider
configuration. **Never commit credentials, connection strings, or real resource
identifiers.** Each environment provides a `terraform.tfvars.example`; copy it to
`terraform.tfvars` (gitignored) and fill in non-sensitive values locally.

Some providers require manual account setup before Terraform can manage resources (an AWS
account/region, a Neon account, a Fly.io organisation, and a Cloudflare account with R2
enabled). Document such manual prerequisites here as resources are added.

## Manual deployment process

Development:

```text
cd infrastructure/terraform/environments/dev

# One-time: copy backend.hcl.example to backend.hcl and fill in your R2 bucket/endpoint.
# Provide R2 credentials as AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY first.
terraform init -backend-config=backend.hcl
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

Remote state is stored in **Cloudflare R2** (an S3-compatible bucket) using Terraform's
`s3` backend with **native lockfile locking** (`use_lockfile = true`) — so no AWS DynamoDB
lock table is needed. This keeps state on a provider we already use, at effectively zero
cost (R2 has no egress fees).

- Non-secret backend settings live in each environment's `backend.tf`.
- Account-specific values (`bucket`, `endpoints`) live in a gitignored `backend.hcl`
  (copy from `backend.hcl.example`) and are passed via
  `terraform init -backend-config=backend.hcl`.
- The R2 access key id/secret are provided as `AWS_ACCESS_KEY_ID` /
  `AWS_SECRET_ACCESS_KEY` environment variables.
- Create the state bucket **once, manually**, before the first `init`. It must be private
  and separate from the catch-photo buckets. Dev and Prod use separate state (separate
  keys, and Prod a separate bucket/account).

State files, `.terraform/` directories, `terraform.tfvars`, and `backend.hcl` are
gitignored and must never be committed. `.terraform.lock.hcl` **is** committed.

## Cost alerts

Where supported, configure billing alerts for AWS, Fly.io, Neon, and Cloudflare. Billing
alerts are preferable to relying solely on provider free tiers — a free tier does not
guarantee zero cost. Terraform must not automatically upgrade resources after thresholds
are reached.

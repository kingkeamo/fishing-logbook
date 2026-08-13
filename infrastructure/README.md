# FishingLogBook Infrastructure

Infrastructure for FishingLogBook is defined with **Terraform** and is applied
**manually only**. Neon (`neon_project`), R2 photos (`cloudflare_r2_bucket`), and
Pages (`cloudflare_pages_project`) are defined. Cognito and Fly remain skeletons.
Further resources are added deliberately, one at a time, only when explicitly approved.

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
infrastructure/
├── README.md
├── fly/                               # Fly.io API apps (flyctl, not Terraform)
│   ├── README.md
│   ├── fly.dev.toml
│   └── fly.prod.toml
└── terraform/
    ├── adding-resources-to-terraform.md
    ├── modules/                       # neon, r2 (photos), pages; cognito/fly naming only
    └── environments/
        ├── dev/
        └── prod/
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

## Neon (Dev)

The Dev API already uses a Console-created Neon project (`neondb`). Terraform must
**import** that project. Do not `apply` a create — that would provision a second
database.

1. Create a Neon API key (Account Settings → API Keys) and set `NEON_API_KEY`.
2. Copy `environments/dev/terraform.tfvars.example` to `terraform.tfvars` and fill in
   values that **match the existing project** (organisation ID, region, Postgres
   version). Find them in the Neon Console; do not guess. Organisation ID is under
   Account Settings → Organization.
3. `terraform init` (R2 backend if `backend.hcl` is ready; otherwise `-backend=false`
   is acceptable only for validate — import/apply need state).
4. Import, then plan. **Abort if the plan shows destroy or replace.**

```powershell
cd infrastructure/terraform/environments/dev
# Project ID is on the Neon project dashboard (not committed).
terraform import module.neon.neon_project.this "<neon-project-id>"
terraform plan -out=dev.tfplan
```

A clean plan may rename the project in-place to `fishing-logbook-dev`. That is expected.
Do not apply if region or Postgres version would force a new project. Free-plan projects
must use `history_retention_seconds = 21600` (6 hours); the paid default of 86400 is
rejected by the API.

`lifecycle.prevent_destroy` blocks deletion, but still never apply a replace. The
database password stays in Fly secrets (`ConnectionStrings__Postgres`); do not copy
Terraform outputs into git or CI logs.

Do not apply `environments/prod` until you are ready to create a **separate** Neon
project. Prod must never point at the Dev database.

## Cloudflare R2 (photos) and Pages

These are **not** the Terraform state bucket. Photo storage is `fishing-logbook-dev`
(private). State stays in a separate, manually created bucket (`backend.hcl`).

**R2** is S3-compatible object storage for catch photos. The bucket is private; the PWA
must never get R2 keys. Uploads will use short-lived API-issued URLs later.

**Pages** hosts the Blazor WASM PWA as static files. Terraform creates a **direct-upload**
project (no Git build). The site stays empty until a later `wrangler pages deploy` /
`deploy-web.yml`. API URLs are baked into `wwwroot/appsettings` at publish time.

1. Create an API token (My Profile → API Tokens) with **Workers R2 Storage:Edit** and
   **Cloudflare Pages:Edit**. Set `CLOUDFLARE_API_TOKEN`.
2. Copy the account ID from the Cloudflare dashboard sidebar into `terraform.tfvars`
   as `cloudflare_account_id` (never commit that file).
3. Plan in `environments/dev`. Expect **2 to add** (bucket + Pages project). Neon should
   show no change. Abort if anything shows destroy or replace.

```powershell
$env:CLOUDFLARE_API_TOKEN = "<token>"
cd C:\git\fishing-logbook\infrastructure\terraform\environments\dev
terraform plan -out dev.tfplan
terraform apply dev.tfplan
```

Do not apply prod until you want a separate bucket and Pages project.

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

# Terraform & Infrastructure Conventions

These rules always apply. Do not wait until a `.tf` file is open.


## Manual apply only

Terraform is applied **manually only**, after reviewing the complete `terraform plan`.

Permitted without explicit developer approval:

- `terraform fmt`
- `terraform validate`
- `terraform init -backend=false`
- `terraform plan`

Never run without explicit developer instruction:

- `terraform apply`
- `terraform destroy`
- `terraform import`

Do not:

- create cloud infrastructure automatically
- resize Fly.io machines
- create additional databases
- create paid provider resources
- enable autoscaling
- provision load balancers
- change production infrastructure

GitHub Actions may deploy application code to infrastructure that already exists.
GitHub Actions must **never** apply Terraform.

If new infrastructure is required for a feature, stop and report:

1. What resource is required.
2. Why it is required.
3. Expected recurring cost where known.
4. Required Terraform changes.
5. Required manual deployment steps.

Never attempt to solve an application problem by provisioning additional infrastructure without explicit approval.

## Golden rules

- Terraform is applied **manually only**, after reviewing the complete `terraform plan`.
- CI/GitHub Actions may run **validation only**: `terraform fmt -check`,
  `terraform init -backend=false`, `terraform validate`. It must **never** run
  `apply`, `destroy`, or `import`, and must never create/modify/destroy cloud resources.
- **Never invent** credentials, account IDs, connection strings, or real resource
  identifiers. Account-specific values are supplied locally; report what is missing
  rather than guessing.
- Add resources **deliberately, one at a time, only when explicitly approved.** Neon
  currently defines `neon_project`; R2 defines `cloudflare_r2_bucket` (photos); Pages
  defines `cloudflare_pages_project`; Grafana Cloud looks up an existing stack and
  defines a Loki `logs:write` access policy. Cognito defines the user pool, public PWA
  app client, hosted-UI domain, and API resource server. Fly remains a skeleton.

## Stack (who does what)

| Concern | Provider / tool | Notes |
|---|---|---|
| PWA hosting | Cloudflare Pages (`cloudflare/cloudflare`) | one project per env |
| Photo storage | Cloudflare R2 (`cloudflare/cloudflare`) | private buckets; zero-egress |
| Database | Neon PostgreSQL (`kislerdm/neon`) | separate DB per env |
| Auth | Amazon Cognito (`hashicorp/aws`) | the only reason we use AWS |
| Diagnostic logs | Grafana Cloud (`grafana/grafana`) | lookup existing free stack; Loki write token only |
| API hosting | **Fly.io — via `flyctl`, NOT Terraform** | no stable TF provider |

Fly.io is intentionally **not** managed by a Terraform provider (the official provider is
archived; the community one is immature). Fly apps are created/deployed with `flyctl`. The
`fly` module only computes naming.

## Provider versions

- Pinned centrally in each environment's `versions.tf` (`required_providers`). This is the
  Terraform equivalent of the solution's Central Package Management.
- `.terraform.lock.hcl` is **committed** and pins exact provider versions + hashes. Only
  update it deliberately via `terraform init -upgrade` (or `terraform providers lock`),
  followed by a plan review — never `-upgrade` in CI.
- `required_version >= 1.10.0` (needed for native S3 backend locking).

## State backend

- Remote state lives in **Cloudflare R2** (S3-compatible `backend "s3"`) with native
  lockfile locking (`use_lockfile = true`) — no AWS DynamoDB lock table.
- Account-specific settings (`bucket`, `endpoints`) come from a gitignored `backend.hcl`
  (see `backend.hcl.example`); the rest lives in `backend.tf`.
- R2 access key/secret are passed as `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` env
  vars. Initialise with `terraform init -backend-config=backend.hcl`.
- Dev and prod use **separate state** (separate keys, and prod a separate bucket/account).
  Never point dev at prod state or vice versa.

## Layout

```text
infrastructure/terraform/
├── modules/<name>/{main,variables,outputs}.tf   # one concern per module
└── environments/{dev,prod}/
    ├── main.tf            # module calls only
    ├── versions.tf        # required_version + required_providers + provider blocks
    ├── backend.tf         # R2 (s3) backend, non-secret settings
    ├── variables.tf
    ├── outputs.tf
    ├── backend.hcl.example
    └── terraform.tfvars.example
```

## Naming & tagging

- Every module defines `locals { resource_prefix = "fishing-logbook-${var.environment}" }`
  and derives resource names from it (append a component suffix where needed, e.g. the Fly
  app name is `"${local.resource_prefix}-api"`).
- Define all variables in `variables.tf`; group related resources in one file.
- Where a provider supports tags, set at least `Environment = var.environment`.

## Secrets

- Secrets never live in `.tf` files, `.tfvars`, or the repo. Supply them via provider
  environment variables (`CLOUDFLARE_API_TOKEN`, `NEON_API_KEY`, AWS credentials) or,
  for app secrets, the relevant secret store — never Terraform variables committed to git.
- `terraform.tfvars` and `backend.hcl` are gitignored; only the `*.example` files are
  committed.

## Data-bearing / high-risk resources

When adding resources that hold data or are hard to recreate, add a safeguard and explain
the impact before proposing the change:

| Resource | Risk | Safeguard |
|---|---|---|
| `neon_project` / database | Data loss | `lifecycle { prevent_destroy = true }` |
| `cloudflare_r2_bucket` (photos) | Content loss | `prevent_destroy`; never publicly writable |
| `aws_cognito_user_pool` | User data loss | `prevent_destroy`; pools can't be restored |

Use `lifecycle { ignore_changes = [...] }` only with a clear reason (value managed
elsewhere, imported resource, provider mutates it).

## Never suggest

- Running `terraform apply` / `destroy` / `import` from CI or automatically.
- Committing credentials, account IDs, R2 endpoints, or real resource IDs.
- Adding a Fly.io Terraform provider without an explicit decision to adopt one.
- Hardcoding environment-specific values that belong in `tfvars` / `backend.hcl`.

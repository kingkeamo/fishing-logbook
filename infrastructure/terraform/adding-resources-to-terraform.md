# Adding Resources to Terraform

All Terraform lives under `infrastructure/terraform/`. Cognito and Fly are still
**skeletons**. Neon (`neon_project`), R2 (`cloudflare_r2_bucket` photos), and Pages
(`cloudflare_pages_project`) are defined. Add further resources deliberately, one at a
time, and only when explicitly approved. Read `.claude/rules/terraform.md` first.

## Step 1 — Pick the module

| Concern | Module | Provider |
|---|---|---|
| PWA hosting | `modules/cloudflare-pages` | `cloudflare/cloudflare` |
| Photo storage | `modules/r2` | `cloudflare/cloudflare` |
| Database | `modules/neon` | `kislerdm/neon` |
| Diagnostic logs | `modules/grafana-cloud` | `grafana/grafana` |
| Auth | `modules/cognito` | `hashicorp/aws` |
| API hosting | `modules/fly` | **flyctl, not Terraform** |

If nothing fits, create `modules/<name>/{main,variables,outputs}.tf`.

> Fly.io is not managed by a Terraform provider (see the rule). Don't add `fly_*`
> resources without an explicit decision to adopt a provider.

## Step 2 — Add the resource to the module

- Define every input in `variables.tf`; group related resources in one `.tf` file.
- If the provider is not `hashicorp/*`, the module must declare `required_providers`
  with that `source` (otherwise Terraform looks for `hashicorp/<name>`). Version
  pins stay in each environment's `versions.tf`.
- Derive names from the existing local:

```hcl
resource "cloudflare_r2_bucket" "photos" {
  account_id = var.cloudflare_account_id
  name       = local.resource_prefix # -> fishing-logbook-dev
}
```

- Expose anything the environment or other modules need via `outputs.tf`.
- For data-bearing resources (Neon DB, R2 bucket, Cognito pool) add
  `lifecycle { prevent_destroy = true }` and explain the impact.

## Step 3 — Register a new provider (only if needed)

If the resource needs a provider not already pinned:

1. Add it to `environments/dev/versions.tf` and `environments/prod/versions.tf` under
   `required_providers` with a version constraint (look up the real current version;
   never invent one).
2. Add the matching `provider "<name>" {}` block (credentials via env vars).
3. Run `terraform init -upgrade` locally and commit the updated `.terraform.lock.hcl`.

## Step 4 — Wire the module into each environment

Add/extend the module call in `environments/dev/main.tf` and `environments/prod/main.tf`:

```hcl
module "r2" {
  source                = "../../modules/r2"
  environment           = var.environment
  cloudflare_account_id = var.cloudflare_account_id
}
```

## Step 5 — Variables & examples

- Add any new inputs to each environment's `variables.tf`.
- Document non-secret values in `terraform.tfvars.example` (commented).
- Never put secrets or account-specific IDs in committed files — use env vars,
  `terraform.tfvars` (gitignored), or `backend.hcl` (gitignored).

## Step 6 — Validate locally

```powershell
cd infrastructure/terraform/environments/dev
terraform fmt
terraform init -backend=false   # or -backend-config=backend.hcl to use R2 state
terraform validate
terraform plan                  # review the ENTIRE plan before any apply
```

CI runs the `fmt`/`init -backend=false`/`validate` steps automatically. Applying is always
manual.

## Common mistakes

- Adding `fly_*` resources — Fly is deployed via `flyctl`, not Terraform.
- Hardcoding the Cloudflare account ID / R2 endpoint instead of using variables /
  `backend.hcl`.
- Forgetting `prevent_destroy` on data-bearing resources.
- Bumping providers with `terraform init -upgrade` in CI (do it locally, review the plan).
- Committing `terraform.tfvars` or `backend.hcl` (only the `*.example` files are tracked).

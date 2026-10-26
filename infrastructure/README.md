# FishingLogBook Infrastructure

Infrastructure for FishingLogBook is defined with **Terraform** and is applied
**manually only**. Neon (`neon_project`), R2 photos (`cloudflare_r2_bucket`), Pages
(`cloudflare_pages_project`), Grafana Cloud Loki write access, and Cognito (user pool,
public PWA/native clients, hosted-UI domain, API resource server) are defined. Fly remains a
skeleton. Further resources are added deliberately, one at a time, only when explicitly
approved.

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
    ├── modules/                       # neon, r2, pages, grafana-cloud, cognito; fly naming only
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
| Lambda zip helper | `hashicorp/archive` | none |
| Neon PostgreSQL | `kislerdm/neon` | `NEON_API_KEY` |
| Grafana Cloud | `grafana/grafana` | `GRAFANA_CLOUD_ACCESS_POLICY_TOKEN` |
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
account/region, a Neon account, a Fly.io organisation, a Cloudflare account with R2
enabled, and a Grafana Cloud free account with one stack). Document such manual
prerequisites here as resources are added.

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
must never get R2 keys. Uploads use short-lived API-issued URLs. Browser PUT requires
CORS on the bucket for `https://fishing-logbook-dev.pages.dev` and local HTTPS
(`https://localhost:7005`). Set CORS in the Cloudflare dashboard; do not run
`terraform apply` from application work.

**Pages** hosts the Blazor WASM PWA as static files. Terraform creates a **direct-upload**
project (no Git build). GitHub Actions (`.github/workflows/deploy-web.yml`) publishes and
uploads on merge to `main`. Set `CLOUDFLARE_API_TOKEN` (Pages:Edit) and
`CLOUDFLARE_ACCOUNT_ID` on the GitHub `dev` environment. API URLs are baked into
`wwwroot/appsettings` at publish time.

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

## Grafana Cloud (diagnostic logs)

Terraform does **not** create a Grafana Cloud stack. Free signup includes one stack;
creating a second stack can require a paid plan. After you have a stack, Terraform
creates a Loki `logs:write` access policy and token for the API.

1. Sign up at https://grafana.com/auth/sign-up/create-user (Free forever plan).
2. Open the stack. The URL is `https://<slug>.grafana.net` — that `<slug>` is
   `grafana_cloud_stack_slug`.
3. Create the Terraform **management** token on the **Cloud Portal**, not inside
   `https://<slug>.grafana.net`.
   - Open **https://grafana.com** (the account site). Do not use Administration →
     Users / Teams / Service accounts inside the Grafana app — those tokens cannot
     read stacks and Terraform returns `403 Forbidden`.
   - Open your organisation → **Security** → **Access Policies**.
   - **Create access policy**. Realm must be the **organisation** (all stacks), not
     only one stack.
   - Scopes:
     - `stacks:read`
     - `accesspolicies:read`
     - `accesspolicies:write`
     - `accesspolicies:delete`
   - Add a token. Set `GRAFANA_CLOUD_ACCESS_POLICY_TOKEN` in the **same** PowerShell
     window you will run `terraform plan` from. Do not paste it into git or chat.
     Close and reopen that window after changing the token, then set the env var
     again.
4. Put the stack slug in gitignored `terraform.tfvars` (`grafana_cloud_stack_slug`).
5. Plan in `environments/dev`. Expect **2 to add** (access policy + token). Abort if
   anything shows a new stack, destroy, or replace of Neon/R2/Pages.

```powershell
$env:GRAFANA_CLOUD_ACCESS_POLICY_TOKEN = "<management-token>"
cd C:\git\fishing-logbook\infrastructure\terraform\environments\dev
terraform plan -out dev.tfplan
```

After a reviewed apply, copy outputs into local user-secrets and Fly (never commit):

```powershell
terraform output -raw grafana_loki_push_url
terraform output -raw grafana_loki_user
terraform output -raw grafana_loki_write_token
```

```powershell
dotnet user-secrets set "ExternalLogging:Provider" "GrafanaCloud" --project src/FishingLogBook.Api
dotnet user-secrets set "ExternalLogging:Url" "<grafana_loki_push_url>" --project src/FishingLogBook.Api
dotnet user-secrets set "ExternalLogging:User" "<grafana_loki_user>" --project src/FishingLogBook.Api
dotnet user-secrets set "ExternalLogging:ApiToken" "<grafana_loki_write_token>" --project src/FishingLogBook.Api
```

Do not apply Grafana in prod until you want a separate Loki write token for production.

## Amazon Cognito (authentication)

Cognito is the only AWS application service this project uses. Terraform owns the user
pool, resource server, separate public PWA and native app clients
(`generate_secret = false`), hosted-UI domain, managed-login branding, and a Pre Token
Generation Lambda (event version V2_0) that copies the verified email user attribute
onto the **access** token. Cognito access tokens omit email by default; ID tokens are
rejected by the API. There is no client secret to output or store. The Lambda does not
log PII.

The PWA uses Authorization Code + PKCE S256 via
`Microsoft.AspNetCore.Components.WebAssembly.Authentication`. FishingLogBook does not
collect Cognito passwords. MFA is off (no SMS). Email is the username; Cognito sends
verification with the default Cognito email quota (no SES).

The native client also permits Authorization Code only. Cognito supports S256 for its
PKCE code exchange; #242 must generate a new verifier/challenge per authorization
request and must not use an implicit flow or an embedded web view. Dev uses exact
reverse-domain private-use redirects derived from the current MAUI application ID:

```text
callback  com.fishinglogbook.mobile.demo://oauth2redirect
logout    com.fishinglogbook.mobile.demo://signedout
```

Private-use schemes work before signing/domain association is available, but another
app can claim the same scheme. The Prod native client is disabled by default so
unrelated PWA-only production plans remain possible. Do not enable it until #238
finalizes the Android package ID, iOS bundle ID, signing identities, and Team ID, and
the site serves:

- Android Digital Asset Links at
  `https://app.catchbutdontforget.com/.well-known/assetlinks.json`
- iOS `apple-app-site-association` at
  `https://app.catchbutdontforget.com/.well-known/apple-app-site-association`

Only then may Prod set `cognito_native_client_enabled = true` and use the exact verified
links in `environments/prod/terraform.tfvars.example`. Do not add both private-use and
verified production redirects as a convenience fallback; keep the registered surface
minimal.

Token settings (explicit):

| Token | Lifetime | Why |
|---|---|---|
| Access | 1 hour | Short-lived API bearer for public PWA/native clients |
| ID | 1 hour | Matches access; used by the OIDC session, not the API |
| Refresh | 30 days | Consumer app should not force daily re-login; rotation limits theft |
| Refresh retry grace | 5 seconds | Enough for a flaky retry; Cognito max is 60 |

Also enabled: token revocation, refresh rotation, `prevent_user_existence_errors`.

Password policy: minimum 12 characters, upper + lower + number required, symbols not
required. Length over extra complexity.

**Do not apply until you have reviewed `terraform plan`.** For #241 against the
existing Dev pool, expect exactly one native `aws_cognito_user_pool_client`, one native
`aws_cognito_managed_login_branding`, and new public outputs. Existing Cognito, Neon,
R2, Pages, and Grafana resources must not be replaced or destroyed. Stop if the plan
changes the PWA client flow, callback/logout URLs, secret setting, resource server,
user pool, Lambda, or token validity unexpectedly.

PWA-only Prod plans keep `cognito_native_client_enabled = false`. A plan that enables
the Prod native client must include the reviewed production callback/logout HTTPS URLs.

After a reviewed apply, copy **public** outputs:

```powershell
terraform output cognito_user_pool_id
terraform output cognito_client_id
terraform output cognito_native_client_id
terraform output -json cognito_approved_client_ids
terraform output cognito_authority
terraform output cognito_hosted_ui_domain
terraform output cognito_api_scope
```

The Web keeps the PWA output as its single `Auth:ClientId`. Put both values from
`cognito_approved_client_ids` into the API's `Auth:ApprovedClientIds` array, preserving
the PWA ID at index 0 and native ID at index 1. The equivalent Fly names are
`Auth__ApprovedClientIds__0` and `Auth__ApprovedClientIds__1`. GitHub `dev` variables
`AUTH_AUTHORITY`, `AUTH_CLIENT_ID`, `AUTH_API_SCOPE`, and `AUTH_API_RESOURCE` remain Web
deployment inputs and `AUTH_CLIENT_ID` remains the PWA ID. Base `appsettings.json` must
not silently represent Dev. Missing or malformed Auth configuration fails API/Web
startup. These are public identifiers; do not mark either client ID as a secret.

Cognito resource-server identifier is the Dev API URL
(`https://fishing-logbook-dev-api.fly.dev`). Custom scopes can only be requested
together with RFC 8707 `resource` when they belong to that identifier. The PWA
sends `resource` = `Auth:ApiResource` (the same URL). Cognito then puts that URL
in access-token `aud`. The API scope is `https://fishing-logbook-dev-api.fly.dev/access`.
The API validates `aud` and requires an exact, case-sensitive `client_id` match against
the environment-specific PWA/native allow-list. An arbitrary app client in the same
user pool cannot call the API. Access tokens do not inherently contain `aud` unless
resource binding is requested.

Create a test user in Cognito Hosted UI (email + verification), then Sign in from the
PWA. Sign-out must clear the OIDC session and send the browser to an allowed logout URL.

### #241 reviewed Dev apply sequence

No command in this sequence is automatic. Stop after the plan until the plan and redirect
choices have been reviewed together.

1. Confirm #242 will register
   `com.fishinglogbook.mobile.demo://oauth2redirect` and
   `com.fishinglogbook.mobile.demo://signedout` on both Dev mobile heads.
2. Use the AWS account/profile and `eu-west-2` region that own the existing Dev user
   pool. Never paste AWS credentials into Terraform variables or repository files.
3. Copy `environments/dev/terraform.tfvars.example` to the gitignored
   `terraform.tfvars`. Keep the documented native URLs exact.
4. Initialize against the existing remote state and create a saved plan:

```powershell
cd C:\git\fishing-logbook\infrastructure\terraform\environments\dev
terraform init -backend-config=backend.hcl
terraform fmt -check -recursive ..\..
terraform validate
terraform plan -out=dev-native-client.tfplan
terraform show dev-native-client.tfplan
```

5. Review the complete plan together. It should add only the native app client and its
   managed-login branding plus outputs. Stop on any destroy, replacement, client secret,
   implicit flow, PWA callback/flow change, or unrelated infrastructure change.
6. Only after explicit approval, apply the saved reviewed plan manually:

```powershell
terraform apply dev-native-client.tfplan
```

7. Read the public outputs and update configuration in a reviewed application change:

```powershell
terraform output -raw cognito_native_client_id
terraform output -json cognito_approved_client_ids
```

   - API local config: `Auth:ApprovedClientIds:0` = PWA output and
     `Auth:ApprovedClientIds:1` = native output.
   - Fly Dev config: `Auth__ApprovedClientIds__0` and
     `Auth__ApprovedClientIds__1`.
   - Native #242 config: authority, hosted-UI domain, native client ID, API resource,
     scope, and the exact Dev redirects. These are public values, not secrets.
   - Web config remains on the PWA client ID.
8. Deploy the API/application configuration separately through the normal reviewed
   deployment path. Terraform does not deploy application code.
9. In #242, prove S256 PKCE in the system browser, then call a protected endpoint with a
   native-client token. Also prove a token from an unlisted pool client returns 401 and
   the existing PWA still signs in.

This work does not run `terraform apply`, alter the live user pool, deploy the API, or
implement the native login/logout/token lifecycle.

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

The complete production provisioning, deployment and recovery sequence is documented in
[`docs/ProductionRunbook.md`](../docs/ProductionRunbook.md). Production deployments are
manual workflow dispatches protected by the GitHub `prod` environment; they never apply
Terraform or run database migrations.

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

Where supported, configure billing alerts for AWS, Fly.io, Neon, Cloudflare, and Grafana Cloud. Billing
alerts are preferable to relying solely on provider free tiers — a free tier does not
guarantee zero cost. Terraform must not automatically upgrade resources after thresholds
are reached.

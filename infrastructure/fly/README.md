# Fly.io API hosting

Fly applications are created and deployed with **flyctl**, not Terraform. CI may deploy
to an existing application, but it must never create, resize, or destroy Fly resources.

| Configuration | Application | Region |
|---|---|---|
| `fly.dev.toml` | `fishing-logbook-dev-api` | See file |
| `fly.prod.toml` | `fishing-logbook-prod-api` | `iad` |

Production uses the low-cost private-alpha configuration: a 256 MB
`shared-cpu-1x`, automatic start/stop, and zero minimum running Machines. Cold starts
are accepted initially. The image uses the repository-root `Dockerfile`; run deployments
from the repository root so the Docker build context includes `src/`.

The complete production sequence, including Terraform, migrations, GitHub configuration,
and acceptance testing, remains in
[`docs/ProductionRunbook.md`](../../docs/ProductionRunbook.md). This document covers the
Fly-specific operations in more detail.

## Prerequisites

```powershell
flyctl auth whoami
flyctl orgs list
```

Use the intended organisation slug explicitly. Never place a Fly access token in a
command, configuration file, or repository document.

## Create the production application once

Creating the application does not deploy a Machine:

```powershell
flyctl apps create fishing-logbook-prod-api --org <organisation-slug>
flyctl status --app fishing-logbook-prod-api
```

The expected initial status has a `fly.dev` hostname and no image.

## Public IP addresses

Use a shared IPv4 address. Do not accidentally allocate a paid dedicated IPv4 address.

```powershell
flyctl ips allocate-v4 --shared --app fishing-logbook-prod-api
flyctl ips allocate-v6 --app fishing-logbook-prod-api
flyctl ips list --app fishing-logbook-prod-api
```

## Production runtime secrets

Create a dedicated R2 Object Read & Write credential scoped only to the production photo
bucket. It must not be the Terraform-state credential. Configure the photo bucket CORS
policy for only `https://app.catchbutdontforget.com` and browser `PUT` requests with the
`Content-Type` header.

Stage secrets before the first deployment so setting each value does not trigger a
release. The Neon Terraform output is a `postgres://` URI suitable for tools such as
`psql`; it is **not** a valid Npgsql connection string and must never be supplied directly
as `ConnectionStrings__Postgres`. Obtain the production role password from Neon's Connect
dialog and construct the Npgsql key/value form in memory:

```powershell
Set-Location infrastructure/terraform/environments/prod
$prodDbHost = terraform output -raw neon_database_host
$prodDbName = terraform output -raw neon_database_name
$prodDbUser = terraform output -raw neon_database_user
$prodDbPassword = Read-Host -MaskInput "Production Neon role password"
$prodDatabase = "Host=$prodDbHost;Port=5432;Database=$prodDbName;Username=$prodDbUser;Password=$prodDbPassword;SSL Mode=Require"
$prodLokiToken = terraform output -raw grafana_loki_write_token
$prodAuthority = terraform output -raw cognito_authority
$prodClientId = terraform output -raw cognito_client_id
$prodLokiUrl = terraform output -raw grafana_loki_push_url
$prodLokiUser = terraform output -raw grafana_loki_user
$prodR2AccessKey = Read-Host -MaskInput "Production photo R2 Access Key ID"
$prodR2SecretKey = Read-Host -MaskInput "Production photo R2 Secret Access Key"
```

Set each secret individually. Replace `<account-id>` locally; never commit the real
Cloudflare account identifier.

```powershell
flyctl secrets set --stage --app fishing-logbook-prod-api "ConnectionStrings__Postgres=$prodDatabase"
flyctl secrets set --stage --app fishing-logbook-prod-api 'ObjectStorage__ServiceUrl=https://<account-id>.r2.cloudflarestorage.com'
flyctl secrets set --stage --app fishing-logbook-prod-api 'ObjectStorage__BucketName=fishing-logbook-prod'
flyctl secrets set --stage --app fishing-logbook-prod-api "ObjectStorage__AccessKeyId=$prodR2AccessKey"
flyctl secrets set --stage --app fishing-logbook-prod-api "ObjectStorage__SecretAccessKey=$prodR2SecretKey"
flyctl secrets set --stage --app fishing-logbook-prod-api "Auth__Authority=$prodAuthority"
flyctl secrets set --stage --app fishing-logbook-prod-api "Auth__ClientId=$prodClientId"
flyctl secrets set --stage --app fishing-logbook-prod-api 'ExternalLogging__Provider=GrafanaCloud'
flyctl secrets set --stage --app fishing-logbook-prod-api "ExternalLogging__Url=$prodLokiUrl"
flyctl secrets set --stage --app fishing-logbook-prod-api "ExternalLogging__User=$prodLokiUser"
flyctl secrets set --stage --app fishing-logbook-prod-api "ExternalLogging__ApiToken=$prodLokiToken"
```

Clear the local values and verify names/status without revealing values:

```powershell
Remove-Variable prodDatabase, prodDbHost, prodDbName, prodDbUser, prodDbPassword, prodLokiToken, prodAuthority, prodClientId, prodLokiUrl, prodLokiUser, prodR2AccessKey, prodR2SecretKey
flyctl secrets list --app fishing-logbook-prod-api
```

All eleven entries should be `Staged`. Do not run `flyctl secrets deploy`; the first
application deployment activates them. On Windows PowerShell, piping multiple secret
lines to `flyctl secrets import` can add a UTF-8 BOM to the first name. Individual
`flyctl secrets set` commands avoid that problem.

If Npgsql receives a `postgres://` URI, parsing can fail and the resulting exception may
include the malformed value. Rotate the database role password immediately if a URI with
credentials ever reaches application logs, then replace the Fly secret with the key/value
form above.

## Permanent API hostname and TLS

Request the certificate, then inspect Fly's generated DNS targets:

```powershell
flyctl certs add api.catchbutdontforget.com --app fishing-logbook-prod-api
flyctl certs setup api.catchbutdontforget.com --app fishing-logbook-prod-api
```

Create the exact CNAME and ownership/ACME records returned by `certs setup` at the
domain's authoritative DNS provider. For the API subdomain, prefer the generated CNAME
target and do not also create an A record for the same name. DNS must point directly to
Fly; do not proxy this initial configuration through a CDN.

Verify public DNS before checking the certificate:

```powershell
Resolve-DnsName api.catchbutdontforget.com -Type CNAME
Resolve-DnsName _acme-challenge.api.catchbutdontforget.com -Type CNAME
Resolve-DnsName _fly-ownership.api.catchbutdontforget.com -Type TXT
flyctl certs check api.catchbutdontforget.com --app fishing-logbook-prod-api
```

Continue only when Fly reports that the certificate is verified and active.

## Database migrations

Migrations are deliberately separate from Fly deployment. Follow the production runbook:
review pending DbUp scripts interactively, apply them explicitly, then run the migration
tool again and require no pending migrations. Never make the API migrate on startup.

## First production deployment

Deploy only after the database is current, the certificate is active, and the protected
GitHub `prod` environment is configured. The preferred first deployment is the manually
dispatched `deploy-production` workflow with target `api`.

For an authorised local recovery deployment, run from the repository root:

```powershell
flyctl deploy . --config infrastructure/fly/fly.prod.toml --app fishing-logbook-prod-api
```

Do not add `fly apps create`, IP allocation, scaling, secret configuration, or database
migrations to CI.

## Verification and diagnostics

```powershell
flyctl status --app fishing-logbook-prod-api
flyctl checks list --app fishing-logbook-prod-api
flyctl logs --app fishing-logbook-prod-api
```

Production smoke endpoints:

```text
https://api.catchbutdontforget.com/health
https://api.catchbutdontforget.com/api/system/database
```

Grafana production logs use `{app="fishing-logbook-api", env="prod"}`. Never paste or
log connection strings, bearer tokens, R2 credentials, Loki tokens, photographs, private
notes, or precise locations while diagnosing a deployment.

## Development

Development uses its separate application, database, R2 bucket, Cognito pool, secrets,
and configuration:

```powershell
flyctl deploy . --config infrastructure/fly/fly.dev.toml --app fishing-logbook-dev-api
```

GitHub Actions uses an app-scoped deployment token stored as `FLY_API_TOKEN` on the
matching GitHub Environment, never as a repository-wide secret:

```powershell
flyctl tokens create deploy --app fishing-logbook-dev-api --name github-actions-dev
```

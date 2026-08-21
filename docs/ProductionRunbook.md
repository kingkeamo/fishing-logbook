# Production v0.1 runbook

Production uses the existing provider accounts with isolated resources and credentials.
Terraform remains manual: never add `terraform apply` or `terraform destroy` to CI.

## Permanent origins

| Role | Origin |
|---|---|
| Public entry | `https://catchbutdontforget.com` |
| PWA | `https://app.catchbutdontforget.com` |
| API | `https://api.catchbutdontforget.com` |

The generated Pages and Fly hostnames remain provider endpoints, not supported user-facing
origins. Cloudflare DNS for the API starts DNS-only; Fly terminates TLS.

## Resource isolation

- Cloudflare Pages: `fishing-logbook-prod` and `catch-but-dont-forget-root`.
- R2: private `fishing-logbook-prod` bucket with production-scoped credentials and CORS
  restricted to the PWA origin.
- Fly: `fishing-logbook-prod-api`, 256 MB `shared-cpu-1x`, auto-stop and auto-start with
  zero minimum running machines.
- Neon: separate `fishing-logbook-prod` project. Production never uses a Dev branch,
  database or connection string.
- Cognito: separate production pool, public client, hosted domain, resource server and
  pre-token Lambda.
- Grafana Cloud: reuse the existing stack with a separate production write token. Loki
  streams use `app="fishing-logbook-api"` and `env="prod"`.

R2 protects object durability but this release does not add object versioning or a media
backup/export system. An accidental application-level delete might therefore be
unrecoverable. Neon Free provides only its current limited PITR window; confirm it in the
provider console before launch.

## GitHub production environment

Create a protected GitHub environment named `prod` and require manual approval.

Variables (public identifiers):

```text
API_BASE_URL=https://api.catchbutdontforget.com
AUTH_AUTHORITY=<production Cognito issuer>
AUTH_CLIENT_ID=<production public app-client id>
AUTH_HOSTED_UI_DOMAIN=https://fishing-logbook-prod.auth.<region>.amazoncognito.com
AUTH_API_SCOPE=https://api.catchbutdontforget.com/access
AUTH_API_RESOURCE=https://api.catchbutdontforget.com
```

Secrets:

```text
CLOUDFLARE_API_TOKEN=<Pages deploy token>
CLOUDFLARE_ACCOUNT_ID=<account id>
FLY_API_TOKEN=<app-scoped production deploy token>
```

The production Web workflow refuses incomplete configuration, any HTTP/local/dev marker,
or an API resource/scope that does not exactly match the permanent production API. It
validates the published artifact again before upload.

API runtime secrets stay in Fly and are not committed:

```text
ConnectionStrings__Postgres
ObjectStorage__ServiceUrl
ObjectStorage__BucketName=fishing-logbook-prod
ObjectStorage__AccessKeyId
ObjectStorage__SecretAccessKey
Auth__Authority
Auth__ClientId
ExternalLogging__Provider=GrafanaCloud
ExternalLogging__Url
ExternalLogging__User
ExternalLogging__ApiToken
```

Non-secret API configuration comes from `fly.prod.toml`, including the one allowed CORS
origin, API audience/scope and `ExternalLogging__Environment=prod`.

## Provisioning and first deployment

1. Confirm Cloudflare zone ownership, provider plans, regions and all account identifiers.
2. Copy prod `terraform.tfvars.example` to gitignored `terraform.tfvars` and complete it.
3. Initialise the prod backend and run `terraform fmt`, `validate`, then `plan -out
   prod.tfplan`. Review every action; abort on unexpected replacement or destruction.
4. Manually apply only the reviewed plan.
5. Create `fishing-logbook-prod-api` manually in the existing Fly organisation.
6. Configure production-only Fly secrets, including the separate R2 and Grafana tokens.
7. Run `fly certs add api.catchbutdontforget.com`; add its exact DNS-only CNAME and
   ownership/validation records in Cloudflare, then use `fly certs check`.
8. Run the existing DbUp runner against the production connection string first without
   `--run` to review pending scripts, then explicitly with `--run`. Run it again and
   require no pending migrations.
9. Dispatch `deploy-production` for `api`; approve the `prod` environment gate and verify
   both health endpoints.
10. Dispatch it for `web`, then for `root`; verify their branded smoke checks.
11. Confirm Pages custom-domain TLS is active and provider-generated origins are not
    exposed in normal navigation.
12. Complete the production acceptance journey on physical iPhone and Android devices
    and applicable Windows browsers.

Production deployment is never triggered by a normal Dev push. The workflow deploys only
one explicitly chosen target per manually approved dispatch. Infrastructure creation and
database migrations are not workflow jobs.

## Recovery and rollback

- Pages: roll back to a known previous deployment in Cloudflare.
- Fly: redeploy a known image/release after confirming schema compatibility.
- Neon: use provider PITR within the available Free-plan window or restore a separately
  captured logical export when one exists.
- R2: diagnose failed upload/sync operations through production API/client diagnostics;
  there is no v0.1 recovery mechanism for an object that has been successfully deleted.
- Grafana: query `{app="fishing-logbook-api", env="prod"}`. Free retention is diagnostic
  history, not a permanent audit record.

Never log bearer, access or refresh tokens, database/storage credentials, connection
strings, photos, private notes or precise private locations.

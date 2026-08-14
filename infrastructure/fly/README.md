# Fly.io API hosting

Fly apps are created and deployed with **flyctl**, not Terraform (there is no official
Fly Terraform provider). These files live here so all infrastructure config sits under
`infrastructure/`, next to Terraform.

| File | App | When |
|---|---|---|
| `fly.dev.toml` | `fishing-logbook-dev-api` | Dev |
| `fly.prod.toml` | `fishing-logbook-prod-api` | Prod (create later, manually) |

The API **image** is still built from the repo-root [`Dockerfile`](../../Dockerfile)
(`dockerfile = "../../Dockerfile"` in the toml — Fly resolves that path relative to
the config file, not the deploy context). Always run `flyctl` from the **repository
root** so the Docker context includes `src/`.

## Create once (manual, never CI)

```powershell
fly auth login
fly apps create fishing-logbook-dev-api --org personal
fly secrets set ConnectionStrings__Postgres="<neon npgsql string>" --app fishing-logbook-dev-api
fly secrets set ObjectStorage__ServiceUrl="https://<account-id>.r2.cloudflarestorage.com" ObjectStorage__BucketName="fishing-logbook-dev" ObjectStorage__AccessKeyId="<r2-access-key>" ObjectStorage__SecretAccessKey="<r2-secret>" --app fishing-logbook-dev-api
```

Optional Grafana Cloud log shipping (leave unset to keep external logging disabled).
After Terraform has created the Loki write token (see `infrastructure/README.md`):

```powershell
fly secrets set `
  ExternalLogging__Provider="GrafanaCloud" `
  ExternalLogging__Url="<terraform output grafana_loki_push_url>" `
  ExternalLogging__User="<terraform output grafana_loki_user>" `
  ExternalLogging__ApiToken="<terraform output grafana_loki_write_token>" `
  --app fishing-logbook-dev-api
```

`ExternalLogging__Environment` is not a secret. Dev is `dev` and prod is `prod`, set in the Fly toml `[env]` block. Local Development uses `localhost` from `appsettings.Development.json`. Query Grafana with `{app="fishing-logbook-api", env="localhost"}` or `env="dev"`.

Do not invent these Grafana values. Copy them from Terraform outputs after a reviewed
apply, or from the Grafana Cloud portal if Terraform has not been applied yet. The API
runs without them.

## Deploy

Local (from the repository root):

```powershell
fly deploy . --config infrastructure/fly/fly.dev.toml --app fishing-logbook-dev-api
```

CI (`.github/workflows/deploy-api.yml`) runs the same deploy after tests pass, only on
merge to `main`. It may **only** deploy a new image to an app that already exists. It
must never run `fly apps create`, `fly apps destroy`, `fly scale`, or `fly machine`
create/destroy.

Secrets stay in Fly (`fly secrets set`), not in git. GitHub Actions authenticates with
`FLY_API_TOKEN` on the `dev` GitHub Environment:

```powershell
fly tokens create deploy --app fishing-logbook-dev-api --name github-actions-dev
```

Add that token as `FLY_API_TOKEN` on the `dev` environment (not a repository-wide secret).

## Checks

```text
https://fishing-logbook-dev-api.fly.dev/health
https://fishing-logbook-dev-api.fly.dev/api/system/database
```

Swagger is not served on Fly (Production). Neon migrations must already have been applied.

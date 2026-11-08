# Apple and Google sign-in (issue #302)

Cognito stays the only authentication authority. Google and Apple are Cognito federated
identity providers. Native MAUI and Web/PWA keep their existing OIDC authorization-code +
PKCE flow, callbacks, refresh and session handling; the only protocol change is the
`identity_provider` authorize parameter, which preselects the provider and skips the Cognito
chooser. Email/password remains available everywhere.

## What the code does

| Area | Behaviour |
|---|---|
| Terraform (`modules/cognito/identity-providers.tf`) | Creates `aws_cognito_identity_provider` for Google and Apple only when their IDs are set; adds them to both app clients' `supported_identity_providers`. Secrets are `sensitive` variables supplied via `TF_VAR_*`. |
| Attribute mapping | `email`, `email_verified`, `username = sub` for both providers. `email_verified` must be mapped: the Pre Token Generation Lambda only adds `email` to the access token when it is verified, and the API returns 401 for tokens without `email`. |
| Native (iOS/Android) | `Continue with Apple` (iOS only), `Continue with Google` (iOS and Android), `Continue with email`. Buttons appear only when `SocialProviders` in `mobileauth.<env>.json` lists the provider **and** the platform supports it. |
| Web/PWA | Every sign-in entry point (app-bar Sign in and Create account, landing page, offline reconnect, protected-page redirect) goes through `ISignInNavigationService`. When `Auth:SocialProviders` is non-empty it opens the `/sign-in` chooser; with none configured it goes straight to Cognito as before. The chosen provider is sent as `identity_provider`. |
| API | Unchanged. `Provider = "Cognito"`, `Subject = sub`; a federated user is a different Cognito user with a different `sub`. |

Apple is not offered on Android: Apple's guideline 4.8 requires it wherever Google is offered
on iOS, but Apple sign-in on Android would be a web fallback only. Change
`SignInProviderService` if you want it.

## Account mapping and linking

Cognito creates a **separate user** (username `Google_<id>` / `SignInWithApple_<id>`, new
`sub`) for each federated identity. CBDF maps `sub` → `UserId`, and `users.email` is not
unique, so:

- A brand-new Google/Apple user gets a new CBDF account with no password. ✔
- A returning federated user resolves to the same `UserId` every time. ✔
- A federated user whose email matches an existing email/password account gets a **new,
  empty** account. Nothing is merged. ✔ (this is the "never auto-merge" requirement)

### Not implemented: explicit linking for existing users

Safe linking is **not** in this PR. The only supported mechanism that keeps the existing
`sub` (and so all catches, trips and profile data) is Cognito `AdminLinkProviderForUser`,
which makes the federated login resolve to the existing Cognito user. It must be triggered
only after the user proves control of both accounts. That needs:

1. an authenticated "Connect Google/Apple" API action;
2. a second proof (a federated sign-in completed in the same session, state-bound);
3. AWS credentials for the API with `cognito-idp:AdminLinkProviderForUser` (new IAM user/role
   and a Fly secret, i.e. new infrastructure — not allowed without approval per
   `terraform.md`).

**Decision needed from the product owner:**

- Is linking required before the first external test round, or is "existing testers sign in
  with their password; new testers use Apple/Google" acceptable?
- If linking is required, approve the IAM principal and Fly secret above. The alternative that
  needs no AWS access — adding a second `useridentities` row for the same `UserId` from a
  verified federated token — is possible because CBDF owns the `sub` → `UserId` mapping, but it
  requires the API to validate two tokens in one request and a new endpoint and UI.

Do **not** add a Cognito Pre Sign-up trigger that links by email: it would link unverified
or relayed addresses automatically.

Until then, a user who signs in with a provider using an email that already has a password
account sees an empty logbook. Tell testers to keep using their original method.

## Verified email

- **Google**: returns `email_verified=true` for Google accounts.
- **Apple**: returns `email_verified=true`, including for Hide My Email private-relay
  addresses (`...@privaterelay.appleid.com`). Apple returns the email only on the **first**
  authorization; Cognito stores it, so later sign-ins work. If a tester revoked the app in
  Apple ID settings before Cognito stored an email, the access token has no email and the API
  returns 401 — have them remove the app from Apple ID → Sign in with Apple and retry.
- The Lambda tests cover verified, unverified and missing `email_verified` for federated users.

## Owner setup

All values are environment specific (dev and prod are separate). Never commit them.

### Google Cloud

1. Create an OAuth consent screen (External); add scopes `openid`, `email`, `profile`;
   publish before external testers (testing mode caps users).
2. Create an OAuth client ID of type **Web application**.
3. Authorized redirect URI: `https://<cognito-hosted-ui-domain>/oauth2/idpresponse`
   (Terraform output `cognito_hosted_ui_domain`; for dev
   `https://fishing-logbook-dev.auth.eu-west-2.amazoncognito.com/oauth2/idpresponse`).
4. Keep the client ID (`cognito_google_client_id`) and secret (`TF_VAR_cognito_google_client_secret`).
   No Android/iOS OAuth client is needed: the app uses the system browser through Cognito.

### Apple Developer

1. App ID for the iOS app with the **Sign in with Apple** capability enabled.
2. Create a **Services ID** (this is `cognito_apple_services_id`); enable Sign in with Apple,
   set the domain to the Cognito hosted-UI domain and the return URL to
   `https://<cognito-hosted-ui-domain>/oauth2/idpresponse`.
3. Create a **Key** with Sign in with Apple enabled; download the `.p8` once. Note the Key ID
   (`cognito_apple_key_id`) and the Team ID (`cognito_apple_team_id`).
4. To email relay users, register the Cognito/FishingLogBook sending domain under Apple's
   "Configure Sign in with Apple for Email Communication" if outbound email is ever sent.

### Terraform / AWS (manual, after review of `terraform plan`)

```text
export TF_VAR_cognito_google_client_secret='…'
export TF_VAR_cognito_apple_private_key="$(cat AuthKey_XXXX.p8)"
# terraform.tfvars (gitignored): cognito_google_client_id, cognito_apple_services_id,
#   cognito_apple_team_id, cognito_apple_key_id
terraform plan   # review: adds identity providers, updates both app clients (in place)
terraform apply  # manual only
```

Updating `supported_identity_providers` is an in-place change; user pool and existing users
are untouched. This PR did not run plan or apply.

### App configuration after apply

- Web: set `Auth:SocialProviders` in `wwwroot/appsettings.<env>.json` (for example
  `["google", "apple"]`).
- Native: add `"SocialProviders": ["google", "apple"]` to `Configuration/mobileauth.<env>.json`.
- iOS: the flow uses the system browser through Cognito, so no native entitlement is added
  here. Confirm with App Store review whether the Sign in with Apple capability is also wanted.
- Leave the lists empty to keep the buttons hidden until the provider works end to end.

## Verification

Automated (this PR): Shared constants; native provider availability per platform/config;
`PrepareLoginAsync` adds `identity_provider=Google|SignInWithApple` with PKCE and `resource`
and nothing for email; `BeginSignInAsync` forwards the provider; view-model visibility,
unavailable-provider guard, failure and pending states; Web chooser buttons, localisation,
Cognito parameter and email path; Lambda email handling for federated users; `terraform
validate` for dev and prod.

Not verifiable without credentials/devices (**not run**):

- [ ] iPhone: fresh Apple ID → Continue with Apple → Dashboard; kill and relaunch; sign out and
      in; API call; record first catch offline and sync.
- [ ] Android: fresh Google account → Continue with Google → same checks.
- [ ] Web/PWA on Chrome and Safari: both providers and the callback.
- [ ] Apple Hide My Email account is accepted by the API.
- [ ] Cancelling at Apple/Google returns to the sign-in screen with a retry (Cognito returns
      `access_denied`, mapped to Cancelled).
- [ ] Provider error / revoked access shows the generic sign-in-failed message.
- [ ] Existing email/password user still signs in.
- [ ] Same-email federated user gets a separate empty account (confirms no auto-merge).

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
| API | Unchanged. `Provider = "Cognito"`, `Subject = sub`. With account linking a federated sign-in returns the canonical native user's `sub`, so the existing `UserId` and data are kept. |
| Account linking | Pre Sign-up Lambda links a verified-email match, or creates a native user and links it. See "Account linking" below. |

Apple is not offered on Android: Apple's guideline 4.8 requires it wherever Google is offered
on iOS, but Apple sign-in on Android would be a web fallback only. Change
`SignInProviderService` if you want it.

## Account linking (canonical Cognito user)

Every person should resolve to **one native Cognito user** and so one `sub` and one CBDF
`UserId`, whether they sign in with email/password, Google or Apple. CBDF still maps
`Provider = "Cognito"` + `sub` → `UserId`; nothing changes in the API or database, because a
linked federated sign-in returns tokens for the native user and therefore the native `sub`.

### How it works

A **Pre Sign-up Lambda** (`modules/cognito/lambda/pre-sign-up.mjs`) runs the first time a
Google or Apple identity appears (`PreSignUp_ExternalProvider`). Every other trigger source is
returned unchanged.

1. The provider and its user id come from the incoming user name (`Google_<id>`,
   `SignInWithApple_<id>`). The pool's user names are case-insensitive and Cognito can pass them
   lower-cased (`google_<id>`), so the prefix is matched case-insensitively and mapped to the
   canonical provider name (`Google`, `SignInWithApple`) before linking. Any other provider is
   rejected.
2. The IdP email must be present and `email_verified = true`, otherwise sign-in is rejected.
3. `ListUsers` finds native users with one search on the normalised (trimmed, lower-case) email.
   The filter value is escaped and every page (`PaginationToken`) is read. If the complete
   result cannot be inspected (the page limit is reached or Cognito repeats a token) sign-in is
   rejected and nothing is created or linked. Each returned user's email is checked again for an
   exact normalised match, and federated `EXTERNAL_PROVIDER` shadow users are ignored.
4. More than one match is rejected rather than choosing one.
5. One match must be **enabled, `CONFIRMED` and `email_verified`**; otherwise sign-in is
   rejected. The identity is then linked with `AdminLinkProviderForUser`.
6. No match: a native user is created (`AdminCreateUser`, email verified, no message sent),
   given a random permanent password that is never stored, logged or returned
   (`AdminSetUserPassword`), and the identity is linked to it. See "Social-first accounts".
7. Already linked: nothing happens. A different identity from the **same provider** on the
   same account is rejected as a conflict.
8. If `AdminLinkProviderForUser` fails, the account is re-read; the sign-in succeeds only if the
   identity is now linked to the intended account (a concurrent request won). Other AWS
   failures are rethrown, never swallowed.

Rejections return a generic error with a reason code in the logs. Logs never contain emails,
user names, provider ids or passwords.

### Policy and its limitations

For this MVP a **verified Google or Apple email that matches an eligible native account links
automatically**. A verified email is **not** proof that the person owns the CBDF account.
Known limitations:

- Anyone who controls a Google or Apple account with that verified email can sign in as the
  existing user. The risk is the mailbox, not CBDF. Google verifies emails; Apple verifies the
  address it holds, which is always the user's Apple ID email or a relay.
- The native account must already be confirmed, so an attacker cannot pre-register an
  unconfirmed account with a victim's address and receive the victim's later Google sign-in.
- If someone loses control of an email address and it is re-issued, the new holder can link.
  An explicit account-connection step (re-authenticate, then link) would remove this; it is not
  built in this ticket.
- Linking uses the Cognito `Cognito_Subject` attribute and is permanent from the Lambda's side.
  Unlinking is manual (`AdminDisableProviderForUser`).
- **Apple Hide My Email:** the relay address (`…@privaterelay.appleid.com`) matches no native
  account, so a **separate** native account is created for it and nothing is merged. If the
  person later signs in with their real email elsewhere, they have two accounts. This cannot
  be solved automatically; it needs the explicit connection flow.
- The Cognito reference does not state whether the `email` filter is case-insensitive (only a
  community answer says it is), so the Lambda searches once on the lower-case email and relies on
  the exact check after retrieval. If the filter were case-sensitive, a native account stored
  with upper-case letters would be missed; creating a user for the same address would then fail
  with `UsernameExistsException`, and sign-in is rejected (`account_ambiguous`) rather than
  linking wrongly. Verify with a mixed-case account on dev.
- The Lambda must finish within Cognito's 5 second trigger limit. Creating a user does four
  Cognito calls.

### Social-first accounts

A user created by `AdminCreateUser` starts in `FORCE_CHANGE_PASSWORD`, and Cognito does not
allow forgot-password in that state, so the person could never add a password later. The
Lambda therefore sets a random permanent password (unknown to everyone, discarded) which moves
the user to `CONFIRMED`. The person adds a password with **Forgot password** on the email
sign-in page (their email is verified). Until they do, the account only works through
Google/Apple, and a second provider with the same verified email links to it. If setting the
password fails, the Lambda deletes the user it just created so nothing is left in
`FORCE_CHANGE_PASSWORD`; a user left unlinked after a later link failure is harmless and is
reused next time.

Email/password **sign-up** with an address that already exists as a native user is refused by
Cognito. That person should use Forgot password.

### First federated sign-in

Community reports describe Cognito returning `Already found an entry for username
Google_…` on the **first** federated sign-in after a Pre Sign-up link, succeeding on retry.
This has not been reproduced here. Check it during dev verification; add an automatic retry only
if it happens.

### Existing Development accounts (PR #306 testing)

Google and Apple users created before this change already exist as separate federated
Cognito users with their own `sub` and a separate, usually empty, CBDF account. They do not pass
through the trigger again, so they stay unlinked. Do **not** delete anything automatically.

1. In the Cognito console (dev pool `fishing-logbook-dev-users`) open Users and look for user
   names beginning `Google_` or `SignInWithApple_`; the status is `External provider`.
2. For each, find the CBDF account: its `sub` is `useridentities.subject` where
   `provider = 'Cognito'`; `useridentities.userid` is the CBDF `UserId`.
3. Check what is on that `UserId` (catches, trips, photographs). Only if it is empty or
   disposable: delete the Cognito user, then delete the CBDF rows (`useridentities` first, then
   `users`) with the DB owner's tooling. Anything with real data stays untouched.
4. Sign in again. The next sign-in goes through the trigger and links to the native user.

If a test person's real data lives on the federated account instead, linking cannot move it:
sign in with the original method and keep the federated account out of the test.

### Deploying

New Terraform resources: Pre Sign-up Lambda, execution role, a pool-scoped IAM policy, log
group, Lambda permission, and `pre_sign_up` in the existing user pool `lambda_config`. This is
an in-place user pool change; the pool is not replaced, the Pre Token Generation trigger,
identity providers and app clients are untouched, and `prevent_destroy` is kept. Run
`terraform plan` and review it before a manual `terraform apply`. Terraform is never applied
automatically.

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
Cognito parameter and email path; Lambda email handling for federated users; the Pre Sign-up Lambda (84 Lambda tests in total, mocked AWS SDK); `terraform
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
- [ ] Existing email/password → Google: same Cognito `sub` and CBDF `UserId`, catches intact.
- [ ] Existing email/password → Apple: same account.
- [ ] Google first, then Apple with the matching verified email: same account (and the reverse).
- [ ] Social first, then Forgot password on the email page: password set and email sign-in works on the same account.
- [ ] Repeat social sign-in: no new user in the pool, no new CBDF user.
- [ ] Native account registered with mixed-case letters in its email: Google/Apple with the same address links to it (confirms the single lower-case search finds it).
- [ ] Apple Hide My Email: a separate account, no merge into an existing account.
- [ ] Unverified or conflicting identity is rejected with the generic sign-in error.
- [ ] Web, iOS and Android all sign in and call the API.
- [ ] First federated sign-in after linking: note whether "Already found an entry for username" appears.

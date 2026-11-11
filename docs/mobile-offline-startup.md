# Mobile offline startup foundation

Issue #268 is delivered in three sequential pull requests. PR 1 established startup and
secure unlock; PR 2 adds automatic, verified device preparation after onboarding.

CBDF attempts online restoration when internet connectivity is available. A temporary Cognito
or CBDF identity-service outage or restoration timeout may fall back to secure offline unlock
even while the device reports internet connectivity. Only an already prepared account can
unlock; explicit authentication rejection and local storage failures do not permit fallback.

## Startup decisions

The native startup coordinator makes one bounded decision and always selects a destination
or a recoverable error:

1. With internet connectivity, restore the Cognito credential.
2. Refresh transient failures receive at most one immediate retry. The retry pipeline is
   shared, and the second refresh request is dispatched only when it can begin within the
   four-second safety window inside Cognito's configured five-second rotation grace period.
3. After transient exhaustion or a restoration timeout, attempt secure offline unlock without
   immediately retrying the same failed restoration, regardless of reported connectivity.
4. Without internet connectivity, read the secure device capability and require Android or
   iOS device-owner authentication. If connectivity returns while that prompt is active,
   attempt online restoration once. A transient failure may still activate the securely
   unlocked local account; rejection, pending/cancelled sign-in, and storage errors cannot.
5. A successful offline unlock activates only an account-scoped local session lease. It
   does not create or expose an API bearer token.

A first launch without internet, missing readiness, failed device verification, and cancellation
all resolve to localized recoverable states instead of an indefinite
loading screen.

## Secure capability

The capability is stored using MAUI secure storage and binds the Cognito subject to the
internal CBDF user ID. Marking it ready writes and reads the value back before readiness is
accepted. Explicit logout, permanent Cognito rejection, and an online transition to a
different account revoke the capability without deleting account-scoped application data.

Android uses the operating-system biometric/device credential prompt. Android 26–28 uses
the system device-credential confirmation screen. Android 29 and later uses the system
biometric prompt with device credentials allowed. iOS uses
`LAPolicy.DeviceOwnerAuthentication`, which permits Face ID, Touch ID, or the device passcode.

## Automatic offline preparation

A server-completed onboarding profile is not sufficient on its own. Before the native app
records device-local onboarding completion or enters the authenticated workspace, it:

1. verifies that the authenticated Cognito subject, CBDF user ID, account lease, and profile
   owner all agree;
2. downloads the current fishing catalogue, preferences, locations, profile, identity, and
   measurement-unit settings;
3. atomically persists account-scoped profile and preference snapshots and reads them back;
4. writes and verifies the secure offline capability last; and
5. revalidates the complete cache/capability set before reporting preparation complete.

The profile snapshot carries the server-confirmed onboarding state and measurement units.
The preferences snapshot carries the required fishing method/species catalogue, the user's
selections, and fishing locations. These are essential startup data. Catch data, photographs,
discovery content, and other refreshable screens remain outside this preparation boundary.

Preparation is idempotent. A verified prepared account causes no network refresh or secure
storage rewrite. An interrupted or partially completed preparation remains in a recoverable
state and can safely retry; because the capability is committed last, partial local files do
not grant offline access. Returning users who completed onboarding before installing this
version are prepared automatically while online.

Offline startup validates the secure capability and both account-scoped snapshots before it
shows the device-owner prompt, then revalidates readiness before activating the local session.
This prevents a sign-out or revocation during unlock from reviving the account.
Missing, corrupt, incomplete, or differently owned data fails
closed and asks the user to reconnect. No offline session contains an API bearer token.

## PR boundaries

PR 2 persists and validates the profile, measurement, preference, location, and catalogue
foundation required by offline startup. PR 3 owns the complete
connectivity-return, online identity revalidation, and synchronization-readiness lifecycle.
Offline catch/photo persistence and synchronization remain in issue #269 and later issues.

## Manual Android validation

Validate the complete PR 2 journey on a physical Android device:

1. Sign in and complete onboarding online.
2. Force stop the app, disable internet connectivity, and relaunch.
3. Confirm the system biometric/device credential UI appears.
4. Cancel once and confirm the app remains locked with a retry action.
5. Unlock and confirm only the prepared account's local data is visible.
6. Confirm protected API calls cannot obtain a bearer token while offline.
7. Sign out and confirm a later offline launch cannot unlock.

Automated tests exercise preparation, partial failure, ownership, persistence verification,
and startup rejection with incomplete data. Device prompt presentation and operating-system
secure-storage behaviour still require emulator or physical-device validation.

Also repeat the launch with internet enabled while the authentication or identity service is
unreachable. Confirm that the prepared account can unlock, cancellation keeps it locked,
and restoring the service allows the existing session lifecycle to revalidate the same owner
and enable synchronization. The lifecycle retries on foreground, connectivity return, or
explicit retry; authentication backoff remains in effect. An explicit 401/403 or permanent
refresh rejection must require sign-in and revoke offline readiness.

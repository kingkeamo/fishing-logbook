# Mobile offline startup foundation

Issue #268 is delivered in three sequential pull requests. This document describes the
PR 1 boundary only.

CBDF remains online-first. Offline access is available only when the device reports that
it has no internet connection. A Cognito or CBDF API outage while the device remains online
is shown as a recoverable service error and does not silently enable offline mode.

## Startup decisions

The native startup coordinator makes one bounded decision and always selects a destination
or a recoverable error:

1. With internet connectivity, restore the Cognito credential.
2. Refresh transient failures receive at most one immediate retry. The retry decision must
   occur within 3.5 seconds, leaving margin inside Cognito's configured five-second refresh
   rotation grace period.
3. After transient exhaustion, check connectivity again. A device that lost connectivity
   may use offline unlock; a still-online device receives a retryable service error.
4. Without internet connectivity, read the secure device capability and require Android or
   iOS device-owner authentication.
5. A successful offline unlock activates only an account-scoped local session lease. It
   does not create or expose an API bearer token.

A first launch without internet, missing readiness, failed device verification, cancellation,
and startup timeout all resolve to localized recoverable states instead of an indefinite
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

## PR boundaries

PR 1 deliberately does not call the readiness method from onboarding. Consequently, an
ordinary installed build will not become offline-ready from this PR alone. PR 2 must prepare
the required local onboarding/profile/catalogue data, persist and verify the secure
capability, and only then mark device-local onboarding readiness complete. Server-confirmed
onboarding status remains separate.

PR 2 owns profile, measurement, and catalogue cache fallback. PR 3 owns the complete
connectivity-return, online identity revalidation, and synchronization-readiness lifecycle.
Offline catch/photo persistence and synchronization remain in issue #269 and later issues.

## Manual Android validation

After PR 2 can prepare an entitlement, validate on a physical Android device:

1. Sign in and complete onboarding online.
2. Force stop the app, disable internet connectivity, and relaunch.
3. Confirm the system biometric/device credential UI appears.
4. Cancel once and confirm the app remains locked with a retry action.
5. Unlock and confirm only the prepared account's local data is visible.
6. Confirm protected API calls cannot obtain a bearer token while offline.
7. Sign out and confirm a later offline launch cannot unlock.

PR 1 unit tests exercise these state transitions with a pre-provisioned capability; device
prompt presentation still requires emulator or physical-device validation.

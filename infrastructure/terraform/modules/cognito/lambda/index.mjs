// Cognito Pre Token Generation trigger (event version V2_0).
// Copies the verified email user attribute onto the access token. Does not log PII.
// Reserved claims (sub, token_use, aud, iss, client_id, and similar) are never set.

export function applyAccessTokenEmail(event) {
  const attributes = event?.request?.userAttributes ?? {};
  const email = typeof attributes.email === "string" ? attributes.email.trim() : "";
  const verified =
    attributes.email_verified === true || attributes.email_verified === "true";

  if (email.length === 0 || !verified) {
    return event;
  }

  const response = event.response ?? {};
  const details = response.claimsAndScopeOverrideDetails ?? {};
  const accessTokenGeneration = details.accessTokenGeneration ?? {};
  const claimsToAddOrOverride = {
    ...(accessTokenGeneration.claimsToAddOrOverride ?? {}),
    email
  };

  event.response = {
    ...response,
    claimsAndScopeOverrideDetails: {
      ...details,
      accessTokenGeneration: {
        ...accessTokenGeneration,
        claimsToAddOrOverride
      }
    }
  };

  return event;
}

export async function handler(event) {
  return applyAccessTokenEmail(event);
}

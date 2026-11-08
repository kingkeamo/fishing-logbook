// Cognito Pre Sign-up trigger. Links a first-time Google or Apple identity to one
// canonical native Cognito user so every sign-in method resolves to the same sub.
// Policy: a verified IdP email matching an eligible native account links automatically.
// Never logs emails, user names, provider user ids or passwords.

import { randomBytes } from "node:crypto";

const EXTERNAL_PROVIDER_TRIGGER = "PreSignUp_ExternalProvider";
const SUPPORTED_PROVIDERS = new Set(["Google", "SignInWithApple"]);
const EXTERNAL_PROVIDER_STATUS = "EXTERNAL_PROVIDER";
const CONFIRMED_STATUS = "CONFIRMED";
const LINK_ATTRIBUTE_NAME = "Cognito_Subject";
const NATIVE_PROVIDER_NAME = "Cognito";

export class AccountLinkingError extends Error {
  constructor(code) {
    super(`Sign-in could not be completed: ${code}.`);
    this.name = "AccountLinkingError";
    this.code = code;
  }
}

export function parseProviderIdentity(userName) {
  const name = typeof userName === "string" ? userName : "";
  const separator = name.indexOf("_");
  const provider = separator > 0 ? name.slice(0, separator) : "";
  const providerUserId = separator > 0 ? name.slice(separator + 1) : "";

  if (!SUPPORTED_PROVIDERS.has(provider) || providerUserId.length === 0) {
    throw new AccountLinkingError("unsupported_identity");
  }

  return { provider, providerUserId };
}

export function normalizeEmail(email) {
  return typeof email === "string" ? email.trim().toLowerCase() : "";
}

export function escapeFilterValue(value) {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function readVerifiedEmail(userAttributes) {
  const raw = typeof userAttributes?.email === "string" ? userAttributes.email.trim() : "";
  const verified =
    userAttributes?.email_verified === true || userAttributes?.email_verified === "true";

  if (raw.length === 0) {
    throw new AccountLinkingError("email_missing");
  }

  if (!verified) {
    throw new AccountLinkingError("email_unverified");
  }

  return { raw, normalized: normalizeEmail(raw) };
}

function readIdentities(account) {
  const value = account.attributes.identities;
  if (value === undefined) {
    return [];
  }

  try {
    const parsed = JSON.parse(value);
    if (!Array.isArray(parsed)) {
      throw new Error("not an array");
    }

    return parsed;
  } catch {
    throw new AccountLinkingError("account_identities_unreadable");
  }
}

function hasIdentity(account, identity) {
  return readIdentities(account).some(
    (linked) =>
      linked.providerName === identity.provider &&
      linked.userId === identity.providerUserId
  );
}

function hasConflictingIdentity(account, identity) {
  return readIdentities(account).some(
    (linked) =>
      linked.providerName === identity.provider &&
      linked.userId !== identity.providerUserId
  );
}

async function findNativeAccounts(gateway, email) {
  const searches = [...new Set([email.normalized, email.raw])];
  const found = new Map();

  for (const search of searches) {
    const accounts = await gateway.listAccountsByEmail(search);
    for (const account of accounts) {
      const sameEmail = normalizeEmail(account.attributes.email) === email.normalized;
      if (account.status !== EXTERNAL_PROVIDER_STATUS && sameEmail) {
        found.set(account.username, account);
      }
    }
  }

  return [...found.values()];
}

function assertEligible(account) {
  if (account.enabled !== true) {
    throw new AccountLinkingError("account_disabled");
  }

  if (account.status !== CONFIRMED_STATUS) {
    throw new AccountLinkingError("account_unconfirmed");
  }

  if (account.attributes.email_verified !== "true") {
    throw new AccountLinkingError("account_email_unverified");
  }

  return account;
}

function generateUnusablePassword() {
  return `${randomBytes(32).toString("base64url")}Aa1!`;
}

async function provisionAccount(gateway, email, log) {
  const created = await gateway.createAccount(email.normalized);

  try {
    await gateway.setPermanentPassword(created.username, generateUnusablePassword());
  } catch (error) {
    try {
      await gateway.deleteAccount(created.username);
    } catch (cleanupError) {
      log.error(JSON.stringify({ event: "account_link", outcome: "cleanup_failed", errorName: cleanupError?.name }));
    }

    throw error;
  }

  return { ...created, status: CONFIRMED_STATUS, enabled: true };
}

async function createOrFindAccount(gateway, email, log) {
  try {
    return { account: await provisionAccount(gateway, email, log), created: true };
  } catch (error) {
    if (error?.name !== "UsernameExistsException") {
      throw error;
    }
  }

  const accounts = await findNativeAccounts(gateway, email);
  if (accounts.length !== 1) {
    throw new AccountLinkingError("account_ambiguous");
  }

  try {
    return { account: assertEligible(accounts[0]), created: false };
  } catch (error) {
    if (error.code === "account_unconfirmed") {
      throw new AccountLinkingError("account_provisioning_in_progress");
    }

    throw error;
  }
}

async function linkAccount(gateway, account, identity) {
  try {
    await gateway.link({
      destinationUsername: account.username,
      provider: identity.provider,
      providerUserId: identity.providerUserId
    });
  } catch (error) {
    const refreshed = await gateway.getAccount(account.username);
    if (hasIdentity(refreshed, identity)) {
      return "already_linked";
    }

    throw error;
  }

  return "linked";
}

export async function linkExternalIdentity(event, gateway, log = console) {
  if (event?.triggerSource !== EXTERNAL_PROVIDER_TRIGGER) {
    return event;
  }

  const identity = parseProviderIdentity(event.userName);
  const email = readVerifiedEmail(event.request?.userAttributes);
  const accounts = await findNativeAccounts(gateway, email);

  if (accounts.length > 1) {
    throw new AccountLinkingError("account_ambiguous");
  }

  let account;
  let created = false;
  if (accounts.length === 1) {
    account = assertEligible(accounts[0]);
  } else {
    ({ account, created } = await createOrFindAccount(gateway, email, log));
  }

  if (hasIdentity(account, identity)) {
    log.info(JSON.stringify({ event: "account_link", outcome: "already_linked", provider: identity.provider }));
    return event;
  }

  if (hasConflictingIdentity(account, identity)) {
    throw new AccountLinkingError("account_conflicting_link");
  }

  const outcome = await linkAccount(gateway, account, identity);
  log.info(JSON.stringify({ event: "account_link", outcome: created ? "created_and_linked" : outcome, provider: identity.provider }));

  return event;
}

export function createCognitoGateway(client, sdk, userPoolId) {
  const toAccount = (username, status, enabled, attributes) => ({
    username,
    status,
    enabled,
    attributes: Object.fromEntries((attributes ?? []).map((attribute) => [attribute.Name, attribute.Value]))
  });

  return {
    async listAccountsByEmail(email) {
      const response = await client.send(
        new sdk.ListUsersCommand({
          UserPoolId: userPoolId,
          Filter: `email = "${escapeFilterValue(email)}"`,
          Limit: 10
        })
      );

      return (response.Users ?? []).map((user) =>
        toAccount(user.Username, user.UserStatus, user.Enabled, user.Attributes)
      );
    },

    async getAccount(username) {
      const response = await client.send(
        new sdk.AdminGetUserCommand({ UserPoolId: userPoolId, Username: username })
      );

      return toAccount(response.Username, response.UserStatus, response.Enabled, response.UserAttributes);
    },

    async createAccount(email) {
      const response = await client.send(
        new sdk.AdminCreateUserCommand({
          UserPoolId: userPoolId,
          Username: email,
          MessageAction: "SUPPRESS",
          UserAttributes: [
            { Name: "email", Value: email },
            { Name: "email_verified", Value: "true" }
          ]
        })
      );
      const user = response.User;

      return toAccount(user.Username, user.UserStatus, user.Enabled, user.Attributes);
    },

    async setPermanentPassword(username, password) {
      await client.send(
        new sdk.AdminSetUserPasswordCommand({
          UserPoolId: userPoolId,
          Username: username,
          Password: password,
          Permanent: true
        })
      );
    },

    async deleteAccount(username) {
      await client.send(new sdk.AdminDeleteUserCommand({ UserPoolId: userPoolId, Username: username }));
    },

    async link({ destinationUsername, provider, providerUserId }) {
      await client.send(
        new sdk.AdminLinkProviderForUserCommand({
          UserPoolId: userPoolId,
          DestinationUser: {
            ProviderName: NATIVE_PROVIDER_NAME,
            ProviderAttributeValue: destinationUsername
          },
          SourceUser: {
            ProviderName: provider,
            ProviderAttributeName: LINK_ATTRIBUTE_NAME,
            ProviderAttributeValue: providerUserId
          }
        })
      );
    }
  };
}

let cognito;

async function loadCognito() {
  cognito ??= import("@aws-sdk/client-cognito-identity-provider").then((sdk) => ({
    sdk,
    client: new sdk.CognitoIdentityProviderClient({})
  }));

  return cognito;
}

export async function handler(event) {
  if (event?.triggerSource !== EXTERNAL_PROVIDER_TRIGGER) {
    return event;
  }

  try {
    const { sdk, client } = await loadCognito();
    return await linkExternalIdentity(event, createCognitoGateway(client, sdk, event.userPoolId));
  } catch (error) {
    const reason = error instanceof AccountLinkingError ? error.code : undefined;
    console.error(JSON.stringify({ event: "account_link", outcome: "rejected", reason, errorName: error?.name }));
    throw error;
  }
}

import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  AccountLinkingError,
  createCognitoGateway,
  escapeFilterValue,
  handler,
  linkExternalIdentity,
  normalizeEmail,
  parseProviderIdentity
} from "./pre-sign-up.mjs";

const EMAIL = "Angler@Example.test";
const NORMALIZED_EMAIL = "angler@example.test";
const NATIVE_USERNAME = "native-user-name";
const GOOGLE_ID = "1234567890";
const APPLE_ID = "001596.abc123.1230";

function event(overrides = {}, userAttributes = {}) {
  return {
    version: "1",
    triggerSource: "PreSignUp_ExternalProvider",
    userPoolId: "eu-west-2_pool",
    userName: `Google_${GOOGLE_ID}`,
    request: {
      userAttributes: {
        email: EMAIL,
        email_verified: "true",
        ...userAttributes
      }
    },
    response: {},
    ...overrides
  };
}

function account(overrides = {}) {
  return {
    username: NATIVE_USERNAME,
    status: "CONFIRMED",
    enabled: true,
    attributes: { email: NORMALIZED_EMAIL, email_verified: "true" },
    ...overrides
  };
}

function withIdentities(identities, overrides = {}) {
  return account({
    ...overrides,
    attributes: {
      email: NORMALIZED_EMAIL,
      email_verified: "true",
      identities: JSON.stringify(identities)
    }
  });
}

function createLog() {
  const lines = [];
  return {
    lines,
    info: (line) => lines.push(line),
    error: (line) => lines.push(line)
  };
}

function createGateway({
  accounts = [],
  accountsAfterCreateConflict,
  createdAccount,
  createError,
  passwordError,
  linkError,
  refreshed
} = {}) {
  const calls = { list: [], create: [], password: [], remove: [], link: [], get: [] };
  let created = false;
  const gateway = {
    calls,
    async listAccountsByEmail(email) {
      calls.list.push(email);
      return created && accountsAfterCreateConflict ? accountsAfterCreateConflict : accounts;
    },
    async getAccount(username) {
      calls.get.push(username);
      return refreshed ?? account();
    },
    async createAccount(email) {
      calls.create.push(email);
      if (createError) {
        created = true;
        throw createError;
      }

      return createdAccount ?? account({ status: "FORCE_CHANGE_PASSWORD", attributes: { email, email_verified: "true" } });
    },
    async setPermanentPassword(username, password) {
      calls.password.push({ username, password });
      if (passwordError) {
        throw passwordError;
      }
    },
    async deleteAccount(username) {
      calls.remove.push(username);
    },
    async link(request) {
      calls.link.push(request);
      if (linkError) {
        throw linkError;
      }
    }
  };

  return gateway;
}

function awsError(name) {
  const error = new Error(name);
  error.name = name;
  return error;
}

async function rejection(promise) {
  try {
    await promise;
  } catch (error) {
    return error;
  }

  assert.fail("expected a rejection");
}

describe("parseProviderIdentity", () => {
  it("parses a Google user name", () => {
    assert.deepEqual(parseProviderIdentity("Google_123"), { provider: "Google", providerUserId: "123" });
  });

  it("parses an Apple user name whose id contains dots", () => {
    assert.deepEqual(parseProviderIdentity(`SignInWithApple_${APPLE_ID}`), {
      provider: "SignInWithApple",
      providerUserId: APPLE_ID
    });
  });

  it("keeps underscores that belong to the provider user id", () => {
    assert.equal(parseProviderIdentity("Google_a_b").providerUserId, "a_b");
  });

  for (const userName of ["Facebook_1", "Google_", "Google", "_1", "", undefined, "google_1"]) {
    it(`rejects ${JSON.stringify(userName)}`, () => {
      assert.throws(() => parseProviderIdentity(userName), (error) => error.code === "unsupported_identity");
    });
  }
});

describe("email handling", () => {
  it("normalises by trimming and lower-casing", () => {
    assert.equal(normalizeEmail("  Angler@Example.TEST "), NORMALIZED_EMAIL);
  });

  it("escapes quotes and backslashes for the ListUsers filter", () => {
    assert.equal(escapeFilterValue('a"b\\c'), 'a\\"b\\\\c');
  });
});

describe("linkExternalIdentity", () => {
  it("returns other trigger sources unchanged without calling Cognito", async () => {
    const gateway = createGateway();
    const input = event({ triggerSource: "PreSignUp_SignUp" });

    const result = await linkExternalIdentity(input, gateway, createLog());

    assert.equal(result, input);
    assert.deepEqual(gateway.calls.list, []);
    assert.deepEqual(gateway.calls.link, []);
  });

  for (const triggerSource of ["PreSignUp_AdminCreateUser", "PreSignUp_SignUp"]) {
    it(`passes ${triggerSource} through`, async () => {
      const gateway = createGateway();
      const input = event({ triggerSource });

      assert.equal(await linkExternalIdentity(input, gateway, createLog()), input);
      assert.deepEqual(gateway.calls.create, []);
    });
  }

  it("rejects an unsupported provider before calling Cognito", async () => {
    const gateway = createGateway();

    const error = await rejection(linkExternalIdentity(event({ userName: "Facebook_1" }), gateway, createLog()));

    assert.equal(error.code, "unsupported_identity");
    assert.deepEqual(gateway.calls.list, []);
  });

  it("rejects a missing email", async () => {
    const gateway = createGateway();

    const error = await rejection(linkExternalIdentity(event({}, { email: "  " }), gateway, createLog()));

    assert.equal(error.code, "email_missing");
    assert.deepEqual(gateway.calls.list, []);
  });

  for (const value of ["false", undefined, "", false, "TRUE"]) {
    it(`rejects an unverified IdP email (${JSON.stringify(value)})`, async () => {
      const gateway = createGateway({ accounts: [account()] });

      const error = await rejection(
        linkExternalIdentity(event({}, { email_verified: value }), gateway, createLog())
      );

      assert.equal(error.code, "email_unverified");
      assert.deepEqual(gateway.calls.link, []);
      assert.deepEqual(gateway.calls.create, []);
    });
  }

  it("accepts a boolean true email_verified", async () => {
    const gateway = createGateway({ accounts: [account()] });

    await linkExternalIdentity(event({}, { email_verified: true }), gateway, createLog());

    assert.equal(gateway.calls.link.length, 1);
  });

  it("links a Google identity to an eligible native account", async () => {
    const gateway = createGateway({ accounts: [account()] });
    const input = event();

    const result = await linkExternalIdentity(input, gateway, createLog());

    assert.equal(result, input);
    assert.deepEqual(gateway.calls.link, [
      { destinationUsername: NATIVE_USERNAME, provider: "Google", providerUserId: GOOGLE_ID }
    ]);
    assert.deepEqual(gateway.calls.create, []);
    assert.deepEqual(gateway.calls.password, []);
  });

  it("links an Apple identity to an eligible native account", async () => {
    const gateway = createGateway({ accounts: [account()] });

    await linkExternalIdentity(event({ userName: `SignInWithApple_${APPLE_ID}` }), gateway, createLog());

    assert.deepEqual(gateway.calls.link, [
      { destinationUsername: NATIVE_USERNAME, provider: "SignInWithApple", providerUserId: APPLE_ID }
    ]);
  });

  it("links a second provider to an account that already has the first", async () => {
    const existing = withIdentities([{ providerName: "Google", userId: GOOGLE_ID }]);
    const gateway = createGateway({ accounts: [existing] });

    await linkExternalIdentity(event({ userName: `SignInWithApple_${APPLE_ID}` }), gateway, createLog());

    assert.equal(gateway.calls.link.length, 1);
    assert.equal(gateway.calls.link[0].provider, "SignInWithApple");
  });

  it("searches by the normalised and the original email and ignores federated shadow users", async () => {
    const shadow = account({ username: "Google_1", status: "EXTERNAL_PROVIDER" });
    const gateway = createGateway({ accounts: [shadow, account()] });

    await linkExternalIdentity(event(), gateway, createLog());

    assert.deepEqual(gateway.calls.list, [NORMALIZED_EMAIL, EMAIL]);
    assert.equal(gateway.calls.link[0].destinationUsername, NATIVE_USERNAME);
  });

  it("does not link to a returned account whose email differs", async () => {
    const other = account({
      username: "other-user",
      attributes: { email: "other@example.test", email_verified: "true" }
    });
    const gateway = createGateway({ accounts: [other] });

    await linkExternalIdentity(event(), gateway, createLog());

    assert.equal(gateway.calls.create.length, 1);
    assert.deepEqual(gateway.calls.link.map((link) => link.destinationUsername), [NATIVE_USERNAME]);
    assert.notEqual(gateway.calls.link[0].destinationUsername, other.username);
  });

  it("is a no-op when the identity is already linked", async () => {
    const existing = withIdentities([{ providerName: "Google", userId: GOOGLE_ID }]);
    const gateway = createGateway({ accounts: [existing] });
    const log = createLog();

    await linkExternalIdentity(event(), gateway, log);

    assert.deepEqual(gateway.calls.link, []);
    assert.match(log.lines.join("\n"), /already_linked/);
  });

  it("rejects a different identity from the same provider as a conflicting link", async () => {
    const existing = withIdentities([{ providerName: "Google", userId: "someone-else" }]);
    const gateway = createGateway({ accounts: [existing] });

    const error = await rejection(linkExternalIdentity(event(), gateway, createLog()));

    assert.equal(error.code, "account_conflicting_link");
    assert.deepEqual(gateway.calls.link, []);
  });

  it("rejects an account whose identities attribute is unreadable", async () => {
    const existing = account({ attributes: { email: NORMALIZED_EMAIL, email_verified: "true", identities: "{" } });
    const gateway = createGateway({ accounts: [existing] });

    const error = await rejection(linkExternalIdentity(event(), gateway, createLog()));

    assert.equal(error.code, "account_identities_unreadable");
    assert.deepEqual(gateway.calls.link, []);
  });

  for (const [name, overrides, code] of [
    ["unconfirmed", { status: "UNCONFIRMED" }, "account_unconfirmed"],
    ["force-change-password", { status: "FORCE_CHANGE_PASSWORD" }, "account_unconfirmed"],
    ["disabled", { enabled: false }, "account_disabled"],
    [
      "email-unverified",
      { attributes: { email: NORMALIZED_EMAIL, email_verified: "false" } },
      "account_email_unverified"
    ],
    ["email-verification-missing", { attributes: { email: NORMALIZED_EMAIL } }, "account_email_unverified"]
  ]) {
    it(`rejects a ${name} matching account and neither links nor creates`, async () => {
      const gateway = createGateway({ accounts: [account(overrides)] });

      const error = await rejection(linkExternalIdentity(event(), gateway, createLog()));

      assert.equal(error.code, code);
      assert.deepEqual(gateway.calls.link, []);
      assert.deepEqual(gateway.calls.create, []);
    });
  }

  it("rejects multiple matching native accounts rather than choosing one", async () => {
    const gateway = createGateway({
      accounts: [account(), account({ username: "second-native" })]
    });

    const error = await rejection(linkExternalIdentity(event(), gateway, createLog()));

    assert.equal(error.code, "account_ambiguous");
    assert.deepEqual(gateway.calls.link, []);
    assert.deepEqual(gateway.calls.create, []);
  });

  it("creates a native account with a discarded permanent password and then links", async () => {
    const gateway = createGateway();
    const log = createLog();

    await linkExternalIdentity(event(), gateway, log);

    assert.deepEqual(gateway.calls.create, [NORMALIZED_EMAIL]);
    assert.equal(gateway.calls.password.length, 1);
    assert.equal(gateway.calls.password[0].username, NATIVE_USERNAME);
    assert.ok(gateway.calls.password[0].password.length >= 40);
    assert.deepEqual(gateway.calls.link, [
      { destinationUsername: NATIVE_USERNAME, provider: "Google", providerUserId: GOOGLE_ID }
    ]);
    assert.match(log.lines.join("\n"), /created_and_linked/);
  });

  it("generates a different password for every created account", async () => {
    const first = createGateway();
    const second = createGateway();

    await linkExternalIdentity(event(), first, createLog());
    await linkExternalIdentity(event(), second, createLog());

    assert.notEqual(first.calls.password[0].password, second.calls.password[0].password);
  });

  it("creates a separate account for an Apple relay address that matches nobody", async () => {
    const gateway = createGateway();
    const relay = "abc123@privaterelay.appleid.com";

    await linkExternalIdentity(
      event({ userName: `SignInWithApple_${APPLE_ID}` }, { email: relay }),
      gateway,
      createLog()
    );

    assert.deepEqual(gateway.calls.list, [relay]);
    assert.deepEqual(gateway.calls.create, [relay]);
    assert.equal(gateway.calls.link.length, 1);
  });

  it("removes the account it just created when the password cannot be set", async () => {
    const failure = awsError("InvalidPasswordException");
    const gateway = createGateway({ passwordError: failure });

    const error = await rejection(linkExternalIdentity(event(), gateway, createLog()));

    assert.equal(error, failure);
    assert.deepEqual(gateway.calls.remove, [NATIVE_USERNAME]);
    assert.deepEqual(gateway.calls.link, []);
  });

  it("keeps the original failure when cleanup also fails", async () => {
    const failure = awsError("InvalidPasswordException");
    const gateway = createGateway({ passwordError: failure });
    gateway.deleteAccount = async () => {
      throw awsError("ServiceException");
    };
    const log = createLog();

    const error = await rejection(linkExternalIdentity(event(), gateway, log));

    assert.equal(error, failure);
    assert.match(log.lines.join("\n"), /cleanup_failed/);
  });

  it("does not delete anything when linking fails after creation", async () => {
    const failure = awsError("InternalErrorException");
    const gateway = createGateway({ linkError: failure, refreshed: account() });

    const error = await rejection(linkExternalIdentity(event(), gateway, createLog()));

    assert.equal(error, failure);
    assert.deepEqual(gateway.calls.remove, []);
  });

  it("links to the account created by a concurrent request after UsernameExistsException", async () => {
    const winner = account();
    const gateway = createGateway({
      createError: awsError("UsernameExistsException"),
      accountsAfterCreateConflict: [winner]
    });

    await linkExternalIdentity(event(), gateway, createLog());

    assert.equal(gateway.calls.create.length, 1);
    assert.deepEqual(gateway.calls.password, []);
    assert.deepEqual(gateway.calls.link.map((link) => link.destinationUsername), [NATIVE_USERNAME]);
  });

  it("asks the caller to retry while the concurrent request is still provisioning", async () => {
    const gateway = createGateway({
      createError: awsError("UsernameExistsException"),
      accountsAfterCreateConflict: [account({ status: "FORCE_CHANGE_PASSWORD" })]
    });

    const error = await rejection(linkExternalIdentity(event(), gateway, createLog()));

    assert.equal(error.code, "account_provisioning_in_progress");
    assert.deepEqual(gateway.calls.link, []);
  });

  it("rejects when UsernameExistsException finds no single account to link", async () => {
    const gateway = createGateway({
      createError: awsError("UsernameExistsException"),
      accountsAfterCreateConflict: []
    });

    const error = await rejection(linkExternalIdentity(event(), gateway, createLog()));

    assert.equal(error.code, "account_ambiguous");
  });

  it("treats a duplicate-link failure as success only when the identity is now linked", async () => {
    const linked = withIdentities([{ providerName: "Google", userId: GOOGLE_ID }]);
    const gateway = createGateway({
      accounts: [account()],
      linkError: awsError("InvalidParameterException"),
      refreshed: linked
    });
    const input = event();

    const result = await linkExternalIdentity(input, gateway, createLog());

    assert.equal(result, input);
    assert.deepEqual(gateway.calls.get, [NATIVE_USERNAME]);
  });

  it("rethrows a link failure when the identity is not linked afterwards", async () => {
    const failure = awsError("InvalidParameterException");
    const gateway = createGateway({
      accounts: [account()],
      linkError: failure,
      refreshed: withIdentities([{ providerName: "Google", userId: "someone-else" }])
    });

    const error = await rejection(linkExternalIdentity(event(), gateway, createLog()));

    assert.equal(error, failure);
  });

  it("does not swallow unrelated lookup failures", async () => {
    const failure = awsError("TooManyRequestsException");
    const gateway = createGateway();
    gateway.listAccountsByEmail = async () => {
      throw failure;
    };

    const error = await rejection(linkExternalIdentity(event(), gateway, createLog()));

    assert.equal(error, failure);
  });

  it("does not swallow unrelated creation failures", async () => {
    const failure = awsError("NotAuthorizedException");
    const gateway = createGateway({ createError: failure });

    const error = await rejection(linkExternalIdentity(event(), gateway, createLog()));

    assert.equal(error, failure);
    assert.deepEqual(gateway.calls.link, []);
  });

  it("never logs the email, user name, provider id or password", async () => {
    const gateway = createGateway();
    const log = createLog();

    await linkExternalIdentity(event(), gateway, log);
    await linkExternalIdentity(event(), createGateway({ accounts: [account()] }), log);
    await rejection(linkExternalIdentity(event({}, { email_verified: "false" }), createGateway(), log));

    const output = log.lines.join("\n");
    assert.ok(log.lines.length >= 2);
    for (const secret of [EMAIL, NORMALIZED_EMAIL, GOOGLE_ID, NATIVE_USERNAME, gateway.calls.password[0].password]) {
      assert.equal(output.includes(secret), false, `log leaked ${secret}`);
    }
  });
});

describe("createCognitoGateway", () => {
  function fakeSdk() {
    const sdk = {};
    for (const name of [
      "ListUsers",
      "AdminGetUser",
      "AdminCreateUser",
      "AdminSetUserPassword",
      "AdminDeleteUser",
      "AdminLinkProviderForUser"
    ]) {
      sdk[`${name}Command`] = class {
        constructor(input) {
          this.name = name;
          this.input = input;
        }
      };
    }

    return sdk;
  }

  function fakeClient(responses = {}) {
    const sent = [];
    return {
      sent,
      send: async (command) => {
        sent.push(command);
        return responses[command.name] ?? {};
      }
    };
  }

  it("lists users with an escaped exact email filter scoped to the pool", async () => {
    const client = fakeClient({
      ListUsers: {
        Users: [{ Username: "u1", UserStatus: "CONFIRMED", Enabled: true, Attributes: [{ Name: "email", Value: "a@b.test" }] }]
      }
    });
    const gateway = createCognitoGateway(client, fakeSdk(), "pool-1");

    const accounts = await gateway.listAccountsByEmail('a"b\\@x.test');

    assert.deepEqual(client.sent[0].input, {
      UserPoolId: "pool-1",
      Filter: 'email = "a\\"b\\\\@x.test"',
      Limit: 10
    });
    assert.deepEqual(accounts, [
      { username: "u1", status: "CONFIRMED", enabled: true, attributes: { email: "a@b.test" } }
    ]);
  });

  it("returns no accounts when ListUsers returns none", async () => {
    const gateway = createCognitoGateway(fakeClient(), fakeSdk(), "pool-1");

    assert.deepEqual(await gateway.listAccountsByEmail("a@b.test"), []);
  });

  it("reads an account with AdminGetUser", async () => {
    const client = fakeClient({
      AdminGetUser: { Username: "u1", UserStatus: "CONFIRMED", Enabled: true, UserAttributes: [{ Name: "identities", Value: "[]" }] }
    });
    const gateway = createCognitoGateway(client, fakeSdk(), "pool-1");

    const result = await gateway.getAccount("u1");

    assert.deepEqual(client.sent[0].input, { UserPoolId: "pool-1", Username: "u1" });
    assert.equal(result.attributes.identities, "[]");
  });

  it("creates a verified-email user with message delivery suppressed", async () => {
    const client = fakeClient({
      AdminCreateUser: { User: { Username: "u1", UserStatus: "FORCE_CHANGE_PASSWORD", Enabled: true, Attributes: [] } }
    });
    const gateway = createCognitoGateway(client, fakeSdk(), "pool-1");

    const result = await gateway.createAccount("a@b.test");

    assert.deepEqual(client.sent[0].input, {
      UserPoolId: "pool-1",
      Username: "a@b.test",
      MessageAction: "SUPPRESS",
      UserAttributes: [
        { Name: "email", Value: "a@b.test" },
        { Name: "email_verified", Value: "true" }
      ]
    });
    assert.equal(result.username, "u1");
  });

  it("sets a permanent password", async () => {
    const client = fakeClient();
    const gateway = createCognitoGateway(client, fakeSdk(), "pool-1");

    await gateway.setPermanentPassword("u1", "secret");

    assert.deepEqual(client.sent[0].input, {
      UserPoolId: "pool-1",
      Username: "u1",
      Password: "secret",
      Permanent: true
    });
  });

  it("deletes a user", async () => {
    const client = fakeClient();
    const gateway = createCognitoGateway(client, fakeSdk(), "pool-1");

    await gateway.deleteAccount("u1");

    assert.deepEqual(client.sent[0].input, { UserPoolId: "pool-1", Username: "u1" });
  });

  it("links the provider user to the native user by Cognito_Subject", async () => {
    const client = fakeClient();
    const gateway = createCognitoGateway(client, fakeSdk(), "pool-1");

    await gateway.link({ destinationUsername: "u1", provider: "SignInWithApple", providerUserId: "apple-id" });

    assert.deepEqual(client.sent[0].input, {
      UserPoolId: "pool-1",
      DestinationUser: { ProviderName: "Cognito", ProviderAttributeValue: "u1" },
      SourceUser: {
        ProviderName: "SignInWithApple",
        ProviderAttributeName: "Cognito_Subject",
        ProviderAttributeValue: "apple-id"
      }
    });
  });
});

describe("handler", () => {
  it("returns other trigger sources unchanged without loading the SDK", async () => {
    const input = event({ triggerSource: "PreSignUp_SignUp" });

    assert.equal(await handler(input), input);
  });

  it("exposes a rejection as an AccountLinkingError type", () => {
    assert.ok(new AccountLinkingError("x") instanceof Error);
  });
});

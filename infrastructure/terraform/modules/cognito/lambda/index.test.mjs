import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { applyAccessTokenEmail, handler } from "./index.mjs";

function baseEvent(userAttributes = {}, accessTokenGeneration = {}) {
  return {
    version: "2",
    triggerSource: "TokenGeneration_Authentication",
    request: {
      userAttributes: {
        sub: "abc-sub",
        email: "tester@example.test",
        email_verified: "true",
        ...userAttributes
      }
    },
    response: {
      claimsAndScopeOverrideDetails: {
        accessTokenGeneration
      }
    }
  };
}

describe("applyAccessTokenEmail", () => {
  it("adds verified email to access token claims", () => {
    const result = applyAccessTokenEmail(baseEvent());

    assert.equal(
      result.response.claimsAndScopeOverrideDetails.accessTokenGeneration
        .claimsToAddOrOverride.email,
      "tester@example.test"
    );
  });

  it("treats boolean true as verified", () => {
    const result = applyAccessTokenEmail(
      baseEvent({ email_verified: true })
    );

    assert.equal(
      result.response.claimsAndScopeOverrideDetails.accessTokenGeneration
        .claimsToAddOrOverride.email,
      "tester@example.test"
    );
  });

  it("trims whitespace from the email value", () => {
    const result = applyAccessTokenEmail(
      baseEvent({ email: "  tester@example.test  " })
    );

    assert.equal(
      result.response.claimsAndScopeOverrideDetails.accessTokenGeneration
        .claimsToAddOrOverride.email,
      "tester@example.test"
    );
  });

  it("does not add email when unverified", () => {
    const result = applyAccessTokenEmail(
      baseEvent({ email_verified: "false" })
    );

    assert.equal(
      result.response.claimsAndScopeOverrideDetails.accessTokenGeneration
        .claimsToAddOrOverride,
      undefined
    );
  });

  it("does not add email when email is missing", () => {
    const result = applyAccessTokenEmail(
      baseEvent({ email: undefined })
    );

    assert.equal(
      result.response.claimsAndScopeOverrideDetails.accessTokenGeneration
        .claimsToAddOrOverride,
      undefined
    );
  });

  it("does not add email when email is whitespace", () => {
    const result = applyAccessTokenEmail(baseEvent({ email: "   " }));

    assert.equal(
      result.response.claimsAndScopeOverrideDetails.accessTokenGeneration
        .claimsToAddOrOverride,
      undefined
    );
  });

  it("does not add email when email_verified is missing", () => {
    const result = applyAccessTokenEmail(
      baseEvent({ email_verified: undefined })
    );

    assert.equal(
      result.response.claimsAndScopeOverrideDetails.accessTokenGeneration
        .claimsToAddOrOverride,
      undefined
    );
  });

  it("does not set reserved claims on the access token", () => {
    const result = applyAccessTokenEmail(baseEvent());
    const claims =
      result.response.claimsAndScopeOverrideDetails.accessTokenGeneration
        .claimsToAddOrOverride;

    assert.equal(claims.sub, undefined);
    assert.equal(claims.token_use, undefined);
    assert.equal(claims.aud, undefined);
    assert.equal(claims.iss, undefined);
    assert.equal(claims.client_id, undefined);
    assert.equal(result.request.userAttributes.sub, "abc-sub");
  });

  it("merges email with existing access-token claim overrides", () => {
    const result = applyAccessTokenEmail(
      baseEvent(
        {},
        { claimsToAddOrOverride: { custom: "keep-me" } }
      )
    );
    const claims =
      result.response.claimsAndScopeOverrideDetails.accessTokenGeneration
        .claimsToAddOrOverride;

    assert.equal(claims.custom, "keep-me");
    assert.equal(claims.email, "tester@example.test");
  });
});

describe("handler", () => {
  it("returns the event with email on the access token", async () => {
    const result = await handler(baseEvent());

    assert.equal(
      result.response.claimsAndScopeOverrideDetails.accessTokenGeneration
        .claimsToAddOrOverride.email,
      "tester@example.test"
    );
  });
});

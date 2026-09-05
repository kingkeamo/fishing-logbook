# Self-review (mandatory)

Every feature or technical ticket must complete a structured self-review **after**
implementation and tests, and **before** the work is declared complete or the PR is
treated as ready for review.

This is not optional. Green tests do **not** complete the self-review.

Act as a critical senior reviewer of the actual implementation. Inspect the **diff**,
the **GitHub issue**, the **tests**, and the **applicable `.claude` rules**. Do not
produce a checklist that merely says everything passed.

## When this applies

Implementation mode in **`product-workflow.md`**. Do not skip it for “small” tickets,
follow-up commits, or correction passes.

## Sequence (mandatory)

1. Read the issue and referenced requirements.
2. Read the applicable `.claude` rules in full.
3. Implement.
4. Add or update tests.
5. Run validation.
6. **Self-review** (this file).
7. Fix BLOCKER and SHOULD FIX findings.
8. Run **final** validation (a run from before the fixes does not count).
9. Update the PR / report completion, including the Self Review section below.

Do not stop at “Implementation complete. All tests pass.”

## How to inspect

Re-open the GitHub issue. Read the complete PR/branch diff. Re-read every `.claude`
rule that applies to the changed files — do not rely on memory.

Typical .NET features require at least:

- `csharp.md`
- `cqrs.md`
- `database.md` when persistence changed
- `testing-csharp.md`
- `testing-blazor.md` when Web/Blazor changed
- `blazor.md` when Web/Blazor changed
- `terraform.md` when infrastructure changed
- `git-commits.md` when branching, committing, or opening a PR

If two rules conflict: do not silently pick one. Resolve the contradiction when the
intended architecture is clear; otherwise report it and stop.

Ask: **did I implement what the issue asks, or what I assumed it meant?**

## Issue and acceptance criteria

Compare **every** Acceptance Criterion with the final implementation.

For each AC:

- implemented?
- tested?
- implemented at the correct layer?
- behaviour matches the wording?
- interpreted more broadly than required?

Also identify missing requirements, partial implementation, scope creep, future
tickets pulled forward, and out-of-scope functionality.

A green test suite does not excuse divergence from the issue.

## Architecture

Walk the complete request path. For CQRS:

```text
Endpoint → IMediator → Handler → I*Service → I*Repository → database / external service
```

Ask:

- Is this genuinely a Command or a Query?
- Does a read accidentally mutate state?
- Does the endpoint contain business logic?
- Does a handler call a repository directly, or contain transaction logic?
- Does Application depend on HTTP/framework concepts?
- Does Domain contain framework/request concepts?
- Do repositories receive Domain concepts where appropriate?
- Are lookup/filter objects explicit `*Args` rather than partial Domain entities?
- Is persistence leaking upward?
- Is DTO mapping at the right boundary? Is Mapster hiding meaningful Domain construction?
- Are types in the correct feature folders?

Do not accept technically working layering if the domain model has disappeared into
primitive parameter lists.

## Code smells

Review the changed code. At minimum:

- long primitive parameter lists / primitive obsession
- partially populated Domain entities used as query filters
- duplicated logic, strings, or error messages
- magic strings/numbers
- methods doing more than one job; very long methods/classes
- inappropriate static helpers; unnecessary abstractions; abstractions created only to make testing easier
- hidden side effects; GET/read operations that write
- async without cancellation propagation
- broad `catch (Exception)` that hides useful failures
- repeated result/error translation
- framework concepts leaking into Domain/Application
- nullable or mutable state that should have invariants
- incorrect aggregate boundaries
- premature generalisation or optimisation
- feature creep; dead/obsolete code; misleading names
- files in the wrong feature folder

Do not refactor for aesthetics. Change only smells that materially improve
correctness, maintainability, architecture, or consistency.

## Security and privacy

For every user-facing or server feature:

- Is `UserId` taken from trusted server identity where ownership matters?
- Can the client select another user's resource?
- Is any client-supplied identity trusted?
- Are hidden/private fields filtered **server-side** (not merely hidden in UI)?
- Are object-storage keys scoped to the authenticated user? Are presigned URLs treated as temporary?
- Are upload types/sizes adequately constrained? Report honestly if a limit is client-only.
- Could a retry/replay create duplicate records?
- Are public endpoints exposing more data than intended?
- Did the implementation expand the privacy surface unnecessarily?

Location: capture and sharing remain separate. Precise coordinates stay private unless
the ticket explicitly allows otherwise.

Authentication: never weaken JWT validation to make a feature work.

## Tests

Do not judge coverage by test count.

For every changed production method/use case, identify happy path, guard/validation,
dependency failure, not-found/existing-state, negative/no-op, security/ownership, and
important boundary values — then compare with the tests that exist.

For grouping, batching, ranges, windows, thresholds, and cumulative logic, verify that
tests prove the complete business invariant rather than only adjacent or pairwise cases.
Require a bridging/transitive counterexample where neighbouring inputs are individually
valid but their combined result would violate the invariant (for example, `0, 4, 8`
minutes for a five-minute maximum group span). Verify the exact boundary and the smallest
meaningful value beyond it, and ensure test names state the business invariant rather
than the implementation strategy.

### Mock interactions

For NSubstitute: assert the observable result **and** meaningful dependency behaviour
with `Received(n)`, `DidNotReceive()`, and `Arg.Is<T>()`. Prove calls that must not
happen. Do not use `Arg.Any` where the argument's correctness matters. Do not assert
private internals. Assert architectural dependency boundaries. Follow
**`testing-csharp.md`**.

### Repository / database

For every repository added or changed: list each public method and distinct SQL
statement. Real PostgreSQL/Testcontainers coverage must exist for each meaningful SQL
path (insert, select existing/missing, update, not-found update, upsert insert/update,
FK, unique, arrays/JSON, transaction rollback, concurrency where relevant). Application
mock tests do not replace this. Do not mock SQL.

### Web / components

For every new page/component: loading, loaded, empty/default, save/update, validation,
client failure, disabled/no-op, localisation, mocked-client interactions, and
significant multi-step workflows. Upload sequences must prove URL → bytes → record with
consistent ids/URLs/object keys/content types. Follow **`testing-blazor.md`**.

### Browser / Playwright

For every new **user-facing** feature, assess whether browser-level coverage is warranted.

Browser tests are warranted when they provide material confidence beyond lower layers
**and** can be implemented without contaminating production architecture or introducing
disproportionate test-host complexity.

Do **not** treat “every UI feature must have Playwright tests” as the rule.

The existing Playwright suite in `src/FishingLogBook.Web/BrowserTests/` is for
browser-level JS/PWA behaviour (IndexedDB, service worker, offline shell/storage).
It is not a full authenticated Blazor host. Production Web authentication is
Cognito/OIDC. Do not add test-only authentication, compile symbols, dual-host hacks,
or RCL extraction to a product feature ticket in order to force Playwright coverage.

If browser coverage is deliberately omitted, the self-review report must state:

- what browser-specific risk remains
- which lower-level tests cover the behaviour
- why Playwright is not appropriate in the current architecture
- whether a follow-up testing-infrastructure ticket is warranted

Photograph/object-storage: do not make CI depend on a live external bucket. Keep exact
upload orchestration in bUnit/API tests. Document that boundary if a real-browser
file picker cannot run in CI.

Load/save hang/failure is often cleaner at bUnit level — keep it there unless the
harness can simulate it deterministically.

### HTTP clients

A new Web API client needs HTTP-contract tests: exact URI, verb, body, deserialisation,
failure behaviour, auth-handler expectations, and presigned/external URL behaviour
where applicable. Component tests that mock the client do not prove the client.

### Validators

Invalid values, valid values, and boundaries. Max length: limit valid, limit+1 invalid.
Enums/allow-lists: every supported value valid; representative unsupported value
invalid. Required ids: empty invalid. Do not stop at one invalid case plus one happy path
when multiple rules exist.

### API

For each changed/added endpoint: unauthenticated, validation failure, not found,
dependency/service failure, success. Use repository substitutes through the real
mediator/service chain. Do **not** mock `IMediator` merely to prove `Send`.

### Integration gaps

Ask what can pass all unit tests and still fail when layers combine (SQL mapping, route
mismatch, DTO serialisation, presigned upload sequence, auth claims, service worker /
offline, concurrency, constraints). Add the smallest integration test the risk warrants.
Do not add broad end-to-end tests for behaviour already strongly covered below.

## Localisation / UI

Changed user-visible UI: localised EN and FR, tests for meaningful translated copy,
dynamic user data not translated, accessibility labels where required, existing
navigation still reachable. Do not redesign unrelated navigation unless the issue
requires it.

## Migrations

Compare schema to Domain/repository. Check FK, nullability, uniqueness, naming
(`database.md`), no accidental destructive change, no future-scope columns. Do not
rewrite a journaled DbUp migration; amend only if it has not been applied.

## CI / deployment

Does the change need a migration apply, Terraform apply, env config, secret, CI
support, Docker/Testcontainers, or Cloudflare/Fly/R2 change? The PR description must
state manual post-merge steps accurately. Never run Terraform apply automatically.

## Findings must be actionable

Classify each finding:

| Class | Meaning |
|---|---|
| **BLOCKER** | Security/privacy, AC not met, wrong architecture, destructive migration, important missing test, behaviour likely broken in production |
| **SHOULD FIX** | Meaningful maintainability/design issue, test weakness, rule violation, misleading documentation |
| **OPTIONAL** | Cosmetic / non-material |

Fix BLOCKER and SHOULD FIX before declaring the ticket complete unless the user
explicitly says otherwise. Do not bury them in the final report.

## Final validation

After self-review fixes, run the complete applicable validation again.

Currently:

```text
dotnet format FishingLogBook.sln
dotnet build FishingLogBook.sln
dotnet test FishingLogBook.sln
npm test
npm run test:browser
```

Also: Terraform fmt/validate if Terraform changed; Lambda/node tests if Lambda
changed; any other changed-stack validation.

## PR and issue, one last time

Re-read the PR description. It must accurately state what was implemented, what was
deliberately not implemented, migrations/manual applies, Terraform apply status,
important security/privacy behaviour, test coverage, and issue closure.

Re-read the issue. Confirm every AC is satisfied and tested.

## Report format (required on every completed ticket)

```text
Self review:
- Issue/AC: PASS / findings
- Architecture/CQRS: PASS / findings
- Rules: PASS / findings
- Security/privacy: PASS / findings
- Database/migrations: PASS / N/A / findings
- Tests/coverage: PASS / findings
- Browser: PASS / N/A (reason) / findings
- Web/UI/localisation: PASS / N/A / findings
- Code smells: PASS / findings
- CI/deployment: PASS / findings

Findings discovered during self-review:
1. ...

Fixes made:
1. ...

Remaining deliberate gaps:
- ...
```

Do not write “None” automatically. Inspect first.

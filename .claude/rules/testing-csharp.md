---
paths:
  - "tests/FishingLogBook.UnitTests/**/*.cs"
  - "tests/FishingLogBook.IntegrationTests/**/*.cs"
---

# C# Testing Conventions (Unit & Integration)

Blazor / bUnit tests are in **`testing-blazor.md`**.

## Test projects

| Project | Scope | References |
|---------|-------|------------|
| `FishingLogBook.UnitTests` | Application services, Domain logic, Shared serialisation, Infrastructure logic that needs no live DB | Domain, Shared, Application, Infrastructure |
| `FishingLogBook.IntegrationTests` | API endpoints via `WebApplicationFactory<Program>` (repositories mocked — no live DB in CI) | Api, Shared, Application |

## Production code — ask before changing (mandatory)

**Never modify production code under `src/` without explicit user approval first.** If a
test fails:

1. **Fix the test** (assertions, test data, mocks) — default action.
2. If the failure reveals a genuine production issue, **propose** the change and **stop** —
   wait for approval before editing `src/`.

Allowed without asking: files under `tests/`, `.editorconfig`, and rule docs when the task
is test/tooling work only.

## Purpose: tests must catch code changes

A unit test must **fail when production behaviour changes**. Tests that pass regardless of
what the code does (asserting only `IsSuccess`, or `Received(Arg.Any<T>())` when the code
passes specific values) are not protecting the codebase.

### Sufficient assertions (every test)

Assert more than "no exception":

| Layer | Minimum assertions |
|-------|-------------------|
| Application services | Returned data/outcome **and** `Received()` / `DidNotReceive()` with `Arg.Is<>` on meaningful inputs |
| API endpoints (integration) | HTTP status code **and** response body shape/values |

After writing a test, mentally invert one line of production behaviour it covers — the test
must fail. If it would not, add assertions.

## Stack

xUnit + NSubstitute + FluentAssertions (pinned to the last Apache-2.0 release, 7.x) +
coverlet.collector. Integration tests use `Microsoft.AspNetCore.Mvc.Testing`.

- `Fact` is available via the project's global `Using Include="Xunit"`.
- Never hand-roll fakes/stubs — use NSubstitute (`Substitute.For<T>()`, `Returns`,
  `Arg.Any<T>()`, `Arg.Is<T>(...)`, `Received()`, `DidNotReceive()`).

## Naming & structure

- One test class per type/behaviour under test: `{TypeUnderTest}Tests`.
- Test methods: `{Method}_Should{ExpectedBehaviour}_When{Condition}`.
- Mirror the production folder structure under the test project.
- NSubstitute substitutes are fields on the class (no `[SetUp]`; use the constructor).

## Arrange / Act / Assert

Every test method uses exactly these section comments — no other comments:

```csharp
// Arrange
// Act
// Assert
```

## Application service tests (UnitTests)

- Instantiate the service directly with `Substitute.For<I*Repository>()`.
- Assert the returned contract/DTO **and** verify the repository was called with the
  expected arguments (`Received(1)` + `Arg.Is<>`), or `DidNotReceive()` for early exits.

## API integration tests (IntegrationTests)

- Use a `WebApplicationFactory<Program>` subclass. Override `ConfigureWebHost` to:
  - set an environment and in-memory config that disables startup migrations
    (`Database:RunMigrationsOnStartup=false`) and supplies an empty connection string,
  - replace real repositories (`RemoveAll<ISystemRepository>()` + add a NSubstitute mock)
    so tests never touch a real database.
- Assert `response.StatusCode` **and** the deserialised body. Cover success and failure
  paths (e.g. healthy record → 200; missing record and repository exception → 503).

## Coverage

Aim for high, meaningful coverage of the code under test (service methods, endpoint
handlers, logic-bearing helpers). Do not write filler tests purely to raise a coverage
number, and do not widen production visibility or use reflection just to reach a branch.

## Before writing new tests

Read the existing test classes of the same type before adding a new one and match the
pattern.

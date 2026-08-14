---
paths:
  - "tests/FishingLogBook.Tests.Common/**/*.cs"
  - "tests/FishingLogBook.Shared.Tests/**/*.cs"
  - "tests/FishingLogBook.Application.Tests/**/*.cs"
  - "tests/FishingLogBook.Infrastructure.Tests/**/*.cs"
  - "tests/FishingLogBook.Db.Migrations.Tests/**/*.cs"
  - "tests/FishingLogBook.Api.Tests/**/*.cs"
---

# C# Testing Conventions (Unit & Integration)

Blazor / bUnit tests are in **`testing-blazor.md`**.

## Issue test requirements (mandatory)

Tests are part of every feature and are not optional follow-up work.

For every GitHub issue:

1. Review every Acceptance Criterion.
2. Add automated tests for each criterion where technically practical.
3. Prefer behavioural tests over implementation-detail tests.
4. Follow this file and **`testing-blazor.md`**.
5. Use the testing libraries already selected by the solution.
6. Do not introduce a new testing framework without a specific reason.
7. Do not add meaningless tests purely to increase coverage.
8. Test failure paths where Acceptance Criteria describe failure behaviour.
9. Offline features must test offline persistence and reconnection behaviour.
10. Location features must test permission granted, denied and unavailable scenarios where relevant.
11. Synchronisation tests must verify retry behaviour does not create duplicate server records.
12. Existing tests must remain green.
13. Run the appropriate test projects before considering implementation complete.

## Test projects (one per production project)

Each production project has its **own** `FishingLogBook.<Project>.Tests` project; shared
builders/fixtures live in `FishingLogBook.Tests.Common`. Do **not** add tests to a
different project's test project.

| Project | Scope | References |
|---------|-------|------------|
| `FishingLogBook.Tests.Common` | Shared test builders/fixtures — **no tests** (plain class library) | Domain, Shared |
| `FishingLogBook.Shared.Tests` | DTO / contract serialisation | Shared, Tests.Common |
| `FishingLogBook.Application.Tests` | CQRS handlers + FluentValidation validators | Application, Tests.Common |
| `FishingLogBook.Infrastructure.Tests` | Infrastructure logic that needs no live DB | Infrastructure, Tests.Common |
| `FishingLogBook.Db.Migrations.Tests` | Migration ordering (`FilenameOnlyScriptComparer`) and engine helpers | Db.Migrations, Tests.Common |
| `FishingLogBook.Api.Tests` | API endpoints via `WebApplicationFactory<Program>` (repositories mocked — no live DB in CI) | Api, Shared, Application, Tests.Common |

`Domain`, `DependencyInjection`, and `Db.Migrations.App` have no dedicated test project yet
(POCOs / wiring / console host). Add one as `FishingLogBook.<Project>.Tests` when they gain
testable logic.

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
| CQRS handlers | Outcome (`IsSuccess` / `IsFailure`, `ErrorMessage`, returned data) **and** `Received()` / `DidNotReceive()` with `Arg.Is<>` on meaningful inputs |
| Validators | `ShouldHaveValidationErrorFor` / `ShouldNotHaveAnyValidationErrors` for the rule under test |
| API endpoints (integration) | HTTP status code **and** response body shape/values |

**Anti-patterns (insufficient):** `result.IsSuccess.Should().BeTrue()` only; `Received(Arg.Any<T>())` when the SUT passes specific values; a success test with no `Received()` verification.

After writing a test, mentally invert one line of production behaviour it covers — the test
must fail. If it would not, add assertions.

## Stack

xUnit + NSubstitute + AwesomeAssertions (the Apache-2.0 fork of FluentAssertions; use
`using AwesomeAssertions;`) + coverlet.collector. Integration tests use
`Microsoft.AspNetCore.Mvc.Testing`. Handler tests instantiate the handler directly.
Validator tests use **FluentValidation.TestHelper**.

- `Fact` is available via the project's global `Using Include="Xunit"`.
- Never hand-roll fakes/stubs — use NSubstitute (`Substitute.For<T>()`, `Returns`,
  `Arg.Any<T>()`, `Arg.Is<T>(...)`, `Received()`, `DidNotReceive()`).
- NSubstitute fields: `Mock*` prefix (`MockSystemRepository`) — same as rah-portal.

## Naming & structure — `WhenTesting` convention (mandatory)

Follow the rah-portal / RefAssured `WhenTesting` layout. Mirror production namespaces:

```text
tests/FishingLogBook.Application.Tests/{Feature}/Commands/{Name}CommandTests/WhenTestingHandle.cs
tests/FishingLogBook.Application.Tests/{Feature}/Commands/{Name}CommandValidatorTests/WhenTestingValidate.cs
tests/FishingLogBook.Application.Tests/{Feature}/Queries/{Name}QueryTests/WhenTestingHandle.cs
tests/FishingLogBook.Api.Tests/SystemEndpointsTests/WhenTestingGetHealth.cs
```

- **One folder per system-under-test:** `{Sut}Tests/` (e.g. `AddCatchCommandTests/`).
- **Base class** `Base{Sut}Test` in that folder holds the SUT and its NSubstitute
  dependencies as `protected` fields, constructed in the constructor (no `[SetUp]`):

```csharp
public class BaseAddCatchCommandTest
{
    protected readonly ICatchRepository MockCatchRepository = Substitute.For<ICatchRepository>();
    protected readonly AddCatchHandler Sut;

    protected BaseAddCatchCommandTest()
    {
        Sut = new AddCatchHandler(MockCatchRepository);
    }
}
```

- **One class per method/behaviour under test:** `WhenTesting{MethodOrBehaviour}`, inheriting
  the base (e.g. `WhenTestingHandle : BaseAddCatchCommandTest`).
  The condition lives in the class name (`WhenTestingReloadedFromTheStore`), not in the
  method name.
- **Test methods:** `ItShould{ExpectedOutcome}` only
  (e.g. `ItShouldKeepQueuedEvents`, `ItShouldReturnDegradedWithNoName`).
- **Do not use underscores in test method names.** Never write
  `ItShouldKeepQueuedEvents_WhenReloadedFromTheStore`. If two conditions need separate
  outcomes, use two `WhenTesting{Condition}` classes.
- Mirror the production namespace/type under the test project via these folders.

## Arrange / Act / Assert

Every test method uses exactly these section comments — no other comments:

```csharp
// Arrange
// Act
// Assert
```

## Handler tests (Application.Tests)

- Instantiate the handler directly with `Substitute.For<I*Service>()` — do not run the
  full MediatR pipeline unless testing integration.
- Call `await Sut.Handle(command, CancellationToken.None)`.
- Assert `result.IsFailure` / `IsSuccess`, `ErrorMessage`, `ValidationErrors`, and success
  data.
- **Every test** must verify mocks with `Received()` / `DidNotReceive()` and `Arg.Is<T>(...)`.
- Build domain inputs with the shared builders from `FishingLogBook.Tests.Common`.
- API tests still mock **repositories** (not `IMediator`) so the real handler and service
  run inside `WebApplicationFactory`.

## Service tests (Application.Tests)

- Extend `Base{Service}Test`. Construct the service with `Substitute.For<I*Repository>()`
  and a Mapster `TypeAdapterConfig` when the service calls `.Adapt<T>()`.
- Assert FluentResults `IsSuccess` / `IsFailed` **and** `Received()` / `Arg.Is<>`.

## Validator tests (Application.Tests)

- Separate folder: `{Command}ValidatorTests/WhenTestingValidate.cs`
- Use `_validator.TestValidate(command)` from FluentValidation.TestHelper
- Assert with `ShouldNotHaveAnyValidationErrors()` / `ShouldHaveValidationErrorFor(...)`
- Do **not** add `RuleFor(x => x).NotNull()` on the command — FluentValidation rejects
  `Validate(null)` before that rule runs. Validate nested properties instead.

## API integration tests (Api.Tests)

- Use a `WebApplicationFactory<Program>` subclass. Override `ConfigureWebHost` to:
  - set an environment and in-memory config that supplies an empty connection string,
  - replace real repositories (`RemoveAll<ISystemRepository>()` + add a NSubstitute mock)
    so tests never touch a real database.
- Assert `response.StatusCode` **and** the deserialised body. Cover success and failure
  paths (e.g. healthy record → 200; missing record and repository exception → 503).

## DI container tests (Api.Tests)

Unit tests construct services directly, so they will not catch a missing
`AddScoped` / `AddSingleton` in `AddFishingLogBook`. Keep a
`DependencyInjectionTests/` suite that:

- builds the real API host (`WebApplicationFactory<Program>`) with `ValidateOnBuild`
  and `ValidateScopes`,
- reflects endpoint handler methods under `FishingLogBook.Api.Endpoints` and
  `GetRequiredService`s every DI parameter (skip `CancellationToken`, HTTP types,
  primitives, and Shared/Domain bindable DTOs).

This is the safety net for “all tests passed but the app dies on first request”.

## Coverage

Aim for **100% line and branch coverage** of the handler, validator, or endpoint under
test. Do not write filler tests purely to raise a number, and do not widen production
visibility or use reflection just to reach a branch.

## AI feature definition of done (mandatory)

When you add or change production code, add or update tests in the **matching test
project in the same task**.

| Production project | Test project |
|--------------------|--------------|
| `FishingLogBook.Application` | `FishingLogBook.Application.Tests` (handler + validator) |
| `FishingLogBook.Api` | `FishingLogBook.Api.Tests` |
| `FishingLogBook.Infrastructure` | `FishingLogBook.Infrastructure.Tests` |
| `FishingLogBook.Shared` | `FishingLogBook.Shared.Tests` |
| `FishingLogBook.Web` | `FishingLogBook.Web.Tests` — see **`testing-blazor.md`** |

Before marking complete: run targeted `dotnet test` for every test project you changed.

## Before writing new tests

Read at least **5 existing test classes of the same type** (handler, validator, endpoint)
before writing a new one, and match the pattern.

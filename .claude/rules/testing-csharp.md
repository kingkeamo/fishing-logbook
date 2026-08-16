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
| `FishingLogBook.Infrastructure.Tests` | Infrastructure logic; database-backed uniqueness/transaction/concurrency tests when an issue requires them | Infrastructure, Tests.Common |
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
| Application services | FluentResults outcome **and** `Received()` / `DidNotReceive()` with `Arg.Is<>` on meaningful inputs |
| Validators | `ShouldHaveValidationErrorFor` / `ShouldNotHaveAnyValidationErrors` for the rule under test |
| API endpoints (integration) | HTTP status **and** response body **and**, when repositories are substituted, meaningful `Received()` / `DidNotReceive()` on those dependencies |
| Infrastructure unit tests | Observable result **and** `Received()` / `DidNotReceive()` when collaborators are substituted |
| Web / bUnit | See **`testing-blazor.md`** |

**Anti-patterns (insufficient):** `result.IsSuccess.Should().BeTrue()` only; `Received(Arg.Any<T>())` when the SUT passes specific values; `Received()` without a call count; a success test with no `Received(n)` verification; inferring a dependency was not called only because the return value looks correct.

After writing a test, mentally invert one line of production behaviour it covers — the test
must fail. If it would not, add assertions.

## Dependency verification (mandatory)

Whenever the System Under Test calls a mocked/substituted dependency, the test must
normally verify that interaction.

A good test proves **both**:

1. Observable result/behaviour.
2. The meaningful dependency interactions that produced that behaviour.

This applies to CQRS handler tests, application service tests, API tests where
dependencies are substituted, Infrastructure unit tests, Web/bUnit component tests,
Web service tests, and any other test using NSubstitute collaborators.

Do **not** add interaction assertions to tests that have no mocked dependencies, or to
true integration/state tests whose behaviour is the persisted or external result
(Testcontainers, real HTTP, in-memory store used as the system under test).

### Meaningful input assertions

Use `Received(n)`, `DidNotReceive(...)`, and `Arg.Is<T>(...)` to prove meaningful
values cross architectural boundaries correctly.

`Received()` **without a count is forbidden**. It only proves the dependency was
called at least once, so extra unexpected calls still pass. Always pass the expected
number: `Received(1)`, `Received(2)`, …

`DidNotReceive()` is the correct assertion when the dependency must not be called.

```csharp
// BAD — extra GetAllAsync calls would still pass
await store.Received().GetAllAsync(Arg.Any<CancellationToken>());

// GOOD
await store.Received(2).GetAllAsync(Arg.Any<CancellationToken>());
```

`Arg.Any<T>()` is acceptable only where the value genuinely does not matter to the
behaviour under test.

Typically acceptable:

- `Arg.Any<CancellationToken>()`
- `Arg.Any<T>()` on `DidNotReceive()` when proving the dependency was **not invoked at
  all** (any argument would be wrong)

Typically **not** acceptable on `Received()` when the SUT is expected to
construct/pass the value:

- `Arg.Any<User>()`, `Arg.Any<UserIdentity>()`, `Arg.Any<Catch>()`
- `Arg.Any<Guid>()` for UserId / record identity
- `Arg.Any<string>()` for Provider / Subject / Email
- `Arg.Any<Dto>()`, `Arg.Any<Model>()`, `Arg.Any<Command>()`, `Arg.Any<Request>()`

Instead assert the relevant properties.

```csharp
// BAD
await MockRepository.Received(1)
    .CreateAsync(
        Arg.Any<User>(),
        Arg.Any<UserIdentity>(),
        Arg.Any<CancellationToken>());

// GOOD
await MockRepository.Received(1)
    .CreateAsync(
        Arg.Is<User>(user =>
            user.Id == expectedUserId &&
            user.Email == expectedEmail),
        Arg.Is<UserIdentity>(identity =>
            identity.UserId == expectedUserId &&
            identity.Provider == expectedProvider &&
            identity.Subject == expectedSubject),
        Arg.Any<CancellationToken>());
```

`Returns(Arg.Any<T>())` in Arrange is a stub. The **Assert** `Received()` still needs
`Arg.Is<>` for values the SUT must pass.

### Negative interaction assertions

Where behaviour requires a dependency **not** to be invoked, prove it with
`DidNotReceive()`. Do not infer "not called" from the return value alone.

Examples:

- validation failure → service / repository not called
- lookup finds an existing identity → create not called
- initial persistence failure → later persistence operation not called
- unauthenticated / unauthorized request → protected repository not called
- missing catch → photograph upload URL not created

### Do not over-assert implementation details

This rule does **not** mean asserting every internal method call. Only verify
interactions across a **substituted dependency boundary** that are part of the
behavioural contract. Tests should remain resilient to internal refactoring.

GOOD: the service passes the correct `User` domain object to `IUserIdentityRepository`.

NOT REQUIRED: asserting calls between private helpers inside the same service.

## Stack

Use only this stack:

- xUnit
- NSubstitute
- **AwesomeAssertions** (`using AwesomeAssertions;`)
- FluentValidation.TestHelper
- coverlet.collector
- `Microsoft.AspNetCore.Mvc.Testing` for API integration tests

Do **not** add the FluentAssertions package. Do not write `using FluentAssertions;`.
AwesomeAssertions is the assertion library.

Handler tests belong in `FishingLogBook.Application.Tests`. They instantiate the
handler directly with `Substitute.For<I*Service>()` — they do not run the MediatR
pipeline unless the test is specifically covering pipeline integration.

- `Fact` is available via the project's global `Using Include="Xunit"`.
- Never hand-roll fakes/stubs — use NSubstitute (`Substitute.For<T>()`, `Returns`,
  `Arg.Any<T>()`, `Arg.Is<T>(...)`, `Received(n)`, `DidNotReceive()`).
  `Received()` without a count is forbidden.
- Use `Arg.Is<>` (or the exact value) rather than `Arg.Any<>` when the actual passed
  value matters. The only hand-rolled doubles that remain acceptable are ones
  NSubstitute cannot replace cleanly: in-memory persistence used **as the system
  under test's backing store**, bUnit `NavigationManager` subclasses, `IJSRuntime`
  timeout/interop doubles, and `ILogger` spies that capture scopes. Collaborators
  of a higher SUT must still be NSubstitute.
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
- **Every test** must verify mocks with `Received()` / `DidNotReceive()` and `Arg.Is<T>(...)`
  for meaningful inputs (see **Dependency verification**). When the handler calls
  `.Adapt<TArgs>()`, `Arg.Is<TArgs>` on the adapted fields is what proves Mapster copied
  the command — do not replace that with `Arg.Any<TArgs>()`.
- Matching property names Adapt by convention without scanning. If an `IRegister`
  customizes the pair, register it on `TypeAdapterConfig.GlobalSettings` in the test
  constructor before `Handle` — handlers call `source.Adapt<T>()`, which uses
  GlobalSettings, not a local config:

```csharp
((IRegister)new UserMappingRegistration()).Register(TypeAdapterConfig.GlobalSettings);
```

- Build domain inputs with the shared builders from `FishingLogBook.Tests.Common`.
- API tests still mock **repositories** (not `IMediator`) so the real handler and service
  run inside `WebApplicationFactory`.

## Service tests (Application.Tests)

- Extend `Base{Service}Test`. Construct the service with `Substitute.For<I*Repository>()`
  and a Mapster `TypeAdapterConfig` when the service calls `.Adapt<T>()` or injects
  `IMapper` (same as rah-portal):

```csharp
var config = new TypeAdapterConfig();
((IRegister)new UserMappingRegistration()).Register(config);
Mapper = new Mapper(config);
```

- Assert FluentResults `IsSuccess` / `IsFailed` **and** `Received()` / `DidNotReceive()`
  with `Arg.Is<>` on meaningful inputs (see **Dependency verification**).

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
- Mock **repositories**, not `IMediator`, so the real handler and application service
  run inside the test host. Do **not** mock `IMediator` merely to verify `Send()`.
- Assert `response.StatusCode` **and** the deserialised body. Cover success and failure
  paths (e.g. healthy record → 200; missing record and repository exception → 503).
- When the endpoint/use case is expected to invoke a substituted repository (or other
  substituted dependency), also verify that call with `Received()` / `Arg.Is<>`.
  When it must not (unauthorized, validation failure, missing prerequisite), use
  `DidNotReceive()`.

## Database-backed infrastructure tests

API tests mock repositories and must not require live PostgreSQL in CI.

When an issue requires proving uniqueness, transactions, or concurrency, add those
tests in `FishingLogBook.Infrastructure.Tests` against a real database
(Testcontainers PostgreSQL is acceptable). Do not mock away the behaviour that must
be trusted.

Where an Infrastructure **unit** test has mocked collaborators, apply **Dependency
verification**: assert meaningful `Received()` / `DidNotReceive()` and `Arg.Is<>`.
Do **not** force interaction assertions into true integration tests where no
dependency is mocked and the observable persisted/external state is the behaviour
under test.

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

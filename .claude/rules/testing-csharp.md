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

## Test projects (one per production project)

Each production project has its **own** `FishingLogBook.<Project>.Tests` project; shared
builders/fixtures live in `FishingLogBook.Tests.Common`. Do **not** add tests to a
different project's test project.

| Project | Scope | References |
|---------|-------|------------|
| `FishingLogBook.Tests.Common` | Shared test builders/fixtures — **no tests** (plain class library) | Domain, Shared |
| `FishingLogBook.Shared.Tests` | DTO / contract serialisation | Shared, Tests.Common |
| `FishingLogBook.Application.Tests` | Application services | Application, Tests.Common |
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
| Application services | Returned data/outcome **and** `Received()` / `DidNotReceive()` with `Arg.Is<>` on meaningful inputs |
| API endpoints (integration) | HTTP status code **and** response body shape/values |

After writing a test, mentally invert one line of production behaviour it covers — the test
must fail. If it would not, add assertions.

## Stack

xUnit + NSubstitute + AwesomeAssertions (the Apache-2.0 fork of FluentAssertions; use
`using AwesomeAssertions;`) + coverlet.collector. Integration tests use
`Microsoft.AspNetCore.Mvc.Testing`.

- `Fact` is available via the project's global `Using Include="Xunit"`.
- Never hand-roll fakes/stubs — use NSubstitute (`Substitute.For<T>()`, `Returns`,
  `Arg.Any<T>()`, `Arg.Is<T>(...)`, `Received()`, `DidNotReceive()`).

## Naming & structure — `WhenTesting` convention (mandatory)

Follow the RefAssured-style `WhenTesting` layout:

- **One folder per system-under-test:** `{Sut}Tests/` (e.g. `SystemStatusServiceTests/`).
- **Base class** `Base{Sut}Test` in that folder holds the SUT and its NSubstitute
  dependencies as `protected` fields, constructed in the constructor (no `[SetUp]`):

```csharp
public class BaseSystemStatusServiceTest
{
    protected readonly ISystemRepository SystemRepository = Substitute.For<ISystemRepository>();
    protected readonly SystemStatusService Sut;

    protected BaseSystemStatusServiceTest()
    {
        Sut = new SystemStatusService(SystemRepository);
    }
}
```

- **One class per method/behaviour under test:** `WhenTesting{MethodOrBehaviour}`, inheriting
  the base (e.g. `WhenTestingGetDatabaseStatusAsync : BaseSystemStatusServiceTest`).
- **Test methods:** `ItShould{ExpectedOutcome}` — append `_When{Condition}` when one
  `WhenTesting` class covers several conditions
  (e.g. `ItShouldReturnDegradedWithNoName_WhenNoRecordExists`).
- Mirror the production namespace/type under the test project via these folders.

## Arrange / Act / Assert

Every test method uses exactly these section comments — no other comments:

```csharp
// Arrange
// Act
// Assert
```

## Application service tests (Application.Tests)

- The base test constructs the service with `Substitute.For<I*Repository>()` dependencies.
- Assert the returned contract/DTO **and** verify the repository was called with the
  expected arguments (`Received(1)` + `Arg.Is<>`), or `DidNotReceive()` for early exits.
- Build domain inputs with the shared builders from `FishingLogBook.Tests.Common`.

## API integration tests (Api.Tests)

- Use a `WebApplicationFactory<Program>` subclass. Override `ConfigureWebHost` to:
  - set an environment and in-memory config that supplies an empty connection string,
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

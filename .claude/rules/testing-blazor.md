---
paths:
  - "tests/FishingLogBook.Web.Tests/**/*.cs"
  - "src/FishingLogBook.Web/**/*.razor"
  - "src/FishingLogBook.Web/**/*.razor.cs"
---

# Blazor & Web Testing Conventions

Production Blazor patterns are in **`blazor.md`**. Server-side C# test rules are in
**`testing-csharp.md`**.

## AI feature definition of done (mandatory)

When you add or change production code under `src/FishingLogBook.Web/` (`.razor`,
`.razor.cs`, `Features/`, `Browser/`, `Components/`, `Layouts/`, `Configuration/`),
you must **in the same task** add or update tests in `tests/FishingLogBook.Web.Tests/`.
Application/Api tests do not replace Web tests for code in `FishingLogBook.Web`.

## Tests mirror production (mandatory)

`FishingLogBook.Web.Tests` follows the same feature-first layout as `FishingLogBook.Web`.

**Do not** create new top-level `SomethingTests` directories. Place tests under the
matching production feature (or shared) path.

Canonical ownership map:

```text
FishingLogBook.Web.Tests/
    Features/
        Catch/
            Pages/
                CatchLogTests.cs
            Offline/
                CatchStoreTests.cs
                CatchSynchroniserTests.cs

        Diagnostics/
            Pages/
            Services/
            Storage/
            TestSupport/          → Diagnostics-only fakes (e.g. MemoryDiagnosticEventStore)

        SystemStatus/
            Pages/

    Components/
        LanguageSwitcherTests/
    Browser/
        Location/
    Layouts/
        MainLayoutTests/
    Localization/
        CultureMatcherTests/
    DependencyInjection/
    TestSupport/                  → genuinely shared fixtures (e.g. TestCulture)
```

This repository keeps the `WhenTesting` convention, so a leaf folder is `{Thing}Tests/`
containing `Base{Thing}Test` plus `WhenTesting{Method}` files — not a single flattened
`CatchLogTests.cs` unless the suite is truly one class.

Use `{Thing}Tests` as the **leaf folder name** (not `{Thing}`) so the test namespace does
not hide the production type (`CS0118`). Example:

```text
Features/Catch/Pages/CatchLogTests/
    BaseCatchLogTest.cs
    WhenTestingSave.cs
```

Namespace: `FishingLogBook.Web.Tests.Features.Catch.Pages.CatchLogTests`.

If the feature folder itself shares a type name (e.g. `Features/SystemStatus` vs page
`SystemStatus`), use a using alias for the component type.

Feature-specific fakes/builders belong with that feature's tests. Only genuinely shared
infrastructure belongs in the project-level `TestSupport/` folder.

## Production code — ask before changing (mandatory)

Same rule as **`testing-csharp.md`**: never modify `src/` production code to make a test
pass without explicit approval. Fix the test first; otherwise propose the production change
and stop.

## Purpose: component tests must catch code changes

A test that only checks "renders without error" is not useful. Assert observable behaviour
that would break if the UI logic changed:

- rendered markup / text content / element presence (`Find`, `TextContent`),
- mocked client services: `Received(n)` / `DidNotReceive()` with `Arg.Is<>` when the
  component passes specific values. `Received()` without a count is forbidden.

A component test that triggers a service/client operation must assert **both**:

1. the resulting rendered/UI behaviour, **and**
2. the exact client/service call made by the component.

Do not consider a Web test sufficient merely because the markup looks correct if the
component's responsibility includes calling a dependency.

## DI container tests

Keep a `DependencyInjection/` suite that builds `AddFishingLogBookWeb` with
`ValidateOnBuild` / `ValidateScopes`, stubs framework services (`IJSRuntime`,
`NavigationManager`), then `GetRequiredService`s every `[Inject]` property on
`IComponent` types in `FishingLogBook.Web`. Register new Web services in
`AddFishingLogBookWeb`, not only in `Program.cs`, or this test will miss them.

## Stack

- **bUnit 2.x** (`BunitContext`) + **xUnit** + **NSubstitute** + **AwesomeAssertions**.
- Test project `FishingLogBook.Web.Tests` references `FishingLogBook.Web` and
  `FishingLogBook.Shared`.

## MudBlazor + bUnit disposal (important)

MudBlazor registers services that are **`IAsyncDisposable`-only** (e.g.
`KeyInterceptorService`). bUnit's synchronous context disposal throws on these. Therefore:

- Do **not** inherit `BunitContext` on the test class (xUnit disposes it synchronously).
- Instead, put a `CreateContext` factory on the `Base{Component}Test` class and create the
  context **inside each `async Task` test** with `await using`, so it is disposed
  asynchronously:

```csharp
// Features/SystemStatus/Pages/SystemStatusTests/BaseSystemStatusTest.cs
public class BaseSystemStatusTest
{
    protected static BunitContext CreateContext(ISystemStatusClient client)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(client);
        return context;
    }
}

// Features/SystemStatus/Pages/SystemStatusTests/WhenTestingRender.cs
public class WhenTestingRender : BaseSystemStatusTest
{
    [Fact]
    public async Task ItShouldShowOnline()
    {
        // Arrange
        var client = Substitute.For<ISystemStatusClient>();
        client.GetApiHealthAsync(Arg.Any<CancellationToken>()).Returns(new HealthDto("Healthy"));
        await using var context = CreateContext(client);

        // Act
        var cut = context.Render<SystemStatus>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#status-row-api").TextContent.Should().Contain("Online"));
        await client.Received(1).GetApiHealthAsync(Arg.Any<CancellationToken>());
    }
}
```

## Required setup

- `JSInterop.Mode = JSRuntimeMode.Loose` (MudBlazor and Blazor make JS calls during render).
- `Services.AddMudServices()` and a NSubstitute mock for each `I*Client` the component
  injects, with default `Returns(...)` for anything called during `OnInitializedAsync`.
- Render with `context.Render<TComponent>(...)`; query with stable `id` selectors; use
  `WaitForAssertion` for async initialisation.

## Naming & AAA (WhenTesting convention)

- Place tests under the matching production feature path. The leaf folder is
  `{Component}Tests/` (or `{Service}Tests/`) with a `Base{Component}Test` (holds
  `CreateContext`) and `WhenTesting{Method}` classes inheriting the base. Do not put
  that `{Thing}Tests/` folder at the test project root.
- The `WhenTesting` class corresponds to the public behaviour being exercised
  (`WhenTestingSave`, `WhenTestingGetEmail`, `WhenTestingRender`), not every
  individual UI state. Put all scenarios for that behaviour in the same class.
  Do not explode bUnit tests into one file/class per rendered scenario.
- Test methods: `ItShould{ExpectedBehaviour}()` or
  `ItShould{ExpectedBehaviour}When{Scenario}()` with **no underscores**.
- Order tests guard/failure → negative → happy path last, same as
  **`testing-csharp.md`**.
- Every test uses exactly the `// Arrange` / `// Act` / `// Assert` section comments.
- See **`testing-csharp.md`** for the full `WhenTesting` convention.

## Dependency assertions (every test)

Follow **`testing-csharp.md` → Dependency verification**. Verify the mocked client
services, not only markup. Both success and failure paths need `Received(n)` /
`DidNotReceive()` with `Arg.Is<>` for meaningful inputs. `Received()` without a
count is forbidden — extra unexpected calls must fail. `Arg.Any<CancellationToken>()`
is acceptable. `Arg.Any<T>()` is not acceptable on `Received(n)` for DTOs, models, ids,
or other values the component is expected to pass.

After clicking Save, for example:

- assert success/error UI
- verify `SaveAsync` was called once
- verify the DTO/model passed to `SaveAsync` contains the expected values

```csharp
cut.WaitForAssertion(() => cut.Find("#save-test-catch-spinner").Should().BeEmpty());
await store.Received(1).SaveAsync(
    Arg.Is<TestCatchModel>(testCatch =>
        testCatch.SpeciesName == "Pike" &&
        testCatch.SyncStatus == SyncStatus.SavedLocally),
    Arg.Any<CancellationToken>());
```

For failure/guard paths, assert the UI state **and** use `DidNotReceive()` where the
dependency must not have been called (disabled action, missing species, unauthenticated
path, failed prerequisite, sync/upload not started). Do not infer "not called" from
markup alone.

## Before writing new tests

Read at least **5 existing tests** of the same type (page, component, service) in
`FishingLogBook.Web.Tests` before adding new ones. Match the production feature folder
and `{Thing}Tests` leaf naming. Do not create a new top-level `SomethingTests` directory.

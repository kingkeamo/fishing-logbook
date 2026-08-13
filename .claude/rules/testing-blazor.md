---
paths:
  - "tests/FishingLogBook.WebTests/**/*.cs"
  - "src/FishingLogBook.Web/**/*.razor"
  - "src/FishingLogBook.Web/**/*.razor.cs"
---

# Blazor & Web Testing Conventions

Production Blazor patterns are in **`blazor.md`**. Server-side C# test rules are in
**`testing-csharp.md`**.

## AI feature definition of done (mandatory)

When you add or change production code under `src/FishingLogBook.Web/` (`.razor`,
`.razor.cs`, `Services/`, `Models/`, `Configuration/`), you must **in the same task** add
or update tests in `tests/FishingLogBook.WebTests/`. Application/Api tests do not replace
Web tests for code in `FishingLogBook.Web`.

## Production code — ask before changing (mandatory)

Same rule as **`testing-csharp.md`**: never modify `src/` production code to make a test
pass without explicit approval. Fix the test first; otherwise propose the production change
and stop.

## Purpose: component tests must catch code changes

A test that only checks "renders without error" is not useful. Assert observable behaviour
that would break if the UI logic changed:

- rendered markup / text content / element presence (`Find`, `TextContent`),
- mocked client services: `Received()` / `DidNotReceive()` with `Arg.Is<>` when the
  component passes specific values.

## Stack

- **bUnit 2.x** (`BunitContext`) + **xUnit** + **NSubstitute** + **FluentAssertions**.
- Test project `FishingLogBook.WebTests` references `FishingLogBook.Web` and
  `FishingLogBook.Shared`.

## MudBlazor + bUnit disposal (important)

MudBlazor registers services that are **`IAsyncDisposable`-only** (e.g.
`KeyInterceptorService`). bUnit's synchronous context disposal throws on these. Therefore:

- Do **not** inherit `BunitContext` on the test class (xUnit disposes it synchronously).
- Instead, create the context **inside each `async Task` test** with `await using`, so it
  is disposed asynchronously:

```csharp
private static BunitContext CreateContext(ISystemStatusClient client)
{
    var context = new BunitContext();
    context.JSInterop.Mode = JSRuntimeMode.Loose;
    context.Services.AddMudServices();
    context.Services.AddSingleton(client);
    return context;
}

[Fact]
public async Task ShouldShowOnline_WhenApiAndDatabaseAreHealthy()
{
    // Arrange
    var client = Substitute.For<ISystemStatusClient>();
    client.GetApiHealthAsync(Arg.Any<CancellationToken>()).Returns(new HealthResponse("Healthy"));
    await using var context = CreateContext(client);

    // Act
    var cut = context.Render<SystemStatus>();

    // Assert
    cut.WaitForAssertion(() => cut.Find("#status-row-api").TextContent.Should().Contain("Online"));
    await client.Received(1).GetApiHealthAsync(Arg.Any<CancellationToken>());
}
```

## Required setup

- `JSInterop.Mode = JSRuntimeMode.Loose` (MudBlazor and Blazor make JS calls during render).
- `Services.AddMudServices()` and a NSubstitute mock for each `I*Client` the component
  injects, with default `Returns(...)` for anything called during `OnInitializedAsync`.
- Render with `context.Render<TComponent>(...)`; query with stable `id` selectors; use
  `WaitForAssertion` for async initialisation.

## Naming & AAA

- Class `{Component}Tests`; methods `Should{Behaviour}_When{Condition}`.
- Every test uses exactly the `// Arrange` / `// Act` / `// Assert` section comments.

## Dependency assertions (every test)

Verify the mocked client services, not only markup. Both success and failure paths need
`Received()` / `DidNotReceive()` with `Arg.Is<>` unless that path never calls the service.

## Before writing new tests

Read the existing Web tests before adding new ones and match the pattern.

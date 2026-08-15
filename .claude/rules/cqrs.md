---
paths:
  - "src/FishingLogBook.Api/**/*.cs"
  - "src/FishingLogBook.Application/**/*.cs"
  - "src/FishingLogBook.DependencyInjection/**/*.cs"
  - "tests/FishingLogBook.Application.Tests/**/*.cs"
  - "tests/FishingLogBook.Api.Tests/**/*.cs"
---

# CQRS & MediatR (RahPortal-aligned)

Same vertical-slice CQRS as **rah-portal**: MediatR in Application, FluentValidation pipeline,
`ValidatedResponse`, Mapster, one file per command/query. Handlers call **application
services**, which call repositories. The host is still **minimal APIs** (not controllers).
Do **not** put orchestration in the endpoint, and do **not** put SQL in the handler.

Use **MediatR 12.5.0** (`IMediator`, `IRequest<TResponse>`, `IRequestHandler<TRequest, TResponse>`).
That is the last Apache-2.0 release. Pin it in `Directory.Packages.props`.

**Do not** use MediatR 13+, `LuckyPennySoftware.MediatR`, or any Lucky Penny commercial
package — those are RPL-1.5 / paid. Do not add Wolverine, MassTransit, martinothamar
`Mediator`, or a second mediator.

Register in `AddFishingLogBookApplication` (composition root):

- `AddValidatorsFromAssembly` (Application)
- `AddMediatR` scanning the Application assembly
- `ValidationBehaviour<,>` as `IPipelineBehavior<,>` (transient)
- Mapster `TypeAdapterConfig` / `IRegister` from `Application/Common/Mappings`
- Application `I*Service` implementations as `AddScoped`

Handlers, validators, and mappings are discovered automatically — do not register
individual handlers or validators by hand.

## Folder layout (`FishingLogBook.Application`)

Organize by feature under `FishingLogBook.Application/{Feature}/`:

```text
{Feature}/
  Commands/   → IRequest<TResponse>, Handler, Response, Validator (same file)
  Queries/    → same pattern

Contracts/Repositories/   → I*Repository
Contracts/Services/       → I*Service
Services/                 → *Service implementations
Args/                     → *Args filter/query objects for repositories
Common/Responses/ValidatedResponse.cs
Common/Behaviours/ValidationBehaviour.cs
Common/Mappings/          → Mapster IRegister
```

Do not put handlers in `FishingLogBook.Shared` or in the API project.

New application work uses this layout. Convert `SystemStatusService` to
`ISystemStatusService` + a query handler when you next change that feature; do not
drive-by rewrite unrelated features.

## Naming

| Artifact | Pattern | Example |
|----------|---------|---------|
| Command/Query | `{Action}{Entity}Command` / `Query` | `AddCatchCommand`, `GetDatabaseStatusQuery` |
| Handler | `{Name}Handler` | `AddCatchHandler`, `GetDatabaseStatusHandler` |
| Response | `{Name}Response : ValidatedResponse` | `AddCatchResponse` |
| Validator | `{Name}Validator : AbstractValidator<T>` | `AddCatchCommandValidator` |
| Service | `I{Entity}Service` / `{Entity}Service` | `ICatchService`, `CatchService` |

Queries do not mutate. Commands are not used as a disguised query. One request type + one
handler per use case.

**Co-location (mandatory):** command/query, handler, response, and validator live in
**one `.cs` file** per use case (same as rah-portal).

## Call chain (mandatory)

```text
Endpoint → IMediator.Send → Handler → I*Service → I*Repository
```

- Endpoints inject `IMediator` only (plus framework types).
- Handlers inject `I*Service` (and `ILogger<T>`). Do not inject `I*Repository` into a
  handler, and do not inject `IMediator` into a handler (no nested sends).
- Services inject `I*Repository` (and other services). They return
  **FluentResults** `Result` / `Result<T>` — not exceptions for expected failures.
- Repositories return FluentResults `Result` / `Result<T>` (see **`database.md`**).
- Map with Mapster `.Adapt<T>()` at the service/handler boundary (Domain ↔ Shared DTO).

## ValidatedResponse

`Application/Common/Responses/ValidatedResponse.cs`:

- `IsSuccess` / `IsFailure` drive API responses
- `ErrorMessage` for handler/service failures (`result.IsFailed` → `result.Errors`)
- `ValidationErrors` (`IList<ValidationFailure>`) for FluentValidation failures
- FluentValidation failures are attached via `ValidationBehaviour` (only when `TResponse`
  inherits `ValidatedResponse` and has a public parameterless constructor)

Every command/query `TResponse` **must** inherit `ValidatedResponse`.

On `result.IsFailed`, return `new *Response { ErrorMessage = result.Errors[0].Message }`
(or join messages). Do not throw for expected failures.

## Handlers

- Implement `IRequestHandler<TRequest, TResponse>`
- Return `*Response : ValidatedResponse` with `ErrorMessage`, `ValidationErrors`, or
  success data (Shared `*Dto` on the response — e.g. `Data`, `NewId`)
- Never Infrastructure types, SQL, Dapper, or Npgsql
- Keep cyclomatic complexity ≤ 10 (see **`csharp.md`**)

## Validators

- `AbstractValidator<T>` in the same file as the command/query
- Validate nested properties (`RuleFor(x => x.Catch).NotNull()`), not
  `RuleFor(x => x).NotNull()` on the request — FluentValidation rejects `Validate(null)`
  before that rule runs
- Skip a validator when the request has no input to validate (parameterless query)

## Minimal API endpoints

Endpoints stay thin:

```csharp
var response = await mediator.Send(command, cancellationToken);
if (response.IsFailure)
{
    return Results.BadRequest(response);
}

return Results.Ok(response);
```

- Map HTTP body/route/query into the command or query in the endpoint (or a private
  static helper in that `Endpoints/` file).
- Keep OpenAPI metadata (`.WithName`, `.WithTags`, `.Produces`).
- Runtime health must still reflect reality (503 when a dependency was actually checked
  and failed) — `IsFailure` → 400 is for validation/domain errors, not for “we skipped
  the health check”.
- Wire **data** types the Blazor client needs live in `Shared` (`*Dto`).
  `ValidatedResponse` stays in Application (FluentValidation types). On 200 the client
  reads Shared data fields; on 400 it uses status + `errorMessage`.

## Pipeline behaviors

The only required behavior is `ValidationBehaviour`. Do not add logging/auth behaviors
until a cross-cutting concern is genuinely shared.

## What not to do

- Do not inject `IMediator` into Infrastructure, Domain, or handlers.
- Do not put MediatR types on `Shared` DTOs.
- Do not skip `I*Service` and call a repository from a handler.
- Do not use CQRS as an excuse for a second database or separate read store.
- Do not split command/handler/response/validator across files.
- Do not hand-roll a Result type — use **FluentResults**.

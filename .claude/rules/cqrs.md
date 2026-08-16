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
- Mapster `TypeAdapterConfig.GlobalSettings.Scan` of Application `IRegister` types,
  plus `IMapper` as a singleton (same as rah-portal)
- Application `I*Service` implementations as `AddScoped`

Handlers, validators, and mappings are discovered automatically — do not register
individual handlers, validators, or `IRegister` types by hand.

## Folder layout (`FishingLogBook.Application`)

Organize by feature under `FishingLogBook.Application/{Feature}/`:

```text
{Feature}/
  Commands/   → IRequest<TResponse>, Handler, Response, Validator (same file)
  Queries/    → same pattern
  Services/   → feature-owned *Service implementations
  Models/     → feature-owned application models, only when the feature needs them

Contracts/Repositories/   → I*Repository
Contracts/Services/       → I*Service (including request-scoped ICurrentUser)
Args/                     → *Args lookup/filter objects for repositories
Common/Responses/ValidatedResponse.cs
Common/Behaviours/ValidationBehaviour.cs
Common/Mappings/          → Mapster *MappingRegistration (IRegister)
```

Do not create empty `Commands/`, `Queries/`, `Services/`, or `Models/` folders
for appearance. Add a folder when the feature has types for it.

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
| Mapping | `{Feature}MappingRegistration` | `UserMappingRegistration` |

Queries do not mutate. Commands are not used as a disguised query. One request type + one
handler per use case.

A use case that **can create state** is a **command**, even when a later call only returns
an existing record. Get-or-create / resolve-or-create belongs in `Commands/`, not
`Queries/`. Example: first authenticated identity resolution may insert `User` +
`UserIdentity`, so it is `ResolveCurrentUserCommand` (or `GetOrCreateCurrentUserCommand`),
not a query.

**Co-location (mandatory):** command/query, handler, response, and validator live in
**one `.cs` file** per use case (same as rah-portal).

## Call chain (mandatory)

```text
Endpoint → IMediator.Send → Handler → I*Service → I*Repository
```

- Endpoints inject `IMediator` only (plus framework types). Authorised endpoints that
  only need the already-resolved FishingLogBook `UserId` may also inject `ICurrentUser`.
  They must not parse claims or send identity-resolution commands again.
- Handlers inject `I*Service` (and `ILogger<T>`). Do not inject `I*Repository` into a
  handler, and do not inject `IMediator` into a handler (no nested sends).
- Services inject `I*Repository` (and other services). They return
  **FluentResults** `Result` / `Result<T>` — not exceptions for expected failures.
- Repositories return FluentResults `Result` / `Result<T>` (see **`database.md`**).
- Map with Mapster `.Adapt<T>()` at application mapping boundaries (command/query → Args,
  Args → lookup Args, Domain → Shared DTO). Do **not** hand-map with
  `new TArgs { Prop = command.Prop }`. Domain construction (`new User`, `new UserIdentity`)
  stays explicit when it represents behaviour.
- Handlers do **not** manage SQL transactions. Begin/commit/rollback and unique-violation
  recovery belong in the application service/repository boundary (`database.md`). The
  handler is orchestration only.

## Mapster (mandatory)

Same pattern as rah-portal. Use Mapster at **application mapping boundaries**:

- Command/Query → Args (`command.Adapt<ResolveUserIdentityArgs>()`)
- Args → lookup Args (`args.Adapt<FindUserIdentityArgs>()`)
- Domain → Shared DTO
- DTO → Web/application model where that copy is mechanical

That is the only allowed mapping at those boundaries. Do **not** hand-map with
`new TArgs { Prop = command.Prop }`.

```csharp
var result = await _userIdentityService.ResolveAsync(
    command.Adapt<ResolveUserIdentityArgs>(),
    cancellationToken);
```

Do **not** require Mapster for Domain object creation. `new User { ... }` and
`new UserIdentity { ... }` are correct when that construction is application or
domain behaviour (ids, ownership links, required fields). Do not hide that
construction behind `.Adapt<User>()` merely for consistency.

- Identical property names map by convention. Still add
  `config.NewConfig<TSource, TDest>()` in an `IRegister` when the pair is a documented
  contract (command → service Args, Args → lookup Args, Domain → DTO).
- Use `.Map(...)` only when names differ, nested objects need mapping, or a property
  must be ignored or transformed.
- Put `IRegister` types in `Application/Common/Mappings/` named `*MappingRegistration`.
- Composition root (same as rah-portal `AddRahPortalMappings`):

```csharp
var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
typeAdapterConfig.Scan(typeof(UserMappingRegistration).Assembly);
services.AddSingleton<IMapper>(new Mapper(typeAdapterConfig));
```

- Prefer `source.Adapt<T>()` in handlers (uses `GlobalSettings`). Inject `IMapper` only
  when a service needs `_mapper.Config` for nested/custom Adapt.
- Do not inject Mapster into Domain. Do not put mapping logic in endpoints.

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
until a cross-cutting concern is genuinely shared. Do **not** introduce a MediatR
pipeline behavior for authentication unless a genuinely shared need appears later.

## Authentication boundary

Commands and queries must not receive:

- `HttpContext`
- `ClaimsPrincipal`
- JWT / token objects
- Cognito (or other IdP) framework types
- `AuthenticationState`

The API/authentication boundary translates a **validated** token into simple
application-safe values and then sends a command or query. For external identity
resolution that means `Provider`, `Subject`, and authenticated `Email` (for Cognito,
`Subject` is the validated `sub` claim and `Email` is the authenticated `email`
claim). The handler maps those command fields with
`command.Adapt<ResolveUserIdentityArgs>()` and passes that object to
`IUserIdentityService`. Add later profile fields (FirstName, LastName, DisplayName)
to the command, the args type, and `UserMappingRegistration` — do not widen
`ResolveAsync` with more primitives, and do not hand-map the new fields. Email is
account data, not the identity lookup key. Application code must not parse JWT claims.

## Internal identity

Once external identity has been resolved, domain and application use cases operate on
the FishingLogBook internal `UserId`. Cognito `sub` (or another provider subject) must
not leak into normal domain commands as the product owner identifier. Store `sub` only
as `UserIdentity.Subject`.

## Current user

Where several use cases need the authenticated FishingLogBook `UserId` (and the
authenticated `Email` already taken from the validated token), expose them
through a small request-scoped application abstraction (`ICurrentUser` in
`Application/Contracts/Services`). Resolve the
external identity **once** per authenticated request (API middleware sends
`ResolveCurrentUserCommand`, then assigns `ICurrentUser` with `UserId` and `Email`).
Do not parse claims or resolve `Provider` + `Subject` again in every handler.

## What not to do

- Do not inject `IMediator` into Infrastructure, Domain, or handlers.
- Do not put MediatR types on `Shared` DTOs.
- Do not skip `I*Service` and call a repository from a handler.
- Do not use CQRS as an excuse for a second database or separate read store.
- Do not split command/handler/response/validator across files.
- Do not hand-roll a Result type — use **FluentResults**.
- Do not pass `HttpContext`, `ClaimsPrincipal`, JWTs, or Cognito types into Application.
- Do not use Cognito `sub` as the domain `UserId` on commands or entities.
- Do not hand-map command/query properties onto Args or DTOs — use `.Adapt<T>()`.
- Do not construct a partially initialised Domain entity to carry lookup/filter
  criteria — use `*Args`.

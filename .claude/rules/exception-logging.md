---
paths:
  - "src/FishingLogBook.Api/**/*.cs"
  - "src/FishingLogBook.Application/**/*.cs"
  - "src/FishingLogBook.Domain/**/*.cs"
  - "src/FishingLogBook.Infrastructure/**/*.cs"
  - "src/FishingLogBook.DependencyInjection/**/*.cs"
  - "src/FishingLogBook.Db.Migrations/**/*.cs"
  - "src/FishingLogBook.Db.Migrations.App/**/*.cs"
---

# Server-side exception logging

Every `catch` block in production **server-side** code must log the caught exception.

This applies to all server-side layers and integrations:

- `FishingLogBook.Api`
- `FishingLogBook.Application`
- `FishingLogBook.Domain` where catch blocks exist
- `FishingLogBook.Infrastructure`
- repositories and other database access
- external HTTP/API clients
- authentication integrations
- object storage / R2
- synchronisation/orchestration
- background processing
- DbUp migrations and the migration runner

Silent catch blocks are not permitted.

Blazor WebAssembly (`FishingLogBook.Web`) is not covered by this rule. Client
diagnostics stay on the existing Web logging/diagnostic path.

## When an exception is caught

If an exception is caught and then:

- converted to a `Result`
- converted to a typed error
- translated into an HTTP response
- suppressed
- retried
- handled locally

the **original exception object** must be logged at an appropriate level.

## Levels

Unexpected technical exceptions:

```csharp
_logger.LogError(exception, "Failed to save the catch {CatchId}.", catchId);
```

Expected or recoverable exceptional conditions (unique-key recovery, known
constraint conflict, invalid caller argument mapped to 400):

```csharp
_logger.LogWarning(exception, "User identity already exists; recovering the existing user.");
```

Pass the exception as the first argument to `ILogger` so stack trace, inner
exception, and exception type are retained. Do not log `exception.ToString()`
or `exception.Message` as a substitute for the exception object.

Safe, generic messages may still be returned to callers and API clients.

Never expose raw exception details to API clients.

## Cleanup-and-rethrow

A `catch` that only rolls back a transaction (or similar cleanup) and rethrows
the same exception may omit a log **if** a later `catch` in the same method
logs that exception before translating or swallowing it. Do not log the same
exception twice.

Request-abort `OperationCanceledException` that is immediately rethrown may be
logged at `LogDebug` so the catch is not silent without treating disconnects as
errors.

## What not to log

Never log:

- access or refresh tokens
- photograph bytes or base64
- precise GPS coordinates
- complete Catch payloads
- secrets, connection strings, or unnecessary PII

Safe correlation fields such as CatchId, PhotographId, and UserId are allowed
in the log message template. Do not add coordinates, emails, or raw request
bodies to those templates.

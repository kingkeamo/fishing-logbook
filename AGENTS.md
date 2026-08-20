# Codex project instructions

The canonical engineering rules for this repository live in `.claude/rules/`.
Before planning, editing, reviewing, testing, committing, or performing GitHub
operations, read and follow every rule file relevant to the task.

Always read these workflow rules:

- `.claude/rules/product-workflow.md`
- `.claude/rules/self-review.md`
- `.claude/rules/git-commits.md` when working with branches or commits
- `.claude/rules/github-operations.md` when interacting with GitHub

Read the applicable technical rules based on the files being changed:

- C# and application code: `csharp.md`, `cqrs.md`, `exception-logging.md`
- Database and persistence: `database.md`
- Blazor/web code: `blazor.md`
- Terraform/infrastructure: `terraform.md`
- Tests: `testing-csharp.md` and/or `testing-blazor.md`

Resolve all paths above relative to `.claude/rules/`. Never expose, copy, log,
or commit credentials. Treat local settings, environment files, tokens, and
secret-bearing MCP configuration as sensitive.


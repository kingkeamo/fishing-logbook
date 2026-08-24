# Product workflow

FishingLogBook product requirements are defined by:

- `docs/Requirements.md`
- `docs/Architecture.md`
- `BUILD.md`

These documents are the source of truth.

There are two distinct operating modes.

## Planning mode

When asked to plan work:

1. Read the relevant requirements completely.
2. Do not modify application code.
3. Break requirements into small independently reviewable GitHub issues.
4. Each issue must contain:
   - Context
   - Goal
   - Requirement references
   - Acceptance Criteria
   - Test Requirements
   - Dependencies
   - Out of Scope
   - Definition of Done
5. Acceptance Criteria must describe observable behaviour rather than implementation details.
6. Do not create duplicate issues.
7. Identify dependencies between issues.
8. Do not start implementing issues automatically.
9. Do not create GitHub issues until the proposed backlog has been reviewed and explicitly approved.

## Implementation mode

When asked to implement a GitHub issue:

1. Read the complete GitHub issue.
2. Fetch latest `origin/main`, then create a branch from **`origin/main`** (not local `main`) using the prefixes in **`git-commits.md`**: bugs are `fix/<n>-…`, urgent production patches are `hotfix/<n>-…`, everything else is `feature/<n>-…`.
3. Read every Requirements, Architecture and BUILD section referenced by the issue.
4. Read the applicable `.claude` rules in full (at least **`self-review.md`** plus the stack rules for the files you will change).
5. Do not expand scope beyond the issue.
6. Convert each Acceptance Criterion into one or more automated tests where practical. User-facing journeys must be assessed for whether Playwright coverage is warranted per **`self-review.md`**. Do not contaminate production architecture to force browser tests onto a product ticket.
7. Implement only what is required to satisfy the Acceptance Criteria.
8. Existing tests must continue to pass.
9. All new user-visible text must use localisation resources.
10. Add both English and French resource values.
11. Do not hard-code user-visible English.
12. Do not create or change cloud infrastructure unless explicitly instructed.
13. Never run terraform apply or terraform destroy. Follow `.claude/rules/terraform.md`.
14. Run the build and relevant tests.
15. Complete the mandatory **self-review** in **`self-review.md`**. Inspect the actual diff, the issue, the tests, and the applicable rules. Green tests do not complete this step.
16. Fix BLOCKER and SHOULD FIX findings from the self-review.
17. Run the **full applicable validation again**. A test run from before the self-review fixes does not count as final validation.
18. Report any Acceptance Criterion that cannot be satisfied rather than pretending it is complete.
19. A pull request should reference the GitHub issue. For a normal implementation issue that the PR fully completes, use GitHub's normal closing reference (for example `Closes #123`). For an issue explicitly marked as a **living**, **ongoing**, **tracking**, **epic**, or **backlog** issue that must remain open across multiple PRs, never use `Closes`, `Fixes`, `Resolves`, or another auto-closing keyword. Reference it with non-closing wording such as `Contributes to #123` or `Part of #123` instead. The PR description and completion report must include the Self Review section from **`self-review.md`**.
20. For a normal implementation issue, do not mark it complete or treat the PR as ready for review unless every Acceptance Criterion is satisfied **and** the self-review (including fixes and final validation) is complete. For a living/ongoing/tracking/epic/backlog issue, do not require the entire tracking issue to be complete: validate only the scoped items selected for that PR, update/check off those items after implementation, and leave the tracking issue open.

Do not stop at “Implementation complete. All tests pass.”

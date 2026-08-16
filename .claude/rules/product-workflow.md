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
4. Do not expand scope beyond the issue.
5. Convert each Acceptance Criterion into one or more automated tests where practical.
6. Implement only what is required to satisfy the Acceptance Criteria.
7. Existing tests must continue to pass.
8. All new user-visible text must use localisation resources.
9. Add both English and French resource values.
10. Do not hard-code user-visible English.
11. Do not create or change cloud infrastructure unless explicitly instructed.
12. Never run terraform apply or terraform destroy. Follow `.claude/rules/terraform.md`.
13. Run the build and relevant tests before considering the work complete.
14. Report any Acceptance Criterion that cannot be satisfied rather than pretending it is complete.
15. A pull request should reference the GitHub issue.
16. Do not mark an issue complete unless every Acceptance Criterion is satisfied.

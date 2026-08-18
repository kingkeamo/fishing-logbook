# Direct GitHub and Git operations

Perform Git and GitHub operations directly in the parent agent.

- Use ordinary shell `git` for local status, checkout, pull, add, commit and history.
- Use the project GitHub MCP directly for remote branches, file pushes, issues, pull
  requests, reviews, review threads, comments, titles and body updates.
- Do not use GitLens/GitKraken MCP tools. They invoke the repository's configured Git
  transport and may incorrectly require SSH credentials.
- Discover the exact MCP tool schema before invoking it.
- Do not delegate Git or GitHub reads or writes to subagents.
- Do not use `gh` or browser automation when the corresponding MCP operation is available.
- Push fixes to the existing pull-request branch. Do not create another branch or PR
  unless explicitly requested.
- Keep one logical change as one coherent commit. Do not split a push into multiple
  remote commits merely to work around a tool or payload failure.
- If a direct MCP operation fails, report the exact failure. Retry directly only when
  the failure is transient; do not fall back to a subagent.
- Stop on authentication or authorization failure rather than changing credentials or
  transport configuration.
- Never merge or approve a pull request unless explicitly requested.

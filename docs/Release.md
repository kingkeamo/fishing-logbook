# Releases

Development deploys continue automatically from `main`. They carry independent Web and API metadata using a development version, the deployed commit SHA, environment `dev`, and commit timestamp; no tag is required.

Production deploys are manual and must use an immutable annotated tag in the exact form `vX.Y.Z`. There is no permanent production, release, or develop branch.

## Normal release

1. Merge the reviewed feature PR into `main` and wait for CI and development deployment checks.
2. From an up-to-date `main`, create and push an annotated tag:

   ```bash
   git tag -a v0.2.0 -m "Release v0.2.0"
   git push origin v0.2.0
   ```

3. Run **Deploy production** manually, enter the exact tag, and choose the required target.
4. Approve the protected `prod` environment gate after confirming the tag and commit shown by the workflow.
5. Verify the authenticated About/System screen shows the expected, independent Web and API versions and SHAs.

Never move, delete, or reuse a published release tag. Correct mistakes with a new version.

## Hotfix

1. Branch from the currently deployed production tag and make the smallest fix.
2. Review and validate that temporary hotfix branch, then create the next patch tag from its exact commit (for example `v0.2.1`).
3. Follow the normal production workflow and verification steps above.
4. Bring the same commit back into `main` by merge or cherry-pick, according to the actual history, and delete the temporary branch.

## Rollback

Run **Deploy production** with a previously verified immutable tag. The workflow checks out and builds that exact tag, so rollback does not require a long-lived production branch. Record the rollback reason and follow up with a forward-fix patch release.

Web configuration and build metadata are injected before Blazor publish creates the service-worker asset manifest. The post-publish step validates only and must never rewrite hashed output. API metadata is embedded into the image as build arguments and exposed as runtime `Build__*` configuration. Missing or invalid production metadata fails the build/startup rather than falling back to development identity.

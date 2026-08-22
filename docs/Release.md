# Releases

Development deploys continue automatically from `main`. They carry independent Web and API metadata using a development version, the deployed commit SHA, environment `dev`, and commit timestamp; no tag is required.

Production deploys are manual and must use an immutable annotated tag in the exact form `vX.Y.Z`. There is no permanent production, release, or develop branch.

## Normal release

1. Merge the reviewed feature PR into `main` and wait for CI and development deployment checks.
2. Open **Actions**, select **Create Release**, choose `patch`, `minor`, or `major`, and run the workflow from `main`.
3. The workflow validates that exact current `main` commit, calculates the next semantic version, creates the immutable tag and GitHub Release, then starts **Deploy production** with `target = all`.
4. Approve the protected `prod` environment gate after confirming the tag and commit shown by the deployment workflow.
5. Verify the root site, Web/PWA and API. Confirm the authenticated About/System screen shows the expected, independent Web and API versions and SHAs, and that Grafana diagnostics contain the API build metadata.

Never move, delete or reuse a published release tag. Correct mistakes with a new version. Do not rerun **Create Release** to recover a partially completed release: it will correctly calculate a newer version.

## Release recovery

If the immutable tag was pushed but GitHub Release creation failed, keep the tag and create the missing Release against it:

   ```bash
   gh release create v0.2.0 --verify-tag --generate-notes --title "CBDF v0.2.0"
   ```

If release creation succeeded but production dispatch failed, manually run **Deploy production** using that existing tag and `target = all`. These recovery steps never move or recreate the tag.

As an emergency-only procedure when Actions is unavailable, an authorised maintainer may create the next correctly calculated annotated tag on a verified `main` commit and push it without force, then create the GitHub Release and manually start **Deploy production**. This is not the normal CBDF release path.

## Hotfix

1. Branch from the currently deployed production tag and make the smallest fix.
2. Review and fully validate that temporary hotfix branch. Because **Create Release** deliberately accepts only current `main`, calculate the next patch version, create its annotated tag on the hotfix commit, and push it without force using the emergency procedure above.
3. Create the GitHub Release for that existing tag, manually run **Deploy production** with `target = all`, approve the protected `prod` gate, and complete the normal verification steps.
4. Bring the same fix back into `main` by merge or cherry-pick, according to the actual history, and delete the temporary branch.

## Rollback

Run **Deploy production** with a previously verified immutable tag. The workflow checks out and builds that exact tag, so rollback does not require a long-lived production branch. Record the rollback reason and follow up with a forward-fix patch release.

Web configuration and build metadata are injected before Blazor publish creates the service-worker asset manifest. The post-publish step validates only and must never rewrite hashed output. API metadata is embedded into the image as build arguments and exposed as runtime `Build__*` configuration. Missing or invalid production metadata fails the build/startup rather than falling back to development identity.

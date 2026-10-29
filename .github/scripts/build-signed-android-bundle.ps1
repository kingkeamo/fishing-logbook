param(
    [string] $ApplicationVersion,
    [string] $ApplicationDisplayVersion,
    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'

if ($ApplicationVersion -and $ApplicationVersion -notmatch '^[1-9][0-9]*$') {
    throw 'The Android version code must be a positive integer.'
}

if ($ApplicationDisplayVersion -and $ApplicationDisplayVersion -notmatch '^[0-9A-Za-z][0-9A-Za-z._+-]{0,49}$') {
    throw 'The version name must use 1-50 letters, numbers, dots, underscores, plus signs or hyphens.'
}

$requiredSecrets = @(
    'ANDROID_UPLOAD_KEYSTORE_BASE64',
    'ANDROID_UPLOAD_KEYSTORE_PASSWORD',
    'ANDROID_UPLOAD_KEY_ALIAS',
    'ANDROID_UPLOAD_KEY_PASSWORD'
)
$missingSecrets = $requiredSecrets | Where-Object {
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
}
if ($missingSecrets.Count -gt 0) {
    throw "Missing required GitHub secrets: $($missingSecrets -join ', '). See docs/google-play-internal-testing.md."
}

$keystorePath = Join-Path $env:RUNNER_TEMP 'catch-upload.keystore'
$publishDirectory = Join-Path $env:RUNNER_TEMP "catch-android-publish-$([Guid]::NewGuid().ToString('N'))"
try {
    $keystoreBytes = [Convert]::FromBase64String($env:ANDROID_UPLOAD_KEYSTORE_BASE64)
    if ($keystoreBytes.Length -eq 0) {
        throw 'ANDROID_UPLOAD_KEYSTORE_BASE64 decoded to an empty file.'
    }
    [IO.File]::WriteAllBytes($keystorePath, $keystoreBytes)

    $properties = @(
        '-f', 'net10.0-android',
        '-c', 'Release',
        '-p:RuntimeIdentifier=android-arm64',
        '-p:MobileEnvironment=Development',
        '-p:AndroidPackageFormats=aab',
        '-p:AndroidKeyStore=true',
        "-p:AndroidSigningKeyStore=$keystorePath",
        '-p:AndroidSigningStorePass=env:ANDROID_UPLOAD_KEYSTORE_PASSWORD',
        "-p:AndroidSigningKeyAlias=$env:ANDROID_UPLOAD_KEY_ALIAS",
        '-p:AndroidSigningKeyPass=env:ANDROID_UPLOAD_KEY_PASSWORD',
        "-p:AndroidSdkDirectory=$env:ANDROID_HOME",
        "-p:JavaSdkDirectory=$env:JAVA_HOME"
    )
    if ($ApplicationVersion) {
        $properties += "-p:ApplicationVersion=$ApplicationVersion"
    }
    if ($ApplicationDisplayVersion) {
        $properties += "-p:ApplicationDisplayVersion=$ApplicationDisplayVersion"
    }

    dotnet publish src/FishingLogBook.Mobile/FishingLogBook.Mobile.csproj @properties --output $publishDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    node .github/scripts/resolve-signed-android-bundle.mjs $publishDirectory $OutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Signed AAB resolution failed with exit code $LASTEXITCODE."
    }
}
finally {
    if (Test-Path -LiteralPath $keystorePath) {
        Remove-Item -LiteralPath $keystorePath -Force
    }
}
